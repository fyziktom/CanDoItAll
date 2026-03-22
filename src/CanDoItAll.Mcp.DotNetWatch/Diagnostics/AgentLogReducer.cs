using System.Text.RegularExpressions;
using CanDoItAll.Mcp.Core.Observability;

namespace CanDoItAll.Mcp.DotNetWatch.Diagnostics;

internal enum LogReductionScenario
{
    App,
    Operation
}

internal sealed record ReducedLogData(
    IReadOnlyList<LogEntry> Entries,
    long NextCursor,
    bool Truncated,
    int TotalAvailableAfterCursor,
    LogFilterSummaryData FilterSummary);

public sealed class AgentLogReducer
{
    private enum ContinuationKind
    {
        None,
        HttpClient,
        EntityFramework,
        AspNetRequest,
        DiagnosticTrace
    }

    private static readonly Regex WarningCodeRegex = new(@":\s*warning\s+(?<code>[A-Z]{2,}\d+):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal ReducedLogData Reduce(
        IReadOnlyList<LogEntry> rawEntries,
        long startCursor,
        int limit,
        LogReductionScenario scenario,
        LogViewMode view)
    {
        var take = Math.Clamp(limit, 1, 500);
        if (view == LogViewMode.Raw)
        {
            var selected = rawEntries.Take(take).ToArray();
            return new ReducedLogData(
                selected,
                selected.Length == 0 ? startCursor : selected[^1].Sequence,
                rawEntries.Count > selected.Length,
                Math.Max(0, rawEntries.Count - selected.Length),
                new LogFilterSummaryData(LogViewMode.Raw, selected.Length, selected.Length, 0, []));
        }

        var visibleEntries = new List<LogEntry>(take);
        var notes = new List<string>();
        var warningCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var continuation = ContinuationKind.None;
        var keptVisibleCount = 0;
        var consumedRawCount = 0;
        var suppressedEntryCount = 0;
        var suppressedRestoreCount = 0;
        var suppressedArtifactCount = 0;
        var suppressedBlankCount = 0;
        var suppressedHttpTraceCount = 0;
        var suppressedEntityFrameworkCount = 0;
        var suppressedAspNetRequestCount = 0;
        var suppressedDiagnosticTraceCount = 0;
        var lastConsumedSequence = startCursor;
        var index = 0;

        while (index < rawEntries.Count && keptVisibleCount < take)
        {
            var entry = rawEntries[index++];
            consumedRawCount++;
            lastConsumedSequence = entry.Sequence;

            if (ShouldSuppress(entry, scenario, ref continuation, warningCodes, ref suppressedRestoreCount, ref suppressedArtifactCount, ref suppressedBlankCount, ref suppressedHttpTraceCount, ref suppressedEntityFrameworkCount, ref suppressedAspNetRequestCount, ref suppressedDiagnosticTraceCount))
            {
                suppressedEntryCount++;
                continue;
            }

            visibleEntries.Add(entry);
            keptVisibleCount++;
        }

        var remainingVisibleCount = 0;
        while (index < rawEntries.Count)
        {
            var entry = rawEntries[index++];
            if (!ShouldSuppress(entry, scenario, ref continuation, warningCodes: null, ref UnsafeIgnore, ref UnsafeIgnore, ref UnsafeIgnore, ref UnsafeIgnore, ref UnsafeIgnore, ref UnsafeIgnore, ref UnsafeIgnore))
            {
                remainingVisibleCount++;
            }
        }

        if (warningCodes.Count > 0)
        {
            var topCodes = string.Join(", ", warningCodes
                .OrderByDescending(static pair => pair.Value)
                .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(static pair => $"{pair.Key} x{pair.Value}"));
            notes.Add($"Suppressed {warningCodes.Values.Sum()} compiler/NuGet warning lines ({topCodes}).");
        }

        if (suppressedHttpTraceCount > 0)
        {
            notes.Add($"Suppressed {suppressedHttpTraceCount} low-value framework HTTP trace lines.");
        }

        if (suppressedEntityFrameworkCount > 0)
        {
            notes.Add($"Suppressed {suppressedEntityFrameworkCount} Entity Framework information/command trace lines.");
        }

        if (suppressedAspNetRequestCount > 0)
        {
            notes.Add($"Suppressed {suppressedAspNetRequestCount} ASP.NET Core request trace lines.");
        }

        if (suppressedDiagnosticTraceCount > 0)
        {
            notes.Add($"Suppressed {suppressedDiagnosticTraceCount} debug/trace log lines.");
        }

        if (suppressedRestoreCount > 0)
        {
            notes.Add($"Suppressed {suppressedRestoreCount} restore/build progress lines.");
        }

        if (suppressedArtifactCount > 0)
        {
            notes.Add($"Suppressed {suppressedArtifactCount} artifact output lines.");
        }

        if (suppressedBlankCount > 2)
        {
            notes.Add($"Suppressed {suppressedBlankCount} blank lines.");
        }

        if (suppressedEntryCount > 0)
        {
            notes.Add("Use view=Raw to inspect the full unsummarized console output.");
        }

        return new ReducedLogData(
            visibleEntries.ToArray(),
            consumedRawCount == 0 ? startCursor : lastConsumedSequence,
            remainingVisibleCount > 0,
            remainingVisibleCount,
            new LogFilterSummaryData(LogViewMode.AgentOptimized, consumedRawCount, visibleEntries.Count, suppressedEntryCount, notes));
    }

    private static int UnsafeIgnore;

    private static bool ShouldSuppress(
        LogEntry entry,
        LogReductionScenario scenario,
        ref ContinuationKind continuation,
        Dictionary<string, int>? warningCodes,
        ref int suppressedRestoreCount,
        ref int suppressedArtifactCount,
        ref int suppressedBlankCount,
        ref int suppressedHttpTraceCount,
        ref int suppressedEntityFrameworkCount,
        ref int suppressedAspNetRequestCount,
        ref int suppressedDiagnosticTraceCount)
    {
        var text = entry.Text ?? string.Empty;
        if (continuation != ContinuationKind.None &&
            IsIndentedContinuation(text) &&
            !StartsNewStructuredRecord(text))
        {
            switch (continuation)
            {
                case ContinuationKind.HttpClient:
                    suppressedHttpTraceCount++;
                    return true;
                case ContinuationKind.EntityFramework:
                    suppressedEntityFrameworkCount++;
                    return true;
                case ContinuationKind.AspNetRequest:
                    suppressedAspNetRequestCount++;
                    return true;
                case ContinuationKind.DiagnosticTrace:
                    suppressedDiagnosticTraceCount++;
                    return true;
            }
        }

        continuation = ContinuationKind.None;

        if (string.IsNullOrWhiteSpace(text))
        {
            suppressedBlankCount++;
            return true;
        }

        if (TryCaptureWarning(text, warningCodes))
        {
            return true;
        }

        if (text.Contains("Determining projects to restore...", StringComparison.OrdinalIgnoreCase) ||
            text.TrimStart().StartsWith("Restored ", StringComparison.OrdinalIgnoreCase))
        {
            suppressedRestoreCount++;
            return true;
        }

        if (scenario == LogReductionScenario.Operation &&
            Regex.IsMatch(text, @"^\s*.+\s->\s.+$", RegexOptions.CultureInvariant))
        {
            suppressedArtifactCount++;
            return true;
        }

        if (text.StartsWith("dbug:", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("trce:", StringComparison.OrdinalIgnoreCase))
        {
            continuation = ContinuationKind.DiagnosticTrace;
            suppressedDiagnosticTraceCount++;
            return true;
        }

        if (text.StartsWith("info: System.Net.Http.HttpClient.", StringComparison.OrdinalIgnoreCase))
        {
            continuation = ContinuationKind.HttpClient;
            suppressedHttpTraceCount++;
            return true;
        }

        if (text.StartsWith("info: Microsoft.EntityFrameworkCore.", StringComparison.OrdinalIgnoreCase))
        {
            continuation = ContinuationKind.EntityFramework;
            suppressedEntityFrameworkCount++;
            return true;
        }

        if (text.StartsWith("info: Microsoft.AspNetCore.Hosting.Diagnostics", StringComparison.OrdinalIgnoreCase) &&
            (text.Contains("Request starting", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("Request finished", StringComparison.OrdinalIgnoreCase)))
        {
            continuation = ContinuationKind.AspNetRequest;
            suppressedAspNetRequestCount++;
            return true;
        }

        return false;
    }

    private static bool TryCaptureWarning(string text, Dictionary<string, int>? warningCodes)
    {
        var match = WarningCodeRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        if (warningCodes is not null)
        {
            var code = match.Groups["code"].Value.ToUpperInvariant();
            warningCodes[code] = warningCodes.TryGetValue(code, out var current) ? current + 1 : 1;
        }

        return true;
    }

    private static bool IsIndentedContinuation(string text)
        => text.Length > 0 && char.IsWhiteSpace(text[0]);

    private static bool StartsNewStructuredRecord(string text)
    {
        return text.StartsWith("info:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("warn:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("fail:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("error", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("crit:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("dbug:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("trce:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("dotnet watch :", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("Now listening on:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("Unhandled exception", StringComparison.OrdinalIgnoreCase);
    }
}
