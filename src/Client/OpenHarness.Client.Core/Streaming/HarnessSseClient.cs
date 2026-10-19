using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace OpenHarness.Client.Core.Streaming;

public sealed record HarnessStreamEvent(
    string EventType,
    string RawData,
    JsonDocument? ParsedData = null);

public sealed class HarnessSseClient(HttpClient httpClient)
{
    public async IAsyncEnumerable<HarnessStreamEvent> StreamSessionEventsAsync(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/sessions/{sessionId}/stream");
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var parser = SseParser.Create(stream);

        await foreach (var item in parser.EnumerateAsync(cancellationToken))
        {
            JsonDocument? jsonDoc = null;
            if (!string.IsNullOrWhiteSpace(item.Data))
            {
                try
                {
                    jsonDoc = JsonDocument.Parse(item.Data);
                }
                catch
                {
                    // Non-json payload or plain text
                }
            }

            yield return new HarnessStreamEvent(item.EventType, item.Data, jsonDoc);
        }
    }
}

