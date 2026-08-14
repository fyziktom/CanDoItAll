using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Readiness;

namespace CanDoItAll.Composition;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityId
{
    ControlPlanePaths,
    PhysicalFileSystem,
    SecretVault,
    FileToolsDesktop,
    DesktopFileOpen,
    InteractiveTerminal,
    NativeProcessDiscovery
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityCriticality
{
    Mandatory,
    Optional
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityAvailability
{
    Available,
    Unavailable,
    Unsupported,
    Misconfigured,
    Unverified
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityReasonCode
{
    Ready,
    ProbePending,
    DisabledByProfile,
    DependencyUnavailable,
    UnsupportedByProfile,
    InvalidConfiguration,
    PermissionDenied,
    UnsafePath,
    IoFailure,
    ActualHostValidationDeferred
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilitySupportLevel
{
    Stable,
    BasicLocal,
    DevelopmentOnly,
    ActualHostUnverified,
    Unsupported
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityExecutionBoundary
{
    ManagedProcess,
    OperatingSystem,
    ExternalProcess,
    ExternalService
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityImplementationRegistration
{
    Registered,
    NotRegistered
}

public sealed record HostCapabilityDescriptor(
    HostCapabilityId Id,
    HostCapabilityCriticality Criticality,
    HostCapabilityAvailability Availability,
    HostCapabilityReasonCode ReasonCode,
    string Remediation,
    HostCapabilitySupportLevel SupportLevel,
    HostCapabilityImplementationRegistration ImplementationRegistration,
    string? ImplementationId,
    string? ImplementationVersion,
    HostCapabilityExecutionBoundary ExecutionBoundary,
    RuntimeHostProfileKind SupportProfile,
    DateTimeOffset ObservedAtUtc);

public sealed record HostCapabilitySnapshot(
    RuntimeHostProfileKind Profile,
    RuntimeHostOperatingSystem OperatingSystem,
    bool IsInteractive,
    bool IsReady,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<ApplicationPurposeRootReadiness> PurposeRoots,
    IReadOnlyList<HostCapabilityDescriptor> Capabilities);

public interface IHostCapabilitySnapshotProvider
{
    HostCapabilitySnapshot GetSnapshot();
}
