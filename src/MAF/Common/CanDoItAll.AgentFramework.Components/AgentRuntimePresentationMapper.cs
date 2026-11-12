using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.AgentFramework.UI.Governance;
using CanDoItAll.AgentFramework.UI.Runtime;

namespace CanDoItAll.AgentFramework.Components;

internal static class AgentRuntimePresentationMapper {
    public static AgentRuntimeDetailsState Details(ExecutionRunRecord? run, IReadOnlyList<ExecutionLogEntry> entries,
        IReadOnlyList<AgentRunMetric> metrics, string state, string tone) {
        var evidence = run?.EntryAgentRequestCompatibilityEvidence;
        return new(run?.Id, Text(run?.Title), Text(string.IsNullOrWhiteSpace(state) ? run?.State.ToString() ?? "Idle" : state), tone,
            run?.Outcome?.ToString(), GovernancePresentationMapping.Timestamp(run?.UpdatedAtUtc),
            Text($"{run?.ProviderName} / {run?.Model}"),
            run?.FailureProviderProfileId is not null ? Text($"{run.FailureProviderName} / {run.FailureModel}") : null,
            evidence is null ? null : new(evidence.Transport.ToString(), evidence.Disposition.ToString(),
                evidence.RequestedEffort?.ToString() ?? "provider default", evidence.EffectiveEffort?.ToString() ?? "provider default",
                Text(evidence.RequestedModel), Text(evidence.EffectiveModel), evidence.Adjustment.ToString()),
            GovernancePresentationMapping.Timeline(entries.Select(entry => entry with { Phase = Text(entry.Phase, 160) })),
            GovernancePresentationMapping.Metrics(metrics.Select(metric => metric with { ProviderName = Text(metric.ProviderName), Model = Text(metric.Model) }).ToArray()));
    }

    public static AgentExecutionLogState Log(ExecutionRunRecord? run, IReadOnlyList<ExecutionLogEntry> entries, Guid? highlightEntryId) {
        var ordered = entries.OrderBy(entry => entry.CreatedAtUtc).ToArray();
        var latest = ordered.LastOrDefault()?.State ?? ExecutionState.Idle;
        var duration = ordered.Length == 0 ? TimeSpan.Zero
            : (run?.CompletedAtUtc ?? ordered[^1].CreatedAtUtc) - (run?.StartedAtUtc ?? run?.CreatedAtUtc ?? ordered[0].CreatedAtUtc);
        return new(string.IsNullOrWhiteSpace(run?.Title) ? "Agent execution" : Text(run.Title),
            run is not null ? Text($"{run.Model} / {run.SourceKind} / revision {run.Revision}")
                : ordered.Length == 0 ? string.Empty : $"Execution log from {ChatPresentationTime.Format(ordered[0].CreatedAtUtc)}.",
            run is not null ? Text(run.ProviderName) : null, latest.ToString(), Tone(latest), Duration(duration), highlightEntryId,
            ordered.Select(entry => new AgentExecutionStepPresentation(entry.Id, entry.ExecutionRunId, entry.State.ToString(), Tone(entry.State),
                Text(entry.Phase, 240), ChatPresentationTime.Format(entry.CreatedAtUtc), Text(entry.Message))).ToImmutableArray());
    }

    private static string Text(string? value, int limit = 4096) => AgentExecutionDisplayText.Format(value, limit);

    private static string Duration(TimeSpan duration) => duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}h {duration.Minutes:D2}m"
        : duration.TotalMinutes >= 1 ? $"{(int)duration.TotalMinutes}m {duration.Seconds:D2}s"
        : $"{Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))}s";

    private static string Tone(ExecutionState state) => state switch {
        ExecutionState.Completed => "success",
        ExecutionState.WaitingOnTool => "warning",
        ExecutionState.Failed => "danger",
        ExecutionState.Running or ExecutionState.Preparing or ExecutionState.Persisting => "info",
        _ => "neutral"
    };
}
