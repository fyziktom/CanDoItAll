using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessLiveBlockedIncidentSummaryService
{
    private const int MaxCauseLength = 260;

    public IReadOnlyList<ProcessLiveEscalationCard> BuildEscalationIncidentCards(
        IReadOnlyList<ProcessLiveEscalationCard> escalationCards,
        IReadOnlyList<ProcessRunListItem> runs,
        IReadOnlyList<ExecutionRunDetail> executionRunDetails)
    {
        if (escalationCards.Count == 0)
        {
            return [];
        }

        var runsById = runs.ToDictionary(item => item.Id);
        var childRunsByParentRunId = BuildChildRunsByParentRunId(runs);

        return escalationCards
            .Select(item => BuildEscalationIncidentProjection(item, runsById, childRunsByParentRunId, executionRunDetails))
            .GroupBy(item => item.GroupKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(item => ResolveRunDepth(item.Card.RunId, runsById))
                .ThenByDescending(item => item.Card.UpdatedAtUtc)
                .ThenByDescending(item => item.Card.CreatedAtUtc)
                .First()
                .Card with
                {
                    Key = $"escalation-incident:{group.Key}"
                })
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.RunName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlySet<Guid> ResolveBlockedRunIdsCoveredByEscalations(
        IReadOnlyList<ProcessLiveEscalationCard> escalationCards,
        IReadOnlyList<ProcessRunListItem> runs)
    {
        if (escalationCards.Count == 0 || runs.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var runsById = runs.ToDictionary(item => item.Id);
        var childRunsByParentRunId = BuildChildRunsByParentRunId(runs);
        var coveredRunIds = new HashSet<Guid>();
        foreach (var escalation in escalationCards)
        {
            if (!runsById.TryGetValue(escalation.RunId, out var run))
            {
                continue;
            }

            AddBlockedRunAndDescendants(run, childRunsByParentRunId, coveredRunIds);
        }

        return coveredRunIds;
    }

    public string BuildRunHealthSummary(
        ProcessRunListItem run,
        ProcessActiveRunSummaryViewModel? activeSummary,
        IReadOnlyList<ProcessLiveEscalationCard> runEscalations,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessRunListItem>> childRunsByParentRunId,
        IReadOnlyList<ExecutionRunDetail> executionRunDetails)
    {
        if (run.Status != ProcessRunStatus.Blocked)
        {
            return activeSummary?.HealthSummary ?? string.Empty;
        }

        var latestEscalation = runEscalations
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
        if (latestEscalation is not null && !string.IsNullOrWhiteSpace(latestEscalation.Reason))
        {
            return latestEscalation.Reason;
        }

        var leafRun = FindDeepestBlockedDescendant(run, childRunsByParentRunId) ?? run;
        var cause = ResolveLatestExecutionCause(leafRun, executionRunDetails);
        if (string.IsNullOrWhiteSpace(cause) && run.BlockedStepCount == 0)
        {
            cause = "No step is currently marked blocked; the run likely selected an exception branch without a routed recovery step.";
        }

        if (string.IsNullOrWhiteSpace(cause))
        {
            cause = run.BlockedStepCount == 1
                ? "One step is blocked."
                : $"{run.BlockedStepCount} steps are blocked.";
        }

        return $"Cause: {EnsureSentence(cause)} Next action: {BuildRunActionProposal(run, leafRun)}";
    }

    public string BuildRunEventSummary(
        ProcessRunListItem run,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessRunListItem>> childRunsByParentRunId,
        IReadOnlyList<ExecutionRunDetail> executionRunDetails)
    {
        if (run.Status != ProcessRunStatus.Blocked)
        {
            return run.Status switch
            {
                ProcessRunStatus.Completed => $"{run.Name} completed {run.CompletedStepCount}/{run.TotalStepCount} step(s).",
                ProcessRunStatus.Failed => $"{run.Name} failed after {run.CompletedStepCount}/{run.TotalStepCount} completed step(s).",
                _ => $"{run.Name} changed to {run.Status}."
            };
        }

        var leafRun = FindDeepestBlockedDescendant(run, childRunsByParentRunId) ?? run;
        var cause = ResolveLatestExecutionCause(leafRun, executionRunDetails);
        if (string.IsNullOrWhiteSpace(cause))
        {
            cause = run.BlockedStepCount == 0
                ? "The run is blocked without a step-level block marker."
                : $"{run.BlockedStepCount} blocked step(s) need attention.";
        }

        return $"{run.Name} is blocked. Cause: {EnsureSentence(cause)} Next action: {BuildRunActionProposal(run, leafRun)}";
    }

    public bool ShouldSuppressRunEvent(
        ProcessRunListItem run,
        IReadOnlySet<Guid> visibleRunIds,
        IReadOnlySet<Guid> escalationRunIds)
    {
        return run.Status == ProcessRunStatus.Blocked &&
               (visibleRunIds.Contains(run.Id) || escalationRunIds.Contains(run.Id));
    }

    public static IReadOnlyDictionary<Guid, IReadOnlyList<ProcessRunListItem>> BuildChildRunsByParentRunId(
        IReadOnlyList<ProcessRunListItem> runs)
    {
        return runs
            .Where(item => item.ParentRunId.HasValue)
            .GroupBy(item => item.ParentRunId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessRunListItem>)group
                    .OrderByDescending(item => item.UpdatedAtUtc)
                    .ToArray());
    }

    private static string BuildEscalationIncidentSummary(
        ProcessLiveEscalationCard escalation,
        IReadOnlyDictionary<Guid, ProcessRunListItem> runsById,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessRunListItem>> childRunsByParentRunId,
        IReadOnlyList<ExecutionRunDetail> executionRunDetails)
    {
        var cause = NormalizeDiagnostic(escalation.Reason);
        var isPropagatedSubprocessBlock = IsSubprocessBlockPropagation(cause);

        if (runsById.TryGetValue(escalation.RunId, out var run))
        {
            var leafRun = FindDeepestBlockedDescendant(run, childRunsByParentRunId);
            if (leafRun is not null)
            {
                var leafCause = ResolveLatestExecutionCause(leafRun, executionRunDetails);
                if (!string.IsNullOrWhiteSpace(leafCause))
                {
                    cause = $"Blocked by subprocess '{leafRun.Name}'. {leafCause}";
                    isPropagatedSubprocessBlock = true;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(cause))
        {
            cause = string.IsNullOrWhiteSpace(escalation.StepTitle)
                ? "Run-level escalation is open."
                : $"Step '{escalation.StepTitle}' needs operator review.";
        }

        return $"Cause: {EnsureSentence(cause)} Next action: {BuildEscalationActionProposal(escalation, cause, isPropagatedSubprocessBlock)}";
    }

    private static EscalationIncidentProjection BuildEscalationIncidentProjection(
        ProcessLiveEscalationCard escalation,
        IReadOnlyDictionary<Guid, ProcessRunListItem> runsById,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessRunListItem>> childRunsByParentRunId,
        IReadOnlyList<ExecutionRunDetail> executionRunDetails)
    {
        ProcessRunListItem? leafRun = null;
        var reason = NormalizeDiagnostic(escalation.Reason);
        if (IsSubprocessBlockPropagation(reason) &&
            runsById.TryGetValue(escalation.RunId, out var run))
        {
            leafRun = FindDeepestBlockedDescendant(run, childRunsByParentRunId);
        }

        var projectedCard = escalation with
        {
            Title = BuildEscalationIncidentTitle(escalation),
            Reason = BuildEscalationIncidentSummary(escalation, runsById, childRunsByParentRunId, executionRunDetails)
        };
        var groupKey = leafRun is null
            ? BuildEscalationIncidentGroupKey(escalation)
            : BuildSubprocessEscalationIncidentGroupKey(leafRun, projectedCard.Reason);

        return new EscalationIncidentProjection(groupKey, projectedCard);
    }

    private static string BuildEscalationIncidentTitle(ProcessLiveEscalationCard escalation)
    {
        var reason = NormalizeDiagnostic(escalation.Reason);
        if (IsSubprocessBlockPropagation(reason))
        {
            return "Blocked subprocess needs attention";
        }

        if (ContainsAny(reason, "tool", "capability") &&
            ContainsAny(reason, "unavailable", "not available", "missing"))
        {
            return "Required tool is unavailable";
        }

        return string.IsNullOrWhiteSpace(escalation.Title)
            ? "Blocked process needs attention"
            : escalation.Title;
    }

    private static string BuildEscalationActionProposal(
        ProcessLiveEscalationCard escalation,
        string cause,
        bool isPropagatedSubprocessBlock)
    {
        if (isPropagatedSubprocessBlock)
        {
            return "Open the blocked child run, request targeted rework at the leaf cause, then let the parent subprocess status refresh.";
        }

        if (ContainsAny(cause, "tool") &&
            ContainsAny(cause, "unavailable", "not available", "missing"))
        {
            return "Correct the step tool requirement or run with a host that exposes the required tool, then request targeted rework.";
        }

        var primaryAction = ProcessLiveEscalationActionPolicy.ResolvePrimaryAction(escalation);
        return primaryAction.Kind switch
        {
            ProcessLiveEscalationActionKind.DecideApproval => "Review the pending approval and approve or reject it.",
            ProcessLiveEscalationActionKind.RequestRework => "Request targeted rework with the cause above, then resolve the escalation after the run advances.",
            ProcessLiveEscalationActionKind.Resolve => "Resolve the escalation only after the blocked condition is no longer present.",
            _ => "Message the process manager for a concrete unblock directive."
        };
    }

    private static string BuildRunActionProposal(ProcessRunListItem run, ProcessRunListItem leafRun)
    {
        if (leafRun.Id != run.Id)
        {
            return "open the leaf blocked child run and request rework there.";
        }

        if (run.BlockedStepCount == 0)
        {
            return "open run details and request rework for the step that selected the blocking branch.";
        }

        return "open run details and request targeted rework for the blocked step.";
    }

    private static ProcessRunListItem? FindDeepestBlockedDescendant(
        ProcessRunListItem run,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessRunListItem>> childRunsByParentRunId)
    {
        if (!childRunsByParentRunId.TryGetValue(run.Id, out var children))
        {
            return null;
        }

        ProcessRunListItem? best = null;
        var stack = new Stack<ProcessRunListItem>(children);
        while (stack.Count > 0)
        {
            var candidate = stack.Pop();
            if (candidate.Status == ProcessRunStatus.Blocked &&
                (best is null ||
                 candidate.HierarchyDepth > best.HierarchyDepth ||
                 candidate.HierarchyDepth == best.HierarchyDepth && candidate.UpdatedAtUtc > best.UpdatedAtUtc))
            {
                best = candidate;
            }

            if (childRunsByParentRunId.TryGetValue(candidate.Id, out var grandchildren))
            {
                foreach (var child in grandchildren)
                {
                    stack.Push(child);
                }
            }
        }

        return best;
    }

    private static void AddBlockedRunAndDescendants(
        ProcessRunListItem run,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessRunListItem>> childRunsByParentRunId,
        HashSet<Guid> coveredRunIds)
    {
        if (run.Status == ProcessRunStatus.Blocked)
        {
            coveredRunIds.Add(run.Id);
        }

        if (!childRunsByParentRunId.TryGetValue(run.Id, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            AddBlockedRunAndDescendants(child, childRunsByParentRunId, coveredRunIds);
        }
    }

    private static int ResolveRunDepth(
        Guid runId,
        IReadOnlyDictionary<Guid, ProcessRunListItem> runsById)
    {
        return runsById.TryGetValue(runId, out var run)
            ? run.HierarchyDepth
            : int.MaxValue;
    }

    private static string ResolveLatestExecutionCause(
        ProcessRunListItem run,
        IReadOnlyList<ExecutionRunDetail> executionRunDetails)
    {
        var runId = run.Id.ToString("D");
        return executionRunDetails
            .Where(item => string.Equals(item.Run.ProcessRunId, runId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Run.CompletedAtUtc ?? item.Run.UpdatedAtUtc)
            .ThenByDescending(item => item.Run.CreatedAtUtc)
            .Select(item => NormalizeDiagnostic(item.Run.ResultSummary))
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty;
    }

    private static string BuildEscalationIncidentGroupKey(ProcessLiveEscalationCard escalation)
    {
        var sourceKey = escalation.StepRunId?.ToString("N") ?? "run";
        var fingerprint = BuildStableHash(
            $"{NormalizeKeyPart(escalation.Title)}|{NormalizeKeyPart(escalation.Reason)}");
        return $"{escalation.RunId:N}:{sourceKey}:{escalation.Kind}:{fingerprint}";
    }

    private static string BuildSubprocessEscalationIncidentGroupKey(
        ProcessRunListItem leafRun,
        string projectedReason)
    {
        return $"subprocess-leaf:{leafRun.Id:N}:{BuildStableHash(NormalizeKeyPart(projectedReason))}";
    }

    private static string BuildStableHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..16];
    }

    private static string NormalizeKeyPart(string value)
    {
        return CollapseWhitespace(value).ToUpperInvariant();
    }

    private static bool IsSubprocessBlockPropagation(string value)
    {
        return value.StartsWith("Subprocess run ", StringComparison.OrdinalIgnoreCase) &&
               value.Contains(" is blocked", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDiagnostic(string value)
    {
        var collapsed = CollapseWhitespace(value);
        if (string.IsNullOrWhiteSpace(collapsed))
        {
            return string.Empty;
        }

        if (collapsed.StartsWith("{", StringComparison.Ordinal))
        {
            var parsed = TryExtractJsonDiagnostic(collapsed);
            if (!string.IsNullOrWhiteSpace(parsed))
            {
                collapsed = parsed;
            }
        }

        return collapsed.Length <= MaxCauseLength
            ? collapsed
            : $"{collapsed[..MaxCauseLength].TrimEnd()}...";
    }

    private static string TryExtractJsonDiagnostic(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            return TryGetJsonString(root, "reason") ??
                   TryGetJsonString(root, "Reason") ??
                   TryGetJsonString(root, "summary") ??
                   TryGetJsonString(root, "Summary") ??
                   TryGetJsonString(root, "error") ??
                   TryGetJsonString(root, "Error") ??
                   TryGetJsonString(root, "message") ??
                   TryGetJsonString(root, "Message") ??
                   string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string? TryGetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? CollapseWhitespace(property.GetString() ?? string.Empty)
            : null;
    }

    private static string CollapseWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }

    private static string EnsureSentence(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith(".", StringComparison.Ordinal) ||
               trimmed.EndsWith("!", StringComparison.Ordinal) ||
               trimmed.EndsWith("?", StringComparison.Ordinal)
            ? trimmed
            : $"{trimmed}.";
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record EscalationIncidentProjection(
        string GroupKey,
        ProcessLiveEscalationCard Card);
}
