using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class UnavailableProcessRuntimeEvidenceSourceProvider(
    ILogger<UnavailableProcessRuntimeEvidenceSourceProvider> logger) : IProcessRuntimeEvidenceSourceProvider
{
    public Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProcessRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogWarning(
            "Process runtime evidence source was requested before the process runtime evidence provider is deployed. ProcessRunId={ProcessRunId}.",
            request.ProcessRunId?.ToString("D") ?? "global");

        throw new InvalidOperationException(
            "Process runtime evidence source is not available until the process runtime evidence provider is deployed.");
    }
}
