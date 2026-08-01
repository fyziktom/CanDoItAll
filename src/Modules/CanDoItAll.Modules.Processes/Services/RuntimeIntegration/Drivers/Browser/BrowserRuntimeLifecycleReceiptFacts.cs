using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class BrowserRuntimeLifecycleReceiptFacts
{
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
