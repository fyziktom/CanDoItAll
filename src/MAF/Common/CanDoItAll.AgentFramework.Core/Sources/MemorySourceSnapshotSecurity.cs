using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.Core;

public static partial class MemorySourceSnapshotSecurity
{
    public const string RedactedValue = "[REDACTED]";

    public static bool ContainsSensitiveInlineValue(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
            (SensitiveAssignmentRegex().IsMatch(value) || SensitiveInlineRegex().IsMatch(value));

    public static bool IsSensitiveQueryParameterName(string key)
        => SensitiveQueryParameterRegex().IsMatch(key ?? string.Empty);

    public static string RedactWhenSensitive(string? value, bool shouldRedact)
        => shouldRedact ? RedactedValue : RedactSensitiveInlineValues(value);

    public static string RedactSensitiveInlineValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return SensitiveInlineRegex().Replace(
            SensitiveAssignmentRegex().Replace(value, "$1[REDACTED]"),
            "$1[REDACTED]");
    }

    public static MemorySourcePermissionContext CreatePermission(
        bool containsSensitivePayload,
        string redactionPolicy,
        string allowedFutureUsageSummary,
        MemorySourceSensitivity sensitiveSensitivity = MemorySourceSensitivity.Sensitive,
        MemorySourceSensitivity nonSensitiveSensitivity = MemorySourceSensitivity.Internal)
    {
        return new MemorySourcePermissionContext(
            containsSensitivePayload ? MemorySourceAccessMode.Redacted : MemorySourceAccessMode.ReadOnly,
            containsSensitivePayload ? sensitiveSensitivity : nonSensitiveSensitivity,
            containsSensitivePayload,
            redactionPolicy,
            allowedFutureUsageSummary);
    }

    public static MemorySourceHashPolicy CreateIntegrityHashPolicy(
        bool containsSensitivePayload,
        string restrictedRawPayloadIntegritySummary)
    {
        return containsSensitivePayload
            ? MemorySourceHashPolicy.RestrictedRawPayloadIntegrity(restrictedRawPayloadIntegritySummary)
            : MemorySourceHashPolicy.InternalIntegrity;
    }

    [GeneratedRegex(@"(?im)^(\s*(?:password|passwd|pwd|secret|api[_-]?key|access[_-]?token|refresh[_-]?token|bearer|connectionstring|connection_string|token)\s*[:=]\s*).+$", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentRegex();

    [GeneratedRegex(@"(?i)(\b(?:password|passwd|pwd|secret|api[_-]?key|access[_-]?token|refresh[_-]?token|bearer|connectionstring|connection_string|token)\s*[:=]\s*)[^\s,;""}]+", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveInlineRegex();

    [GeneratedRegex(@"(?i)^(?:access_token|api_key|apikey|client_secret|code|password|secret|token)$", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveQueryParameterRegex();
}
