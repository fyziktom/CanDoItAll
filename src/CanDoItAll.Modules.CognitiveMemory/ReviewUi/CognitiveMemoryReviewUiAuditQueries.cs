using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryReviewUiService
{
    private static async Task<IReadOnlyList<CognitiveMemoryOperatorAuditItem>> LoadOperatorAuditAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var items = new List<CognitiveMemoryOperatorAuditItem>(query.Take * 4);
        items.AddRange(await LoadMutationCommandAuditAsync(dbContext, query, cancellationToken));
        items.AddRange(await LoadMutationEventAuditAsync(dbContext, query, cancellationToken));
        items.AddRange(await LoadClaimAuditAsync(dbContext, query, cancellationToken));
        items.AddRange(await LoadEvidenceAuditAsync(dbContext, query, cancellationToken));
        items.AddRange(await LoadProjectionFailureAuditAsync(dbContext, query, cancellationToken));
        items.AddRange(await LoadRetentionCleanupAuditAsync(dbContext, query, cancellationToken));

        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.AuditKind)
            .Take(query.Take)
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryOperatorAuditItem>> LoadMutationCommandAuditAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var commandsQuery = dbContext.Set<CognitiveMemoryMutationCommandRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } projectId)
        {
            commandsQuery = commandsQuery.Where(command => command.ProjectId == projectId);
        }

        return (await commandsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(command => command.RequiresHumanReview)
            .ThenByDescending(command => command.UpdatedAtUtc)
            .Take(query.Take)
            .Select(command => new CognitiveMemoryOperatorAuditItem(
                command.Id,
                command.ProjectId,
                CognitiveMemoryOperatorAuditKind.MutationCommand,
                command.Id,
                CognitiveMemoryOperatorAuditSubjectKind.MutationCommand,
                ToOperatorAuditStatus(command.Status),
                $"{command.CommandKind} by {command.ActorId}",
                string.IsNullOrWhiteSpace(command.ReviewReason)
                    ? $"Affected memory: {CountJsonIds(command.AffectedMemoryRecordIdsJson)} / claims: {CountJsonIds(command.AffectedClaimIdsJson)} / evidence: {CountJsonIds(command.EvidenceAnchorIdsJson)}"
                    : command.ReviewReason,
                command.UpdatedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryOperatorAuditItem>> LoadMutationEventAuditAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var eventsQuery = dbContext.Set<CognitiveMemoryMutationAuditEventRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } projectId)
        {
            eventsQuery = eventsQuery.Where(item => item.ProjectId == projectId);
        }

        return (await eventsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Sequence)
            .Take(query.Take)
            .Select(item => new CognitiveMemoryOperatorAuditItem(
                item.Id,
                item.ProjectId,
                CognitiveMemoryOperatorAuditKind.MutationAuditEvent,
                item.MutationCommandId,
                CognitiveMemoryOperatorAuditSubjectKind.MutationAuditEvent,
                ToOperatorAuditStatus(item.EventKind),
                $"{item.EventKind} mutation event",
                $"Sequence {item.Sequence}. {item.Message}",
                item.CreatedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryOperatorAuditItem>> LoadClaimAuditAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var claimsQuery = dbContext.Set<CognitiveMemoryClaimRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } projectId)
        {
            claimsQuery = claimsQuery.Where(claim => claim.ProjectId == projectId);
        }

        return (await claimsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(claim => claim.ValidationState != CognitiveMemoryValidationState.Approved)
            .ThenByDescending(claim => claim.UpdatedAtUtc)
            .Take(query.Take)
            .Select(claim => new CognitiveMemoryOperatorAuditItem(
                claim.Id,
                claim.ProjectId,
                CognitiveMemoryOperatorAuditKind.ClaimState,
                claim.Id,
                CognitiveMemoryOperatorAuditSubjectKind.Claim,
                ToOperatorAuditStatus(claim.CurrentBeliefState, claim.ValidationState),
                FirstNonEmpty(claim.ClaimText, $"{claim.SubjectKey} {claim.PredicateKey} {claim.ObjectKey}"),
                $"{claim.ClaimKind} / {claim.CurrentBeliefState} / {claim.ValidationState} / memory {claim.MemoryRecordId:D} / stability {claim.StabilityState}",
                claim.UpdatedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryOperatorAuditItem>> LoadEvidenceAuditAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var anchorsQuery = dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } projectId)
        {
            anchorsQuery = anchorsQuery.Where(anchor => anchor.ProjectId == projectId);
        }

        return (await anchorsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(anchor => anchor.RedactionState != CognitiveMemoryRedactionState.Safe)
            .ThenByDescending(anchor => anchor.CreatedAtUtc)
            .Take(query.Take)
            .Select(anchor => new CognitiveMemoryOperatorAuditItem(
                anchor.Id,
                anchor.ProjectId,
                CognitiveMemoryOperatorAuditKind.EvidenceAnchor,
                anchor.Id,
                CognitiveMemoryOperatorAuditSubjectKind.EvidenceAnchor,
                ToOperatorAuditStatus(anchor.RedactionState),
                FirstNonEmpty(anchor.SourceSystem, "Evidence anchor"),
                $"{anchor.AnchorKind} / {anchor.TrustLevel} / {anchor.RedactionState} / {FirstNonEmpty(anchor.Locator, anchor.StructuredPath, anchor.SourceHash)}",
                anchor.CreatedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryOperatorAuditItem>> LoadProjectionFailureAuditAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var projectionsQuery = dbContext.Set<CognitiveMemoryProjectionStateRecord>()
            .AsNoTracking()
            .Where(projection => projection.RebuildRequired ||
                                 projection.Status == CognitiveMemoryProjectionStatus.RebuildRequired ||
                                 projection.Status == CognitiveMemoryProjectionStatus.Failed);
        if (query.ProjectId is { } projectId)
        {
            projectionsQuery = projectionsQuery.Where(projection => projection.ProjectId == projectId);
        }

        return (await projectionsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(projection => projection.Status == CognitiveMemoryProjectionStatus.Failed)
            .ThenByDescending(projection => projection.UpdatedAtUtc)
            .Take(query.Take)
            .Select(projection => new CognitiveMemoryOperatorAuditItem(
                projection.Id,
                projection.ProjectId,
                CognitiveMemoryOperatorAuditKind.ProjectionFailure,
                projection.Id,
                CognitiveMemoryOperatorAuditSubjectKind.ProjectionState,
                ToOperatorAuditStatus(projection.Status, projection.RebuildRequired),
                $"{projection.TargetProvider} projection requires operator attention",
                $"{projection.ProjectionKind} / {projection.Status} / {FirstNonEmpty(projection.FailureMessage, projection.FailureCode, projection.LastSourceHash)}",
                projection.UpdatedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryOperatorAuditItem>> LoadRetentionCleanupAuditAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var runsQuery = dbContext.Set<CognitiveMemoryRunRecord>()
            .AsNoTracking()
            .Where(run => run.RunKind == CognitiveMemoryRunKind.RetentionCleanup);
        if (query.ProjectId is { } projectId)
        {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId);
        }

        return (await runsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(run => run.CompletedAtUtc ?? run.StartedAtUtc)
            .Take(query.Take)
            .Select(run => new CognitiveMemoryOperatorAuditItem(
                run.Id,
                run.ProjectId,
                CognitiveMemoryOperatorAuditKind.RetentionCleanup,
                run.Id,
                CognitiveMemoryOperatorAuditSubjectKind.Run,
                ToOperatorAuditStatus(run.Status),
                run.OperationMode == CognitiveMemoryOperationMode.Observe
                    ? "Retention cleanup dry-run"
                    : "Retention cleanup executed",
                $"{run.OperationMode} / {run.Status} / scopes {FirstNonEmpty(run.Cursor, "default")} / {FirstNonEmpty(run.FailureMessage, run.FailureCode, run.AlgorithmVersion)}",
                run.CompletedAtUtc ?? run.StartedAtUtc))
            .ToArray();
    }

    private static CognitiveMemoryOperatorAuditStatus ToOperatorAuditStatus(CognitiveMemoryMutationCommandStatus status)
        => status switch
        {
            CognitiveMemoryMutationCommandStatus.Accepted => CognitiveMemoryOperatorAuditStatus.Accepted,
            CognitiveMemoryMutationCommandStatus.Rejected => CognitiveMemoryOperatorAuditStatus.Rejected,
            CognitiveMemoryMutationCommandStatus.ReviewRequired => CognitiveMemoryOperatorAuditStatus.ReviewRequired,
            _ => CognitiveMemoryOperatorAuditStatus.Informational
        };

    private static CognitiveMemoryOperatorAuditStatus ToOperatorAuditStatus(CognitiveMemoryMutationAuditEventKind eventKind)
        => eventKind switch
        {
            CognitiveMemoryMutationAuditEventKind.Rejected => CognitiveMemoryOperatorAuditStatus.Rejected,
            CognitiveMemoryMutationAuditEventKind.ReviewRequired => CognitiveMemoryOperatorAuditStatus.ReviewRequired,
            CognitiveMemoryMutationAuditEventKind.AcceptedForHandler => CognitiveMemoryOperatorAuditStatus.Accepted,
            _ => CognitiveMemoryOperatorAuditStatus.Informational
        };

    private static CognitiveMemoryOperatorAuditStatus ToOperatorAuditStatus(
        CognitiveMemoryBeliefStateKind beliefState,
        CognitiveMemoryValidationState validationState)
        => validationState == CognitiveMemoryValidationState.Approved ||
           beliefState is CognitiveMemoryBeliefStateKind.Supported or CognitiveMemoryBeliefStateKind.Validated
            ? CognitiveMemoryOperatorAuditStatus.Supported
            : CognitiveMemoryOperatorAuditStatus.NeedsReview;

    private static CognitiveMemoryOperatorAuditStatus ToOperatorAuditStatus(CognitiveMemoryRedactionState redactionState)
        => redactionState == CognitiveMemoryRedactionState.Safe
            ? CognitiveMemoryOperatorAuditStatus.Safe
            : CognitiveMemoryOperatorAuditStatus.Restricted;

    private static CognitiveMemoryOperatorAuditStatus ToOperatorAuditStatus(
        CognitiveMemoryProjectionStatus status,
        bool rebuildRequired)
        => status switch
        {
            CognitiveMemoryProjectionStatus.Failed => CognitiveMemoryOperatorAuditStatus.Failed,
            CognitiveMemoryProjectionStatus.RebuildRequired => CognitiveMemoryOperatorAuditStatus.RebuildRequired,
            _ when rebuildRequired => CognitiveMemoryOperatorAuditStatus.RebuildRequired,
            _ => CognitiveMemoryOperatorAuditStatus.Informational
        };

    private static CognitiveMemoryOperatorAuditStatus ToOperatorAuditStatus(CognitiveMemoryRunStatus status)
        => status switch
        {
            CognitiveMemoryRunStatus.Succeeded => CognitiveMemoryOperatorAuditStatus.Succeeded,
            CognitiveMemoryRunStatus.Failed => CognitiveMemoryOperatorAuditStatus.Failed,
            CognitiveMemoryRunStatus.Blocked => CognitiveMemoryOperatorAuditStatus.Blocked,
            CognitiveMemoryRunStatus.Running => CognitiveMemoryOperatorAuditStatus.Running,
            _ => CognitiveMemoryOperatorAuditStatus.Informational
        };

    private static int CountJsonIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return 0;
        }

        return json.Count(character => character == ',') + 1;
    }
}
