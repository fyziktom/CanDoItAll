using CanDoItAll.AgentFramework.Models;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace CanDoItAll.AgentFramework.UI.Governance;

public static class GovernancePresentationMapping {
    public const int LabelLimit = 160;
    public const int SummaryLimit = 320;
    public const int SectionRowLimit = 30;

    public static string Text(string? value, int limit = LabelLimit) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 2);
        var text = new StringBuilder(Math.Min(value.Length, limit));
        foreach (var rune in value.EnumerateRunes()) {
            if (Rune.IsControl(rune) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format) {
                continue;
            }
            var next = Rune.IsWhiteSpace(rune) ? " " : rune.ToString();
            if (next == " " && (text.Length == 0 || text[^1] == ' ')) {
                continue;
            }
            if (text.Length + next.Length >= limit) {
                text.Append('…');
                break;
            }
            text.Append(next);
        }
        return text.ToString().Trim();
    }

    public static string Timestamp(DateTimeOffset? value)
        => value?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "Not recorded";

    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Tone(GovernanceTone tone) => tone switch {
        GovernanceTone.Info => "info",
        GovernanceTone.Success => "success",
        GovernanceTone.Warning => "warning",
        GovernanceTone.Danger => "danger",
        _ => "neutral"
    };

    public static GovernanceRunPresentation Run(ExecutionRunRecord run, string agentLabel)
        => new(run.Id, run.AgentId, Text(run.Title), Text(agentLabel), run.State.ToString(), run.Outcome?.ToString(),
            RunTone(run.State, run.Outcome), Timestamp(run.UpdatedAtUtc), Text(run.ProviderName), Text(run.Model),
            Text($"{run.State} / {run.ProviderName} / {run.Model}", SummaryLimit), Text(run.SourceKind, 64),
            Reference(run.SourceId), Reference(run.ProcessRunId), Reference(run.ProcessStepId),
            !string.IsNullOrWhiteSpace(run.ProcessRunId));

    public static GovernanceDetailPresentation Detail(ExecutionRunDetail detail, string agentLabel)
        => new(Run(detail.Run, agentLabel), detail.Approvals.Count, detail.Artifacts.Count,
            detail.Checkpoints.Count, detail.ToolReceipts.Count,
            detail.Approvals.OrderByDescending(item => item.RequestedAtUtc).Take(SectionRowLimit)
                .Select(item => new GovernanceEntry(Text(item.ToolName), Text(item.ToolKind), Timestamp(item.RequestedAtUtc),
                    "Approval arguments and runtime details are omitted.", item.Status.ToString(),
                    item.Status switch {
                        ExecutionApprovalStatus.Pending => GovernanceTone.Warning,
                        ExecutionApprovalStatus.Approved => GovernanceTone.Success,
                        ExecutionApprovalStatus.Rejected => GovernanceTone.Danger,
                        _ => GovernanceTone.Neutral
                    })).ToImmutableArray(),
            detail.Artifacts.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).Take(SectionRowLimit)
                .Select(item => new GovernanceEntry(Text(item.DisplayName), Text(item.ArtifactKind), Timestamp(item.CreatedAtUtc),
                    RelativeArtifactPath(item.RelativePath))).ToImmutableArray(),
            detail.Checkpoints.OrderByDescending(item => item.CapturedAtUtc).Take(SectionRowLimit)
                .Select(item => new GovernanceEntry(Text(item.CheckpointKind), item.RunState.ToString(), Timestamp(item.CapturedAtUtc),
                    Number(item.PendingApprovalIds.Count) + " pending approval id(s)")).ToImmutableArray(),
            detail.ToolReceipts.OrderByDescending(item => item.CompletedAtUtc).Take(SectionRowLimit)
                .Select(item => new GovernanceEntry(Text(item.ToolName), Text(item.ToolFamily), Timestamp(item.CompletedAtUtc),
                    Text($"{item.RiskClass} / {item.InvocationOutcome} / {item.EffectState}", SummaryLimit))).ToImmutableArray(),
            Timeline(detail.ExecutionLog), Metrics(detail.Metrics));

    public static ImmutableArray<GovernanceEntry> Timeline(IEnumerable<ExecutionLogEntry> entries)
        => entries.OrderByDescending(item => item.CreatedAtUtc).Take(12)
            .Select(item => new GovernanceEntry(Text(item.Phase), item.State.ToString(), Timestamp(item.CreatedAtUtc),
                "Runtime phase recorded; execution message omitted.", item.State.ToString(), RunTone(item.State, null))).ToImmutableArray();

    public static ExecutionMetricsPresentation Metrics(IReadOnlyList<AgentRunMetric> metrics)
        => new(new(metrics.Count, metrics.Count(item => item.Outcome == RunOutcome.Succeeded),
            metrics.Count(item => item.Outcome == RunOutcome.Failed), metrics.Sum(item => (decimal)item.InputTokens),
            metrics.Sum(item => (decimal)item.OutputTokens), metrics.Sum(item => (decimal)item.ToolCalls)),
            metrics.OrderByDescending(item => item.CreatedAtUtc).Take(10)
                .Select(item => new GovernanceEntry(Text(item.ProviderName), item.Outcome.ToString(), Timestamp(item.CreatedAtUtc),
                    FormattableString.Invariant($"Input {item.InputTokens}, output {item.OutputTokens}, tools {item.ToolCalls}"),
                    Text(item.Model) + " / " + Number(item.DurationMs) + " ms",
                    item.Outcome == RunOutcome.Succeeded ? GovernanceTone.Success : GovernanceTone.Danger)).ToImmutableArray());

    public static string RelativeArtifactPath(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return "Artifact path not recorded";
        }
        var path = value.Replace('\\', '/');
        if (path.StartsWith('/') || path.Contains(':') || path.Contains('?') || path.Contains('#')
            || path.Split('/').Any(part => part is ".." or ".") || path.Any(char.IsControl)) {
            return "Artifact path omitted";
        }
        return Text(path, SummaryLimit);
    }

    private static string Reference(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Not recorded"
            : Guid.TryParse(value, out var id) ? id.ToString() : "External reference";

    private static GovernanceTone RunTone(ExecutionState state, RunOutcome? outcome)
        => outcome == RunOutcome.Succeeded || state == ExecutionState.Completed ? GovernanceTone.Success
            : outcome == RunOutcome.Failed || state == ExecutionState.Failed ? GovernanceTone.Danger
            : state == ExecutionState.WaitingOnTool ? GovernanceTone.Warning : GovernanceTone.Info;
}
