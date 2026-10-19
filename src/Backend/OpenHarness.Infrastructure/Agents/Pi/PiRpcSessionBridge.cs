using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OpenHarness.Application.Common.Interfaces;

namespace OpenHarness.Infrastructure.Agents.Pi;

public sealed class PiRpcSessionBridge : IPiRpcSessionBridge
{
    private readonly string? _workingDirectory;
    private readonly PiRpcOptions _options;
    private readonly ILogger<PiRpcSessionBridge> _logger;

    private Process? _process;
    private StreamWriter? _stdinWriter;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<PiSessionEvent> _eventChannel = Channel.CreateUnbounded<PiSessionEvent>(
        new UnboundedChannelOptions { SingleWriter = true, SingleReader = false });

    private Task? _stdoutTask;
    private Task? _stderrTask;
    private bool _disposed;

    public bool IsRunning => _process is { HasExited: false };

    public PiRpcSessionBridge(
        string? workingDirectory,
        PiRpcOptions options,
        ILogger<PiRpcSessionBridge> logger)
    {
        _workingDirectory = workingDirectory;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        var workDir = _workingDirectory
            ?? _options.DefaultWorkingDirectory
            ?? Directory.GetCurrentDirectory();

        var psi = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            Arguments = BuildArguments(),
            WorkingDirectory = workDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            _logger.LogInformation("Starting Pi RPC sub-process: {FileName} {Arguments} in {WorkingDir}",
                psi.FileName, psi.Arguments, psi.WorkingDirectory);

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Exited += OnProcessExited;

            var started = _process.Start();
            if (!started)
            {
                throw new InvalidOperationException($"Failed to start Pi RPC process: {_options.ExecutablePath}");
            }

            _stdinWriter = _process.StandardInput;

            _stdoutTask = Task.Run(() => ReadStandardOutputAsync(_cts.Token), _cts.Token);
            _stderrTask = Task.Run(() => ReadStandardErrorAsync(_cts.Token), _cts.Token);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Pi RPC process.");
            _eventChannel.Writer.TryComplete(ex);
            throw;
        }
    }

    private string BuildArguments()
    {
        var sb = new StringBuilder("--mode rpc --no-session");
        foreach (var extra in _options.ExtraArgs)
        {
            sb.Append(' ').Append(extra);
        }
        return sb.ToString();
    }

    public async Task SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureRunning();

        var payload = new
        {
            type = "prompt",
            message = prompt
        };

        var json = JsonSerializer.Serialize(payload);
        await WriteLineAsync(json, cancellationToken);
    }

    public async Task SendApprovalResponseAsync(Guid toolExecutionId, bool confirmed, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureRunning();

        var payload = new
        {
            type = "extension_ui_response",
            id = toolExecutionId.ToString(),
            confirmed
        };

        var json = JsonSerializer.Serialize(payload);
        await WriteLineAsync(json, cancellationToken);
    }

    public async IAsyncEnumerable<PiSessionEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(linkedCts.Token))
        {
            yield return evt;
        }
    }

    private async Task WriteLineAsync(string json, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (_stdinWriter is null)
            {
                throw new InvalidOperationException("StandardInput writer is not initialized.");
            }

            await _stdinWriter.WriteLineAsync(json.AsMemory(), cancellationToken);
            await _stdinWriter.FlushAsync(cancellationToken);
            _logger.LogDebug("Wrote to Pi RPC stdin: {Json}", json);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadStandardOutputAsync(CancellationToken cancellationToken)
    {
        try
        {
            var reader = _process!.StandardOutput;

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    _logger.LogInformation("Pi RPC stdout stream closed.");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var evt = ParseEvent(doc.RootElement);
                    if (evt is not null)
                    {
                        _eventChannel.Writer.TryWrite(evt);
                    }
                }
                catch (JsonException jex)
                {
                    // EC-006: Malformed JSONL line handling: log and continue reading
                    _logger.LogWarning(jex, "Malformed JSONL line received from Pi RPC: {RawLine}", line);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from Pi RPC stdout.");
            _eventChannel.Writer.TryWrite(new PiSessionEvent(
                Type: "error",
                Delta: $"Process communication error: {ex.Message}"));
        }
        finally
        {
            _eventChannel.Writer.TryComplete();
        }
    }

    private async Task ReadStandardErrorAsync(CancellationToken cancellationToken)
    {
        try
        {
            var reader = _process!.StandardError;

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) break;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    _logger.LogWarning("Pi RPC stderr: {ErrorLine}", line);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected cancellation
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error reading Pi RPC stderr.");
        }
    }

    private PiSessionEvent? ParseEvent(JsonElement root)
    {
        if (!root.TryGetProperty("type", out var typeProp))
        {
            return null;
        }

        var type = typeProp.GetString() ?? string.Empty;

        return type switch
        {
            "message_update" => ParseMessageUpdate(root),
            "thinking_update" => ParseThinkingUpdate(root),
            "tool_execution_start" => ParseToolStart(root),
            "extension_ui_request" => ParseApprovalRequest(root),
            "tool_execution_end" => ParseToolEnd(root),
            "agent_end" or "turn_completed" => ParseAgentEnd(root),
            "error" => ParseError(root),
            _ => new PiSessionEvent(Type: type, Delta: root.ToString())
        };
    }

    private static PiSessionEvent ParseMessageUpdate(JsonElement root)
    {
        var delta = root.TryGetProperty("delta", out var d) ? d.GetString()
            : root.TryGetProperty("content", out var c) ? c.GetString()
            : null;

        return new PiSessionEvent(Type: "text.delta", Delta: delta);
    }

    private static PiSessionEvent ParseThinkingUpdate(JsonElement root)
    {
        var delta = root.TryGetProperty("delta", out var d) ? d.GetString()
            : root.TryGetProperty("content", out var c) ? c.GetString()
            : null;

        return new PiSessionEvent(Type: "thinking.delta", Delta: delta);
    }

    private static PiSessionEvent ParseToolStart(JsonElement root)
    {
        var toolName = root.TryGetProperty("tool", out var t) ? t.GetString() : "unknown";
        Guid? executionId = null;

        if (root.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var parsedId))
        {
            executionId = parsedId;
        }
        else
        {
            executionId = Guid.CreateVersion7();
        }

        JsonElement? args = root.TryGetProperty("args", out var a) ? a.Clone() : null;

        return new PiSessionEvent(
            Type: "tool.started",
            ToolName: toolName,
            ToolExecutionId: executionId,
            Arguments: args);
    }

    private static PiSessionEvent ParseApprovalRequest(JsonElement root)
    {
        var toolName = root.TryGetProperty("tool", out var t) ? t.GetString()
            : root.TryGetProperty("method", out var m) ? m.GetString()
            : "sandbox_action";

        Guid? executionId = null;
        if (root.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var parsedId))
        {
            executionId = parsedId;
        }
        else
        {
            executionId = Guid.CreateVersion7();
        }

        JsonElement? args = root.TryGetProperty("args", out var a) ? a.Clone()
            : root.TryGetProperty("params", out var p) ? p.Clone()
            : null;

        return new PiSessionEvent(
            Type: "sandbox.approval_required",
            ToolName: toolName,
            ToolExecutionId: executionId,
            Arguments: args,
            RequiresApproval: true);
    }

    private static PiSessionEvent ParseToolEnd(JsonElement root)
    {
        Guid? executionId = null;
        if (root.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var parsedId))
        {
            executionId = parsedId;
        }

        JsonElement? result = root.TryGetProperty("result", out var r) ? r.Clone() : null;

        return new PiSessionEvent(
            Type: "tool.completed",
            ToolExecutionId: executionId,
            Result: result);
    }

    private static PiSessionEvent ParseAgentEnd(JsonElement root)
    {
        long? inTokens = null;
        long? outTokens = null;

        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("inputTokens", out var it)) inTokens = it.GetInt64();
            if (usage.TryGetProperty("outputTokens", out var ot)) outTokens = ot.GetInt64();
        }

        return new PiSessionEvent(
            Type: "turn.completed",
            InputTokens: inTokens,
            OutputTokens: outTokens);
    }

    private static PiSessionEvent ParseError(JsonElement root)
    {
        var msg = root.TryGetProperty("message", out var m) ? m.GetString() : root.ToString();
        return new PiSessionEvent(Type: "error", Delta: msg);
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        // EC-002: Detect process exit, alert channel
        var exitCode = _process?.ExitCode ?? -1;
        _logger.LogWarning("Pi RPC process exited with code {ExitCode}", exitCode);

        if (exitCode != 0)
        {
            _eventChannel.Writer.TryWrite(new PiSessionEvent(
                Type: "error",
                Delta: $"Process exited unexpectedly with exit code {exitCode}"));
        }
    }

    private void EnsureRunning()
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("Pi RPC session process is not running. Call StartAsync() first.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();

        try
        {
            if (_stdinWriter is not null)
            {
                await _stdinWriter.DisposeAsync();
                _stdinWriter = null;
            }

            if (_process is not null)
            {
                if (!_process.HasExited)
                {
                    try
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to kill Pi RPC process gracefully.");
                    }
                }

                _process.Dispose();
                _process = null;
            }

            if (_stdoutTask is not null) await _stdoutTask;
            if (_stderrTask is not null) await _stderrTask;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error during PiRpcSessionBridge disposal.");
        }
        finally
        {
            _writeLock.Dispose();
            _cts.Dispose();
        }
    }
}

