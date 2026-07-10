using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptMatcher;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeLifecycleReceiptFacts
{
    internal sealed record RuntimeLifecycleToolNames(string RunToolName, string StopToolName);

    internal sealed record RuntimeLifecycleReceiptFacts(
        IReadOnlyList<string> StartupReceiptPaths,
        IReadOnlyList<string> LoopbackAuthorities)
    {
        public static RuntimeLifecycleReceiptFacts From(ToolExecutionReceiptRecord receipt)
        {
            var text = ReceiptText(receipt);
            return new RuntimeLifecycleReceiptFacts(
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
