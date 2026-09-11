using System.Data;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowProcessAssignmentRunQuery(IDbContextFactory<WorkflowDbContext> factory, ICanonicalRuntimeDatabase canonical)
    : IWorkflowProcessAssignmentRunQuery {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RequireRetainedChildAsync(WorkflowRunSnapshot child, Guid originalProfileId,
        WorkflowDefinitionSelection originalSelection, string originalInputJson, CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = WorkflowPersistenceProvider.IsInMemory(database) ? null
            : await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await RequireRetainedAsync(database, child, canonical.Profile.Profile.Id, originalProfileId, originalSelection, originalInputJson, cancellationToken);
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    internal static async Task RequireRetainedAsync(WorkflowDbContext database, WorkflowRunSnapshot child, Guid currentProfileId,
        Guid originalProfileId, WorkflowDefinitionSelection originalSelection, string originalInputJson, CancellationToken cancellationToken) {
        var (runId, stepId) = child.Origin switch {
            WorkflowLaunchOrigin.ProcessDispatchAssignment mapped => (mapped.Dispatch.ProcessRun.Value, mapped.Dispatch.Assignment.Value),
            WorkflowLaunchOrigin.ProcessAssignment legacy => (legacy.ProcessRun.Value, legacy.Assignment.Value),
            _ => throw new InvalidOperationException("The retained child has no original mapped Process identity.")
        };
        if (originalProfileId != currentProfileId) {
            throw new InvalidOperationException("The original Process continuation belongs to a different database profile.");
        }
        var retainedChildren = await PersistentWorkflowProcessAssignmentRunQuery.ReadAsync(database, new(runId), new(stepId), cancellationToken);
        if (retainedChildren.Count != 1 || retainedChildren[0].RunId != child.RunId) {
            throw new InvalidOperationException("The original Process assignment has missing or ambiguous Workflow children; continuation requires reconciliation.");
        }
        var callerKey = new WorkflowLaunchIdempotencyKey($"process-assignment:{runId:N}:{stepId:N}");
        var intent = new WorkflowLaunchIntent(originalSelection, WorkflowLaunchMode.Production, child.Origin,
            originalInputJson, WorkflowLaunchCompletionPolicy.WaitForStopped, new WorkflowLaunchIdempotency.CallerSupplied(callerKey));
        var expectedScope = WorkflowLaunchIdempotencyRequestFactory.CreateScope(intent, callerKey);
        var expectedFingerprint = WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(intent, originalInputJson);
        var rows = WorkflowPersistenceProvider.IsInMemory(database)
            ? await database.Set<WorkflowLaunchIdempotencyRecordEntity>().AsNoTracking()
                .Where(row => row.ReservedRunId == child.RunId.Value).ToArrayAsync(cancellationToken)
            : await database.Set<WorkflowLaunchIdempotencyRecordEntity>().FromSqlInterpolated($"""
                SELECT * FROM "AgentFramework_WorkflowLaunchIdempotency" WHERE "ReservedRunId" = {child.RunId.Value} FOR SHARE
                """).AsNoTracking().ToArrayAsync(cancellationToken);
        if (rows.Length != 1) {
            throw new InvalidOperationException("The original Workflow launch receipt is missing or ambiguous; continuation requires reconciliation.");
        }
        var receipt = rows[0];
        if (receipt.State != WorkflowLaunchIdempotencyClaimState.Completed || receipt.CompletedAtUtc is null ||
                receipt.CallerKey != expectedScope.CallerKey.Value || receipt.WorkflowId != expectedScope.WorkflowId.Value ||
                receipt.SelectionKind != expectedScope.SelectionKind || receipt.RequestedVersionId != (expectedScope.RequestedVersionId?.Value ?? Guid.Empty) ||
                receipt.Mode != expectedScope.Mode || receipt.OriginKind != expectedScope.OriginKind || receipt.OriginScopeKey != expectedScope.OriginScopeKey.Value ||
                receipt.Fingerprint != expectedFingerprint.Value || receipt.CanonicalInputHash != expectedFingerprint.CanonicalInputHash) {
            throw new InvalidOperationException("The original Workflow launch receipt does not prove this exact deferred assignment and input.");
        }
        var completed = JsonSerializer.Deserialize<WorkflowLaunchIdempotencyCompletion>(receipt.CompletionJson, JsonOptions)
            ?? throw new InvalidOperationException("The original Workflow launch completion cannot be read.");
        if (completed.Run.RunId != child.RunId || completed.Run.WorkflowId != child.WorkflowId || completed.Run.VersionId != child.VersionId ||
                completed.ResolvedRequest.Definition.Id != child.WorkflowId || completed.ResolvedRequest.Definition.VersionId != child.VersionId ||
                WorkflowRunRecordEntity.SerializeOrigin(completed.Run.Origin) != WorkflowRunRecordEntity.SerializeOrigin(child.Origin) ||
                WorkflowRunRecordEntity.SerializeOrigin(completed.ResolvedRequest.Origin) != WorkflowRunRecordEntity.SerializeOrigin(child.Origin) ||
                WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(intent, completed.ResolvedRequest.InputJson).CanonicalInputHash != expectedFingerprint.CanonicalInputHash) {
            throw new InvalidOperationException("The retained Workflow child differs from its original committed launch identity.");
        }
    }

    public async Task<IReadOnlyList<WorkflowRunSnapshot>> FindAsync(WorkflowProcessRunId runId, WorkflowProcessAssignmentId assignmentId,
        CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadAsync(database, runId, assignmentId, cancellationToken);
    }

    internal static async Task<IReadOnlyList<WorkflowRunSnapshot>> ReadAsync(WorkflowDbContext database,
        WorkflowProcessRunId runId, WorkflowProcessAssignmentId assignmentId, CancellationToken cancellationToken) {
        if (runId.Value == Guid.Empty || assignmentId.Value == Guid.Empty) {
            throw new ArgumentException("Exact mapped Workflow observation requires nonempty Process run and step identities.");
        }
        var rows = await database.Set<WorkflowRunRecordEntity>().AsNoTracking()
            .Where(row => row.OriginProcessRunId == runId.Value && row.OriginProcessAssignmentId == assignmentId.Value)
            .OrderBy(row => row.RunId).Take(2).ToArrayAsync(cancellationToken);
        return rows.Select(row => row.ToSnapshot()).ToArray();
    }

    internal static async Task RequireVacantAsync(WorkflowDbContext database, WorkflowLaunchOrigin.ProcessDispatchAssignment origin,
        CancellationToken cancellationToken) {
        if ((await ReadAsync(database, origin.Dispatch.ProcessRun, origin.Dispatch.Assignment, cancellationToken)).Count != 0) {
            throw new InvalidOperationException("This mapped Process assignment already admitted a Workflow child; observe the original receipt instead of launching another.");
        }
    }
}
