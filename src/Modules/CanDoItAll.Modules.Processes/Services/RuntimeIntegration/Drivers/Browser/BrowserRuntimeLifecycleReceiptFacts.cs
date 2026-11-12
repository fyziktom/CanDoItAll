using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class BrowserRuntimeLifecycleReceiptFacts
{
    internal const string ExecutionGuidance = """
        Browser host lifecycle evidence:
        After each successful host start or restart, call browser_navigate with the exact loopback URL returned by that start, then collect the required browser proof before stopping that host with its returned startup.json receipt. The latest successful start needs its own current-execution browser and cleanup receipts, even when a restart reuses the same URL or unchanged output. Earlier screenshots do not prove a restarted host.
        Offline/cache tests may continue after stopping a host, but do not replace its online lifecycle proof. If an update check needs a restart, navigate to the returned URL again and collect fresh proof before cleanup. Do not restart merely to restate earlier evidence. Keep all owned hosts accounted for and stopped before completing the step.
        """;

    internal sealed record BrowserRuntimeLifecycleToolNames(string RunToolName, string StopToolName);

    internal sealed record BrowserRuntimeLifecycleReceipt(
        IReadOnlyList<string> StartupReceiptPaths,
        IReadOnlyList<string> LoopbackAuthorities)
    {
        public static BrowserRuntimeLifecycleReceipt From(ToolExecutionReceiptRecord receipt)
        {
            var text = string.Join(
                Environment.NewLine,
                [receipt.RequestSummary, receipt.WorkingDirectory, receipt.ExitSummary]);
            return new BrowserRuntimeLifecycleReceipt(
                ExtractStartupReceiptPaths(text),
                ExtractLoopbackAuthorities(text));
        }
    }

    internal static IReadOnlyList<string> ExtractStartupReceiptPaths(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return Regex.Matches(
                text,
                @"(?:startupReceipt=)?(?<path>[A-Za-z]:\\[^\s;|""'<>]*startup\.json|(?:\.?/)?(?:artifacts|outputs|data|tool-runs|process-runs)/[^\s;|""'<>]*startup\.json)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => NormalizeLifecyclePath(match.Groups["path"].Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static IReadOnlyList<string> ExtractLoopbackAuthorities(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return Regex.Matches(
                text,
                @"https?://(?:localhost|127\.0\.0\.1|\[::1\]):\d+(?:/[^\s""'<>)]*)?",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => NormalizeLoopbackAuthority(match.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static string NormalizeLifecyclePath(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().TrimEnd('.', ',', ';').Replace('\\', '/');

    internal static string NormalizeLoopbackAuthority(string value)
    {
        if (!Uri.TryCreate(value.Trim().TrimEnd('.', ',', ';'), UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var host = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            ? "127.0.0.1"
            : uri.Host.Trim('[', ']');
        return $"{uri.Scheme.ToLowerInvariant()}://{host.ToLowerInvariant()}:{uri.Port}";
    }
}
