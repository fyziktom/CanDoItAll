using System.Text.Json;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

internal static class ProcessRuntimeOperatorActionDiagnostics
{
    private const int MaxDiagnosticTextLength = 900;
    private const int MaxDiagnosticItemLength = 240;

    public static StepExecutionDiagnostic? Create(ProcessExecutionObservation? observation)
    {
        if (observation is null)
        {
            return null;
        }

        var failedTools = BuildFailedToolDiagnostics(observation);
        var lastError = TruncateDiagnosticText(observation.LastError);
        var rawSummary = observation.ResultSummary.Trim();
        if (rawSummary.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(rawSummary);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var root = document.RootElement;
                    return new StepExecutionDiagnostic(
                        observation.ExecutionRunId,
                        observation.ProviderName,
                        observation.Model,
                        observation.State,
                        observation.Outcome,
                        ReadJsonString(root, "status"),
                        ReadJsonString(root, "branchOutcomeKey"),
                        ReadJsonString(root, "branchOutcomeTitle"),
                        TruncateDiagnosticText(FirstNonEmpty(
                            ReadJsonString(root, "reason"),
                            ReadJsonString(root, "humanReadableSummaryMarkdown"))),
                        ReadJsonStringArray(root, "nextActions", take: 3),
                        ReadJsonStringArray(root, "evidenceRefs", take: 5),
                        lastError,
                        failedTools);
                }
            }
            catch (JsonException)
            {
            }
        }

        if (string.IsNullOrWhiteSpace(rawSummary) &&
            string.IsNullOrWhiteSpace(lastError) &&
            failedTools.Count == 0)
        {
            return null;
        }

        return new StepExecutionDiagnostic(
            observation.ExecutionRunId,
            observation.ProviderName,
            observation.Model,
            observation.State,
            observation.Outcome,
            string.Empty,
            string.Empty,
            string.Empty,
            TruncateDiagnosticText(rawSummary),
            [],
            [],
            lastError,
            failedTools);
    }

    public static string BuildExecutionSummary(StepExecutionDiagnostic? diagnostic)
    {
        if (diagnostic is null)
        {
            return string.Empty;
        }

        var details = new List<string>();
        var status = FirstNonEmpty(diagnostic.Status, diagnostic.ExecutionOutcome, diagnostic.ExecutionState);
        if (!string.IsNullOrWhiteSpace(status))
        {
            details.Add($"status {status}");
        }

        var branch = FormatBranchOutcome(diagnostic);
        if (!string.IsNullOrWhiteSpace(branch))
        {
            details.Add($"branch {branch}");
        }

        if (!string.IsNullOrWhiteSpace(diagnostic.Reason))
        {
            details.Add($"reason: {diagnostic.Reason}");
        }

        if (!string.IsNullOrWhiteSpace(diagnostic.LastError))
        {
            details.Add($"last error: {diagnostic.LastError}");
        }

        var failedToolSummary = BuildFailedToolSummary(diagnostic.FailedTools);
        if (!string.IsNullOrWhiteSpace(failedToolSummary))
        {
            details.Add($"failed tools: {failedToolSummary}");
        }

        if (details.Count == 0)
        {
            return $"AgentFramework execution run {diagnostic.ExecutionRunId:D} has no persisted result reason.";
        }

        return $"AgentFramework result {diagnostic.ExecutionRunId:D}: {string.Join("; ", details)}.";
    }

    public static string BuildPriorNextActions(StepExecutionDiagnostic? diagnostic)
    {
        if (diagnostic is null || diagnostic.NextActions.Count == 0)
        {
            return string.Empty;
        }

        var actions = diagnostic.NextActions
            .Take(2)
            .Select(action => TruncateDiagnosticText(action, MaxDiagnosticItemLength))
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .ToArray();
        return actions.Length == 0
            ? string.Empty
            : $" Prior agent next action(s): {string.Join(" ", actions)}";
    }

    public static string BuildFailedToolInstruction(StepExecutionDiagnostic? diagnostic)
    {
        if (diagnostic is null || diagnostic.FailedTools.Count == 0)
        {
            return string.Empty;
        }

        var failedToolSummary = BuildFailedToolSummary(diagnostic.FailedTools);
        return string.IsNullOrWhiteSpace(failedToolSummary)
            ? string.Empty
            : $" Previous failed tool receipt(s): {failedToolSummary}. Inspect the listed stdout/stderr or receipt artifacts before editing or rerunning; if the prior command target was wrong, rerun the validation command with the corrected target and cite the fresh receipt.";
    }

    private static IReadOnlyList<StepToolDiagnostic> BuildFailedToolDiagnostics(ProcessExecutionObservation observation)
    {
        return observation.RecentTools
            .Where(IsFailedToolObservation)
            .OrderByDescending(tool => tool.CompletedAtUtc)
            .Take(4)
            .OrderBy(tool => tool.StartedAtUtc)
            .Select(tool => new StepToolDiagnostic(
                tool.ToolName,
                TruncateDiagnosticText(tool.RequestSummary, MaxDiagnosticItemLength),
                TruncateDiagnosticText(tool.ExitSummary, MaxDiagnosticItemLength),
                BuildToolDiagnosticArtifactRefs(observation, tool.ToolName)))
            .ToArray();
    }

    private static IReadOnlyList<string> BuildToolDiagnosticArtifactRefs(
        ProcessExecutionObservation observation,
        string toolName)
    {
        return observation.Artifacts
            .Where(artifact =>
                string.Equals(artifact.ProducedBy, toolName, StringComparison.OrdinalIgnoreCase) &&
                IsDiagnosticToolArtifact(artifact))
            .OrderBy(artifact => ResolveDiagnosticArtifactPriority(artifact.DisplayName))
            .ThenBy(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(artifact => TruncateDiagnosticText(
                $"{artifact.DisplayName}: {artifact.RelativePath}",
                MaxDiagnosticItemLength))
            .Take(4)
            .ToArray();
    }

    private static bool IsFailedToolObservation(ProcessExecutionToolObservation tool)
    {
        var exitSummary = tool.ExitSummary.Trim();
        return exitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
               exitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase) ||
               exitSummary.StartsWith("TimedOut", StringComparison.OrdinalIgnoreCase) ||
               exitSummary.Contains("exit 1", StringComparison.OrdinalIgnoreCase) ||
               exitSummary.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
               exitSummary.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
               exitSummary.Contains("timed out", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDiagnosticToolArtifact(ProcessExecutionArtifactObservation artifact)
        => artifact.DisplayName.Contains("stdout", StringComparison.OrdinalIgnoreCase) ||
           artifact.DisplayName.Contains("stderr", StringComparison.OrdinalIgnoreCase) ||
           artifact.DisplayName.Contains("receipt", StringComparison.OrdinalIgnoreCase);

    private static int ResolveDiagnosticArtifactPriority(string displayName)
    {
        if (displayName.Contains("stderr", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (displayName.Contains("stdout", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static string BuildFailedToolSummary(IReadOnlyList<StepToolDiagnostic> failedTools)
    {
        var summaries = failedTools
            .Take(3)
            .Select(tool =>
            {
                var refs = tool.DiagnosticRefs.Count == 0
                    ? string.Empty
                    : $"; diagnostics: {string.Join(", ", tool.DiagnosticRefs.Take(2))}";
                return TruncateDiagnosticText(
                    $"{tool.ToolName} {tool.ExitSummary} for {tool.RequestSummary}{refs}",
                    MaxDiagnosticItemLength);
            })
            .Where(summary => !string.IsNullOrWhiteSpace(summary))
            .ToArray();
        return summaries.Length == 0
            ? string.Empty
            : string.Join(" | ", summaries);
    }

    private static string FormatBranchOutcome(StepExecutionDiagnostic diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic.BranchOutcomeKey))
        {
            return diagnostic.BranchOutcomeTitle;
        }

        if (string.IsNullOrWhiteSpace(diagnostic.BranchOutcomeTitle) ||
            string.Equals(diagnostic.BranchOutcomeKey, diagnostic.BranchOutcomeTitle, StringComparison.OrdinalIgnoreCase))
        {
            return diagnostic.BranchOutcomeKey;
        }

        return $"{diagnostic.BranchOutcomeKey} ({diagnostic.BranchOutcomeTitle})";
    }

    private static string ReadJsonString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static IReadOnlyList<string> ReadJsonStringArray(JsonElement root, string propertyName, int take)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                continue;
            }

            values.Add(TruncateDiagnosticText(item.GetString()!, MaxDiagnosticItemLength));
            if (values.Count == take)
            {
                break;
            }
        }

        return values;
    }

    private static string TruncateDiagnosticText(
        string value,
        int maxLength = MaxDiagnosticTextLength)
    {
        var normalized = string.Join(
            " ",
            value
                .ReplaceLineEndings(" ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "...";
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

internal sealed record StepExecutionDiagnostic(
    Guid ExecutionRunId,
    string ProviderName,
    string Model,
    string ExecutionState,
    string ExecutionOutcome,
    string Status,
    string BranchOutcomeKey,
    string BranchOutcomeTitle,
    string Reason,
    IReadOnlyList<string> NextActions,
    IReadOnlyList<string> EvidenceRefs,
    string LastError,
    IReadOnlyList<StepToolDiagnostic> FailedTools)
{
    public bool HasConcreteFailure =>
        !string.IsNullOrWhiteSpace(LastError) ||
        FailedTools.Count > 0;
}

internal sealed record StepToolDiagnostic(
    string ToolName,
    string RequestSummary,
    string ExitSummary,
    IReadOnlyList<string> DiagnosticRefs);
