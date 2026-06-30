using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class McpDiagnostics
{
    private static readonly Regex SecretAssignmentPattern = new(
        "(?i)(api[_-]?key|token|secret|authorization)(\"?\\s*[:=]\\s*\"?)([^\\s\",}]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BearerPattern = new(
        "(?i)Bearer\\s+[^\\s\",}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CapabilityDiagnostic Create(
        CapabilityDiagnosticCategory category,
        McpServerDescriptor descriptor,
        string fieldPath,
        string detail,
        string repairHint,
        string correlationId,
        int? httpStatusCode = null)
    {
        return new CapabilityDiagnostic(
            category,
            CapabilityValidationSeverity.Error,
            descriptor.Identity.Kind,
            descriptor.Identity.Key,
            null,
            fieldPath,
            descriptor is InternalHostedMcpServerDescriptor hosted ? hosted.ImplementationKey : null,
            ResolveTransport(descriptor),
            null,
            httpStatusCode,
            category == CapabilityDiagnosticCategory.Timeout ? descriptor.Timeout : null,
            correlationId,
            Bound(Mask(detail), 240),
            repairHint);
    }

    public static string Mask(string detail)
    {
        var bearerMasked = BearerPattern.Replace(detail, "Bearer ***");
        return SecretAssignmentPattern.Replace(bearerMasked, "$1$2***");
    }

    private static string Bound(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 14)] + "...[truncated]";
    }

    private static CapabilityTransportKind ResolveTransport(McpServerDescriptor descriptor)
    {
        return descriptor.DescriptorKind switch
        {
            McpServerDescriptorKind.InternalHosted => CapabilityTransportKind.InternalHosted,
            McpServerDescriptorKind.LocalStdio => CapabilityTransportKind.LocalStdio,
            McpServerDescriptorKind.RemoteHttp => CapabilityTransportKind.RemoteHttp,
            _ => CapabilityTransportKind.RemoteHttp
        };
    }
}
