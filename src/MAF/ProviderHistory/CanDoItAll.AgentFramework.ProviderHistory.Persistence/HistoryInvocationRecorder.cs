using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryInvocationRecorder(
    IProviderHistoryPartition partitions,
    IDbContextFactory<AppDbContext> factory,
    IDatabaseRuntimeState runtime,
    IProviderHistoryCapture capture,
    TimeProvider clock,
    ILogger<HistoryInvocationRecorder> logger) : IProviderHistoryRecorder {
    public async Task<HistoryAttemptStart> BeginAsync(HistoryInvocation invocation, CancellationToken cancellationToken) {
        var context = invocation.Context;
        var expected = runtime.GetSnapshot();
        var partition = await partitions.GetAsync(cancellationToken);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = await db.Set<HistoryPolicyRow>().AsNoTracking()
            .SingleAsync(policy => policy.PartitionId == partition.StorageLineageId, cancellationToken);
        var owner = context.Owner is { } source
            ? new CanonicalEvidenceReference(partition, source.Kind, source.OwnerId, source.EvidenceId) : null;
        var start = new HistoryAttemptStart(new(Guid.NewGuid()), partition,
            new(expected.Generation, 0), context.RequestId, new(Guid.NewGuid()), HistoryStorageTimestamp.Normalize(clock.GetUtcNow()),
            invocation.Provider, invocation.Operation, context.Workload, context.Caller,
            HistoryPolicyStore.Snapshot(row), owner, context.CorrelationId) {
            ExternalReference = context.ExternalReference
        };
        if (context.CurrentTurn is { } turn) {
            start = start with { InputExpiresAtUtc = context.Attempts.FreezeInputExpiry(turn.InputRevision, start.InputExpiresAtUtc) };
        }
        try {
            await capture.BeginAsync(start, context.CurrentTurn, cancellationToken);
        } catch (Exception exception) {
            logger.LogError("History start failed before provider use. AttemptId={AttemptId} ProviderId={ProviderId} FailureType={FailureType}.",
                start.AttemptId.Value, start.Provider.Id?.Value, exception.GetType().Name);
            throw;
        }
        context.Attempts.Add(start);
        return start;
    }

    public async Task CompleteAsync(HistoryAttemptStart start, HistoryAttemptCompletion completion, string? response,
        CancellationToken cancellationToken) {
        try {
            await capture.CompleteAsync(start, completion, response, cancellationToken);
        } catch (Exception exception) {
            logger.LogError("History finalization failed; inference must not be repeated. AttemptId={AttemptId} FailureType={FailureType}.",
                start.AttemptId.Value, exception.GetType().Name);
            throw;
        }
    }
}
