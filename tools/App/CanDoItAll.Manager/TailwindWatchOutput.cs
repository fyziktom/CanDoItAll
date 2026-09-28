using System.Text.RegularExpressions;

namespace CanDoItAll.Manager;

public enum TailwindWatchLineKind
{
    Informational,
    BuildCompleted,
    Error
}

public sealed record TailwindWatchLine(TailwindWatchLineKind Kind, string Text, string? Duration = null);

public static partial class TailwindWatchOutputParser
{
    public static TailwindWatchLine Parse(string line)
    {
        var text = Sanitize(line);
        if (text.Length == 0)
        {
            return new TailwindWatchLine(TailwindWatchLineKind.Informational, text);
        }

        var buildCompleted = BuildCompletedRegex().Match(text);
        if (buildCompleted.Success)
        {
            return new TailwindWatchLine(
                TailwindWatchLineKind.BuildCompleted,
                text,
                buildCompleted.Groups["duration"].Value);
        }

        return ErrorRegex().IsMatch(text)
            ? new TailwindWatchLine(TailwindWatchLineKind.Error, text)
            : new TailwindWatchLine(TailwindWatchLineKind.Informational, text);
    }

    // Tailwind colours its output, redraws its polling status with carriage returns and frames errors
    // with box-drawing characters. Only the text that stays visible on a terminal is kept, without the
    // leading decoration, which also arrives garbled when the host decodes output with an OEM code page.
    public static string Sanitize(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = AnsiEscapeRegex().Replace(line, string.Empty).TrimEnd('\r');
        var carriageReturn = text.LastIndexOf('\r');
        if (carriageReturn >= 0)
        {
            text = text[(carriageReturn + 1)..];
        }

        return LeadingDecorationRegex().Replace(text, string.Empty).TrimEnd();
    }

    public static bool IsErrorHeader(string text)
        => ErrorHeaderRegex().IsMatch(text);

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscapeRegex();

    [GeneratedRegex(@"^[\s\u0080-￿]+")]
    private static partial Regex LeadingDecorationRegex();

    [GeneratedRegex(@"\bDone in (?<duration>\d[^\s]*)")]
    private static partial Regex BuildCompletedRegex();

    [GeneratedRegex(@"^(?:\[[^\]]+\]\s*)?(?:\w*Error\b|npm (?:error|ERR!))|is not recognized as an internal or external command|\bcommand not found\b|: not found$")]
    private static partial Regex ErrorRegex();

    [GeneratedRegex(@"^(?:\[[^\]]+\]\s*)?(?:Error|npm error|npm ERR!):?$")]
    private static partial Regex ErrorHeaderRegex();
}

public enum TailwindBuildCycleOutcome
{
    Succeeded,
    Failed
}

// Tailwind's watch mode reports a failed rebuild as an error block followed by an ordinary
// "Done in" line, so a completed cycle is successful only when no error preceded it.
internal sealed class TailwindBuildCycleTracker
{
    private bool errorReportedInCycle;
    private string? cycleError;

    public string? LastError { get; private set; }

    // Returns true when the error worth reporting changed: the first error of a cycle, or the first
    // detailed message after a bare "Error:" header.
    public bool ObserveError(TailwindWatchLine line)
    {
        var changed = !errorReportedInCycle;
        if (changed)
        {
            errorReportedInCycle = true;
            LastError = null;
        }

        if (cycleError is null && !TailwindWatchOutputParser.IsErrorHeader(line.Text))
        {
            cycleError = line.Text;
            LastError = line.Text;
            changed = true;
        }

        return changed;
    }

    public TailwindBuildCycleOutcome CompleteCycle()
    {
        var outcome = errorReportedInCycle
            ? TailwindBuildCycleOutcome.Failed
            : TailwindBuildCycleOutcome.Succeeded;
        errorReportedInCycle = false;
        cycleError = null;
        if (outcome == TailwindBuildCycleOutcome.Succeeded)
        {
            LastError = null;
        }

        return outcome;
    }
}

internal sealed record TailwindWatchRestartPolicy(
    TimeSpan MinimumDelay,
    TimeSpan MaximumDelay,
    TimeSpan StableRunDuration)
{
    public static TailwindWatchRestartPolicy Default { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(1));

    public TimeSpan NextDelay(TimeSpan? previousDelay, TimeSpan runDuration)
    {
        if (previousDelay is null || runDuration >= StableRunDuration)
        {
            return MinimumDelay;
        }

        var doubled = TimeSpan.FromTicks(previousDelay.Value.Ticks * 2);
        return doubled > MaximumDelay ? MaximumDelay : doubled;
    }
}
