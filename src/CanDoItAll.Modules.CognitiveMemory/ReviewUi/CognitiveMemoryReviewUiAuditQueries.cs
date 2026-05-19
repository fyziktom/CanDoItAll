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
        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.OperatorAudit);
        var branchLimit = page.Skip + page.PageSize;
        var items = new List<CognitiveMemoryOperatorAuditItem>(branchLimit * 4);
        items.AddRange(await LoadMutationCommandAuditAsync(dbContext, query, branchLimit, cancellationToken));
        items.AddRange(await LoadMutationEventAuditAsync(dbContext, query, branchLimit, cancellationToken));
        items.AddRange(await LoadClaimAuditAsync(dbContext, query, branchLimit, cancellationToken));
        items.AddRange(await LoadEvidenceAuditAsync(dbContext, query, branchLimit, cancellationToken));
        items.AddRange(await LoadProjectionFailureAuditAsync(dbContext, query, branchLimit, cancellationToken));
        items.AddRange(await LoadRetentionCleanupAuditAsync(dbContext, query, branchLimit, cancellationToken));

        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.AuditKind)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryOperatorAuditItem>> LoadMutationCommandAuditAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        var commandsQuery = dbContext.Set<CognitiveMemoryMutationCommandRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } projectId)
        {
            commandsQuery = commandsQuery.Where(command => command.ProjectId == projectId);
        }

        var orderedCommands = commandsQuery
            .OrderByDescending(command => command.RequiresHumanReview)
            .ThenBy(command => command.Id);
        if (!UsesSqlite(dbContext))
        {
            orderedCommands = commandsQuery
                .OrderByDescending(command => command.RequiresHumanReview)
                .ThenByDescending(command => command.UpdatedAtUtc);
        }

        var commands = await orderedCommands
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return commands
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
        int limit,
        CancellationToken cancellationToken)
    {
        var eventsQuery = dbContext.Set<CognitiveMemoryMutationAuditEventRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } projectId)
        {
            eventsQuery = eventsQuery.Where(item => item.ProjectId == projectId);
        }

        var orderedEvents = UsesSqlite(dbContext)
            ? eventsQuery
                .OrderByDescending(item => item.Sequence)
                .ThenBy(item => item.Id)
            : eventsQuery
                .OrderByDescending(item => item.CreatedAtUtc)
                .ThenByDescending(item => item.Sequence);
        var events = await orderedEvents
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return events
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
        int limit,
        CancellationToken cancellationToken)
    {
        var claimsQuery = dbContext.Set<CognitiveMemoryClaimRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } projectId)
        {
            claimsQuery = claimsQuery.Where(claim => claim.ProjectId == projectId);
        }

        var orderedClaims = claimsQuery
            .OrderByDescending(claim => claim.ValidationState != CognitiveMemoryValidationState.Approved)
            .ThenBy(claim => claim.Id);
        if (!UsesSqlite(dbContext))
        {
            orderedClaims = claimsQuery
                .OrderByDescending(claim => claim.ValidationState != CognitiveMemoryValidationState.Approved)
                .ThenByDescending(claim => claim.UpdatedAtUtc);
        }

        var claims = await orderedClaims
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return claims
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
        int limit,
        CancellationToken cancellationToken)
    {
        var anchorsQuery = dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } projectId)
        {
            anchorsQuery = anchorsQuery.Where(anchor => anchor.ProjectId == projectId);
        }

        var orderedAnchors = anchorsQuery
            .OrderByDescending(anchor => anchor.RedactionState != CognitiveMemoryRedactionState.Safe)
            .ThenBy(anchor => anchor.Id);
        if (!UsesSqlite(dbContext))
        {
            orderedAnchors = anchorsQuery
                .OrderByDescending(anchor => anchor.RedactionState != CognitiveMemoryRedactionState.Safe)
                .ThenByDescending(anchor => anchor.CreatedAtUtc);
        }

        var anchors = await orderedAnchors
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return anchors
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
        int limit,
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

        var orderedProjections = projectionsQuery
            .OrderByDescending(projection => projection.Status == CognitiveMemoryProjectionStatus.Failed)
            .ThenBy(projection => projection.Id);
        if (!UsesSqlite(dbContext))
        {
            orderedProjections = projectionsQuery
                .OrderByDescending(projection => projection.Status == CognitiveMemoryProjectionStatus.Failed)
                .ThenByDescending(projection => projection.UpdatedAtUtc);
        }

        var projections = await orderedProjections
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return projections
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
        int limit,
        CancellationToken cancellationToken)
    {
        var runsQuery = dbContext.Set<CognitiveMemoryRunRecord>()
            .AsNoTracking()
            .Where(run => run.RunKind == CognitiveMemoryRunKind.RetentionCleanup);
        if (query.ProjectId is { } projectId)
        {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId);
        }

        var orderedRuns = UsesSqlite(dbContext)
            ? runsQuery.OrderBy(run => run.Id)
            : runsQuery.OrderByDescending(run => run.CompletedAtUtc ?? run.StartedAtUtc);
        var runs = await orderedRuns
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return runs
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
