using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Collaboration;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessRuntimeReadQueryService
{
    private static IReadOnlyList<ProcessStepDependencyViewModel> BuildRuntimeDependencies(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId)
    {
        return ProcessStepDependencyCollection.BuildRuntimeDependencies(stepDefinition.Id, dependenciesByStepId);
    }

    private static IReadOnlyList<ProcessStepRunResponsibilityPortViewModel> BuildRuntimeResponsibilityPorts(
        Guid stepDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepRoleAssignmentRequirement>> assignmentsByStepId)
    {
        if (!assignmentsByStepId.TryGetValue(stepDefinitionId, out var assignments) || assignments.Count == 0)
        {
            return [];
        }

        var orderedKinds = new[]
        {
            ProcessResponsibilityKind.Responsible,
            ProcessResponsibilityKind.Reviewer,
            ProcessResponsibilityKind.Approver,
            ProcessResponsibilityKind.Backup
        };

        return orderedKinds
            .Select(
                responsibilityKind =>
                {
                    var matchingAssignments = assignments
                        .Where(item => item.ResponsibilityKind == responsibilityKind)
                        .ToList();
                    return new ProcessStepRunResponsibilityPortViewModel(
                        responsibilityKind,
                        matchingAssignments.Any(item => item.IsRequired),
                        matchingAssignments.Count);
                })
            .Where(item => item.AssignmentCount > 0)
            .ToList();
    }

    private static int Average(IEnumerable<int> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0
            ? 0
            : (int)Math.Round(materialized.Average(), MidpointRounding.AwayFromZero);
    }

    private static async Task<IReadOnlyList<ProcessDecisionViewModel>> ListDecisionRecordsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessDecisionRecord>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessDecisionViewModel(
                item.Id,
                item.DecisionKind,
                item.Outcome,
                item.Title,
                item.Reason,
                item.BranchOutcomeTitle,
                item.DecidedBy,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessArtifactViewModel>> ListArtifactsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessArtifactViewModel(
                item.Id,
                item.ArtifactKind,
                item.Title,
                item.TrustStatus,
                item.SensitivityLevel,
                item.ProvenanceSummary,
                item.AllowedFutureUsageSummary,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessRunAssignmentViewModel>> ListAssignmentsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.Set<ProcessRunAssignment>()
            .Where(item => item.ProcessRunId == runId)
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return [];
        }

        var roleIds = assignments
            .Select(item => item.RoleRequirementId)
            .Distinct()
            .ToList();
        var roleDisplayNames = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => roleIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);

        return assignments
            .OrderBy(item => roleDisplayNames.GetValueOrDefault(item.RoleRequirementId, item.DisplayName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(
                item => new ProcessRunAssignmentViewModel(
                    item.Id,
                    item.RoleRequirementId,
                    item.StepDefinitionId,
                    item.PartyId,
                    item.DisplayName,
                    item.ExecutorKind,
                    item.BindingReason,
                    item.SourceRegistryKey,
                    item.SnapshotSummary,
                    item.IsFallback,
                    item.IsCapabilityGap,
                    item.AllowsDirectMessaging)
                {
                    RoleDisplayName = roleDisplayNames.GetValueOrDefault(item.RoleRequirementId, string.Empty)
                })
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessWorkBriefViewModel>> ListWorkBriefsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessWorkBrief>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessWorkBriefViewModel(
                item.Id,
                item.StepRunId,
                item.Title,
                item.WorkBriefText,
                item.HandoffSummary,
                item.AssignmentReason,
                item.ExpectedOutcome,
                item.EvidenceExpectationSummary,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessConformanceObservationViewModel>> ListConformanceObservationsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessConformanceObservation>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessConformanceObservationViewModel(
                item.Id,
                item.StepRunId,
                item.Severity,
                item.Category,
                item.Observation,
                item.DeviationReason,
                item.IsSafeNonAction,
                item.ContainsSensitiveAssessment,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessDirectMessageThreadViewModel>> ListDirectMessageThreadsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var threads = await dbContext.Set<CollaborationThreadRecord>()
            .Where(item => item.ContextKind == CollaborationContextKind.ProcessRun && item.ContextId == runId)
            .Select(item => new ProcessDirectMessageThreadProjection(
                item.Id,
                item.Subject,
                item.LastActivityAtUtc))
            .ToListAsync(cancellationToken);
        if (threads.Count == 0)
        {
            return [];
        }

        var threadIds = threads
            .Select(item => item.ThreadId)
            .ToArray();
        var inboxItems = await dbContext.Set<CollaborationInboxItemRecord>()
            .Where(item => threadIds.Contains(item.ThreadId))
            .Select(item => new ProcessDirectMessageInboxProjection(
                item.ThreadId,
                item.Route,
                item.UnreadCount))
            .ToListAsync(cancellationToken);
        var participants = await dbContext.Set<CollaborationParticipantRecord>()
            .Where(item => threadIds.Contains(item.ThreadId) && item.ParticipantKind == CollaborationParticipantKind.Role)
            .Select(item => new ProcessDirectMessageParticipantProjection(
                item.ThreadId,
                item.DisplayName))
            .ToListAsync(cancellationToken);
        var messages = await dbContext.Set<CollaborationMessageRecord>()
            .Where(item => threadIds.Contains(item.ThreadId))
            .Select(item => new ProcessDirectMessageMessageProjection(
                item.ThreadId,
                item.Id,
                item.Kind,
                item.AuthorName,
                item.Body,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var inboxByThreadId = inboxItems.ToDictionary(item => item.ThreadId);
        var participantsByThreadId = participants
            .GroupBy(item => item.ThreadId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.DisplayName)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        var messagesByThreadId = messages
            .GroupBy(item => item.ThreadId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => new ProcessDirectMessageEntryViewModel(
                        item.MessageId,
                        item.MessageKind,
                        item.AuthorName,
                        item.Body,
                        item.CreatedAtUtc))
                    .ToList());

        return threads
            .OrderByDescending(item => item.LastActivityAtUtc)
            .Where(item => participantsByThreadId.ContainsKey(item.ThreadId))
            .Select(item =>
            {
                var roleLabels = participantsByThreadId[item.ThreadId];
                var threadMessages = (messagesByThreadId.GetValueOrDefault(item.ThreadId) ?? [])
                    .OrderBy(message => message.CreatedAtUtc)
                    .ToList();
                inboxByThreadId.TryGetValue(item.ThreadId, out var inbox);
                return new ProcessDirectMessageThreadViewModel(
                    item.ThreadId,
                    item.Subject,
                    inbox?.Route ?? string.Empty,
                    roleLabels.Count == 0 ? "Process roles" : string.Join(" / ", roleLabels),
                    threadMessages.Count,
                    inbox?.UnreadCount ?? 0,
                    item.LastActivityAtUtc,
                    threadMessages);
            })
            .ToList();
    }

    private sealed record ProcessRunListProjection(
        Guid Id,
        Guid ProcessDefinitionId,
        Guid ProcessDefinitionVersionId,
        Guid? ProjectId,
        string Name,
        ProcessRunStatus Status,
        ProcessOperatingMode OperatingMode,
        decimal EstimatedCost,
        decimal ActualCost,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ProcessRunStepSummaryProjection(
        Guid ProcessRunId,
        int CompletedCount,
        int TotalCount,
        int BlockedCount,
        int CapabilityGapCount)
    {
        public static ProcessRunStepSummaryProjection Empty(Guid runId)
        {
            return new ProcessRunStepSummaryProjection(runId, 0, 0, 0, 0);
        }
    }

    private sealed record ProcessArtifactOutputProjection(
        Guid StepDefinitionId,
        ProcessStepRunArtifactPortViewModel ArtifactOutput);

    private sealed record ProcessStepArtifactInputCountProjection(Guid StepDefinitionId, int Count);

    private sealed record ProcessBranchOutcomeProjection(
        Guid StepDefinitionId,
        ProcessStepBranchOutcomeOptionViewModel BranchOutcome);

    private sealed record ProcessAnalyticsRunProjection(
        Guid Id,
        ProcessRunStatus Status,
        decimal EstimatedCost,
        decimal ActualCost);

    private sealed record ProcessStepAnalyticsProjection(
        int WaitMinutes,
        int TouchMinutes,
        int BlockedMinutes,
        ProcessCapabilityGapSeverity CapabilityGapSeverity);

    private sealed record ProcessDirectMessageThreadProjection(
        Guid ThreadId,
        string Subject,
        DateTimeOffset LastActivityAtUtc);

    private sealed record ProcessDirectMessageInboxProjection(
        Guid ThreadId,
        string Route,
        int UnreadCount);

    private sealed record ProcessDirectMessageParticipantProjection(
        Guid ThreadId,
        string DisplayName);

    private sealed record ProcessDirectMessageMessageProjection(
        Guid ThreadId,
        Guid MessageId,
        CollaborationMessageKind MessageKind,
        string AuthorName,
        string Body,
        DateTimeOffset CreatedAtUtc);
}
