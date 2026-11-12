using CanDoItAll.Application.UI.Runtime;
using CanDoItAll.Composition;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Web.RuntimeCapabilities;

internal static class RuntimeCapabilitiesPresentationMapper {
    public static RuntimeCapabilitiesPresentation Map(HostCapabilitySnapshot snapshot) => new(
        snapshot.Profile.ToString(), snapshot.OperatingSystem.ToString(), snapshot.IsInteractive,
        snapshot.IsReady, snapshot.Capabilities.Select(capability => new RuntimeCapabilityPresentation(
            CapabilityTestId(capability.Id), FormatName(capability.Id.ToString()), capability.Criticality.ToString(),
            capability.Availability.ToString(), AvailabilityTone(capability.Availability),
            FormatName(capability.SupportLevel.ToString()), FormatName(capability.ReasonCode.ToString()),
            FormatName(capability.ExecutionBoundary.ToString()), FormatName(capability.ImplementationRegistration.ToString()),
            capability.ObservedAtUtc,
            capability.ImplementationRegistration == HostCapabilityImplementationRegistration.Registered
                ? FormatImplementation(capability) : null,
            capability.Remediation, RemediationStyle(capability))).ToArray());

    private static string CapabilityTestId(HostCapabilityId id)
        => $"runtime-capability-{id.ToString().ToLowerInvariant()}";

    private static string FormatImplementation(HostCapabilityDescriptor capability)
        => capability.ImplementationVersion is { Length: > 0 } version
            ? $"{capability.ImplementationId} {version}"
            : capability.ImplementationId ?? "Registered implementation";

    private static string FormatName(string value) {
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (int index = 0; index < value.Length; index++) {
            char current = value[index];
            if (index > 0 && char.IsUpper(current)) {
                result.Append(' ');
            }

            result.Append(index == 0 ? char.ToUpperInvariant(current) : current);
        }

        return result.ToString();
    }

    private static string AvailabilityTone(HostCapabilityAvailability availability)
        => availability switch {
            HostCapabilityAvailability.Available => "success",
            HostCapabilityAvailability.Unavailable or
                HostCapabilityAvailability.Misconfigured => "danger",
            HostCapabilityAvailability.Unsupported or
                HostCapabilityAvailability.Unverified => "warning",
            _ => "neutral"
        };

    private static AlertStyle RemediationStyle(HostCapabilityDescriptor capability)
        => capability.Availability switch {
            HostCapabilityAvailability.Available when capability.ReasonCode == HostCapabilityReasonCode.Ready
                => AlertStyle.Light,
            HostCapabilityAvailability.Available => AlertStyle.Warning,
            HostCapabilityAvailability.Unavailable or HostCapabilityAvailability.Misconfigured
                => AlertStyle.Danger,
            _ => AlertStyle.Warning
        };
}
