using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenHarness.Application.Common.Interfaces;

namespace OpenHarness.Infrastructure.Agents.Pi;

public sealed class PiRpcSessionFactory(
    IOptions<PiRpcOptions> options,
    ILoggerFactory loggerFactory) : IPiRpcSessionFactory
{
    public IPiRpcSessionBridge CreateBridge(string? workingDirectory = null)
    {
        var logger = loggerFactory.CreateLogger<PiRpcSessionBridge>();
        return new PiRpcSessionBridge(workingDirectory, options.Value, logger);
    }
}

