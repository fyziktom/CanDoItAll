using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Tools.Abstractions;

namespace CanDoItAll.AgentFramework.Tools;

internal static class ToolDiagnostics
{
    private static readonly Regex SecretAssignmentPattern = new(
        "(?i)(api[_-]?key|token|secret|authorization)(\"?\\s*[:=]\\s*\"?)([^\\s\",}]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BearerPattern = new(
        "(?i)Bearer\\s+[^\\s\",}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CapabilityDiagnostic Create(
        CapabilityDiagnosticCategory category,
        ToolDescriptor descriptor,
        string fieldPath,
        string maskedDetail,
        string repairHint,
        string correlationId,
        CapabilityTransportKind transport,
        int? exitCode = null,
        int? httpStatusCode = null,
        TimeSpan? timeout = null)
    {
        return new CapabilityDiagnostic(
            category,
            CapabilityValidationSeverity.Error,
            descriptor.Identity.Kind,
            descriptor.Identity.Key,
            null,
            fieldPath,
            descriptor.ImplementationKey,
            transport,
            exitCode,
            httpStatusCode,
            timeout,
            correlationId,
            Bound(Mask(maskedDetail), 200),
            repairHint);
    }

    public static string Mask(string detail)
    {
        var bearerMasked = BearerPattern.Replace(detail, "Bearer ***");
        return SecretAssignmentPattern.Replace(bearerMasked, "$1$2***");
    }

    public static string Bound(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 14)] + "...[truncated]";
    }
}
