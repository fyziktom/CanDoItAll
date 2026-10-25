using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowProcessCancellationRecovery(IDbContextFactory<WorkflowDbContext> factory,
    ICanonicalRuntimeDatabase canonical, IWorkflowActiveRunRegistry activeRuns, IWorkflowExternalRequestBoundaryStore boundaries,
    IWorkflowResumeBoundaryStore resumes, TimeProvider clock) : IWorkflowProcessCancellationRecovery {
    private const int MaximumOriginBytes = 32768;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkflowRunId?> FindNextAsync(WorkflowProcessRunId processRunId, WorkflowRunId? after, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(processRunId.Value, Guid.Empty);
        if (after?.Value == Guid.Empty) {
            throw new ArgumentOutOfRangeException(nameof(after));
        }
        await using var database = await OpenAsync(cancellationToken).ConfigureAwait(false);
        FormattableString selection = after is { } cursor ? (FormattableString)$"""
            SELECT "RunId" AS "Value" FROM "AgentFramework_WorkflowRuns"
            WHERE "OriginProcessRunId" = {processRunId.Value} AND "RunId" > {cursor.Value}
            ORDER BY "RunId" LIMIT 1
            """ : (FormattableString)$"""
            SELECT "RunId" AS "Value" FROM "AgentFramework_WorkflowRuns"
            WHERE "OriginProcessRunId" = {processRunId.Value} ORDER BY "RunId" LIMIT 1
            """;
        var selected = await database.Database.SqlQuery<Guid>(selection).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return selected.Length == 0 ? null : new(selected.Single());
    }

    public async Task<WorkflowProcessCancellationStatus> StopAsync(WorkflowProcessRunId processRunId, WorkflowRunId runId, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(processRunId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        await using var database = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await database.Database.SqlQuery<WorkflowCancellationRow>($"""
            SELECT "RunId", "State", "OriginProcessRunId",
                CASE WHEN octet_length("OriginJson") BETWEEN 2 AND {MaximumOriginBytes} THEN "OriginJson" ELSE NULL END AS "OriginJson"
            FROM "AgentFramework_WorkflowRuns" WHERE "RunId" = {runId.Value}
            """).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null || row.OriginProcessRunId != processRunId.Value || row.OriginJson is null || !Enum.IsDefined((WorkflowRunState)row.State)) {
            return WorkflowProcessCancellationStatus.ReconciliationRequired;
        }
        WorkflowLaunchOrigin? origin;
        try {
            origin = JsonSerializer.Deserialize<WorkflowLaunchOrigin>(row.OriginJson, JsonOptions);
        } catch (JsonException) {
            return WorkflowProcessCancellationStatus.ReconciliationRequired;
        }
        var originalProfile = origin switch {
            WorkflowLaunchOrigin.ProcessDispatchAssignment mapped => mapped.Dispatch.ProfileId == canonical.Profile.Profile.Id,
            WorkflowLaunchOrigin.ProcessToolInvocation tool => tool.Invocation.Profile.ProfileId == canonical.Profile.Profile.Id && tool.Invocation.PreparedRunId == runId,
            WorkflowLaunchOrigin.ProcessAssignment => true,
            _ => false
        };
        if (!originalProfile ||
                WorkflowRunRecordEntity.ResolveOriginProcessRunId(origin) != processRunId.Value ||
                origin!.AuthorizationScope != WorkspaceScopeDescriptor.Process(processRunId.Value.ToString("D")) ||
                origin.AuthorizationPolicyFingerprint != WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint) {
            return WorkflowProcessCancellationStatus.ReconciliationRequired;
        }
        if (IsClosed((WorkflowRunState)row.State)) {
            return WorkflowProcessCancellationStatus.Closed;
        }
        var signal = activeRuns.TrySignalCancellation(runId);
        if (signal == WorkflowActiveRunCancellationSignal.Signalled) {
            return WorkflowProcessCancellationStatus.Pending;
        }
        if (signal == WorkflowActiveRunCancellationSignal.NotSupported) {
            return WorkflowProcessCancellationStatus.ReconciliationRequired;
        }
        if (signal != WorkflowActiveRunCancellationSignal.NotActive) {
            throw new InvalidDataException("Workflow cancellation returned an unknown active-owner signal.");
        }
        if ((WorkflowRunState)row.State != WorkflowRunState.WaitingForInput) {
            return WorkflowProcessCancellationStatus.Pending;
        }
        var pending = await database.Set<WorkflowExternalRequestRecordEntity>().AsNoTracking()
            .Where(item => item.RunId == runId.Value && item.RespondedAtUtc == null)
            .OrderBy(item => item.Id).Select(item => item.Id).Take(2).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (pending.Length != 1) {
            return WorkflowProcessCancellationStatus.ReconciliationRequired;
        }
        WorkflowExternalRequestId requestId = new(pending[0]);
        var observed = await boundaries.ReadAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (!observed.Succeeded || observed.Boundary is not { } boundary) {
            return WorkflowProcessCancellationStatus.ReconciliationRequired;
        }
        var stopped = await resumes.TryCancelAsync(new(runId, requestId, boundary.RequestVersion, clock.GetUtcNow(),
            "Workflow stopped after its original Process root admitted cancellation."), cancellationToken).ConfigureAwait(false);
        return stopped.Outcome switch {
            WorkflowResumeBoundaryCancellationOutcome.Cancelled or WorkflowResumeBoundaryCancellationOutcome.AlreadyTerminal
                when stopped.Run?.RunId == runId && IsClosed(stopped.Run.State) => WorkflowProcessCancellationStatus.Closed,
            WorkflowResumeBoundaryCancellationOutcome.ActiveResume => WorkflowProcessCancellationStatus.Pending,
            _ => WorkflowProcessCancellationStatus.ReconciliationRequired
        };
    }

    private static bool IsClosed(WorkflowRunState state) => state is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled;

    private async Task<WorkflowDbContext> OpenAsync(CancellationToken cancellationToken) {
        var database = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!database.Database.IsNpgsql()) {
            await database.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Workflow Process recovery requires PostgreSQL's bounded owner contract.");
        }
        database.Database.SetCommandTimeout(5);
        return database;
    }
}

internal sealed record WorkflowCancellationRow(Guid RunId, int State, Guid? OriginProcessRunId, string? OriginJson);
