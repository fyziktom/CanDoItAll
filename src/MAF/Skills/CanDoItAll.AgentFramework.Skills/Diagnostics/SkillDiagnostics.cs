using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Skills.Abstractions;

namespace CanDoItAll.AgentFramework.Skills;

internal static class SkillDiagnostics
{
    public static CapabilityDiagnostic Create(
        CapabilityDiagnosticCategory category,
        SkillDescriptor descriptor,
        string fieldPath,
        string detail,
        string repairHint,
        string correlationId,
        CapabilityTransportKind transport,
        ImplementationKey? implementationKey = null)
    {
        return new CapabilityDiagnostic(
            category,
            CapabilityValidationSeverity.Error,
            descriptor.Identity.Kind,
            descriptor.Identity.Key,
            null,
            fieldPath,
            implementationKey,
            transport,
            null,
            null,
            null,
            correlationId,
            Bound(detail, 240),
            repairHint);
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
