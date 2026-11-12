using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Application.UI.Runtime;

public sealed record RuntimeCapabilitiesPresentation(
    string Profile,
    string OperatingSystem,
    bool IsInteractive,
    bool IsReady,
    IReadOnlyList<RuntimeCapabilityPresentation> Capabilities);

public sealed record RuntimeCapabilityPresentation(
    string TestId,
    string Name,
    string Criticality,
    string Availability,
    string AvailabilityTone,
    string SupportLevel,
    string Reason,
    string ExecutionBoundary,
    string ImplementationRegistration,
    DateTimeOffset ObservedAtUtc,
    string? ImplementationIdentity,
    string Remediation,
    AlertStyle RemediationStyle);
