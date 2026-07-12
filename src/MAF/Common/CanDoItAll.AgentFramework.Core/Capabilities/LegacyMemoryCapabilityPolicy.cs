using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class LegacyMemoryCapabilityPolicy
{
    public static bool IsRetired(CapabilityKind kind) => kind == CapabilityKind.Memory;

    public static string BuildDiagnostic(string? capabilityName)
    {
        var subject = string.IsNullOrWhiteSpace(capabilityName)
            ? "The catalog capability"
            : $"Capability '{capabilityName.Trim()}'";
        return $"{subject} uses the retired catalog Memory integration. Remove the stale capability assignment and configure external memory provider bindings in the agent Memory settings.";
    }

    public static InvalidOperationException CreateException(string? capabilityName) =>
        new(BuildDiagnostic(capabilityName));

    public static void EnsureNotRetired(CapabilityKind kind, string? capabilityName)
    {
        if (IsRetired(kind))
        {
            throw CreateException(capabilityName);
        }
    }
}
