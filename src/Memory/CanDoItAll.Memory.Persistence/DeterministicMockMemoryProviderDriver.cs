using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Persistence;

public sealed class DeterministicMockMemoryProviderDriver : IMemoryProviderDriver
{
    private int dispatchCount;

    public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

    public int DispatchCount => dispatchCount;

    public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryContextQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref dispatchCount);

        var sourceRef = request.SourceProvenance.SourceSnapshotId?.Value
            ?? request.SourceProvenance.SourceModule
            ?? "memory://mock";
        var sourceLabel = request.SourceProvenance.Citations.FirstOrDefault()
            ?? request.SourceProvenance.SourceModule
            ?? "deterministic mock memory";

        var contextPack = new MemoryContextPack(
            MemoryContextPackId.New(),
            $"Mock memory context for {request.Query}",
            [
                new MemoryContextSection(
                    "Deterministic mock memory",
                    $"Provider '{provider.InstanceId}' received query '{request.Query}'.",
                    [new MemoryCitation(sourceRef, sourceLabel)],
                    Confidence: 1.0m)
            ],
            Warnings: [],
            ProviderConfidence: 1.0m,
            FeedbackHandle: null);
        return Task.FromResult(MemoryProviderDriverResult.ContextPackResult(
            contextPack,
            "Deterministic mock memory completed."));
    }
}
