using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Readiness;

namespace CanDoItAll.Composition;

/// <summary>
/// Host capability reported by the runtime snapshot, as a string token: <c>ControlPlanePaths</c> (control-plane
/// and application purpose-root paths, mandatory), <c>PhysicalFileSystem</c> (safe native file system access under
/// those roots, mandatory), <c>SecretVault</c> (the configured secret vault, mandatory), <c>FileToolsDesktop</c>
/// (desktop file tools, optional), <c>DesktopFileOpen</c> (opening files with desktop applications, optional),
/// <c>InteractiveTerminal</c> (interactive terminal, optional, not implemented in this build) or
/// <c>NativeProcessDiscovery</c> (native process discovery, optional, not implemented in this build).
/// </summary>
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

/// <summary>
/// Whether the host needs a capability to be ready, as a string token: <c>Mandatory</c> (the host is ready only when
/// the capability is available) or <c>Optional</c> (the host can be ready without it).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityCriticality
{
    Mandatory,
    Optional
}

/// <summary>
/// Current availability of a host capability, as a string token: <c>Available</c>, <c>Unavailable</c> (a
/// dependency is missing or failing), <c>Unsupported</c> (the host profile does not support it),
/// <c>Misconfigured</c> (its configuration is invalid or unsafe) or <c>Unverified</c> (its probe has not completed).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityAvailability
{
    Available,
    Unavailable,
    Unsupported,
    Misconfigured,
    Unverified
}

/// <summary>
/// Reason for the availability of a host capability, as a string token: <c>Ready</c>, <c>ProbePending</c> (the
/// startup probe has not completed), <c>DisabledByProfile</c> (the host profile, for example a headless one, turns
/// it off), <c>DependencyUnavailable</c>, <c>UnsupportedByProfile</c>, <c>InvalidConfiguration</c>,
/// <c>PermissionDenied</c> (the operating-system account lacks access), <c>UnsafePath</c> (a configured path failed
/// the path safety checks), <c>IoFailure</c> or <c>ActualHostValidationDeferred</c> (available, but support on this
/// kind of host is not yet validated).
/// </summary>
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

/// <summary>
/// Support level of a host capability on this host, as a string token: <c>Stable</c>, <c>BasicLocal</c> (works
/// with basic local protection only, such as a local user-file secret vault), <c>DevelopmentOnly</c> (acceptable for
/// development only), <c>ActualHostUnverified</c> (works, but is not validated on a real host of this kind) or
/// <c>Unsupported</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilitySupportLevel
{
    Stable,
    BasicLocal,
    DevelopmentOnly,
    ActualHostUnverified,
    Unsupported
}

/// <summary>
/// Where the implementation of a host capability runs, as a string token: <c>ManagedProcess</c> (inside the host
/// process), <c>OperatingSystem</c> (operating-system services), <c>ExternalProcess</c> or <c>ExternalService</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityExecutionBoundary
{
    ManagedProcess,
    OperatingSystem,
    ExternalProcess,
    ExternalService
}

/// <summary>
/// Whether this build contains an implementation of a host capability, as a string token: <c>Registered</c> or
/// <c>NotRegistered</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostCapabilityImplementationRegistration
{
    Registered,
    NotRegistered
}

/// <summary>
/// Availability of one host capability in a runtime snapshot, with the reason and the remediation an operator can
/// apply. Every enumerated member is written as a string token.
/// </summary>
/// <param name="Id">
/// The capability: <c>ControlPlanePaths</c>, <c>PhysicalFileSystem</c>, <c>SecretVault</c>, <c>FileToolsDesktop</c>,
/// <c>DesktopFileOpen</c>, <c>InteractiveTerminal</c> or <c>NativeProcessDiscovery</c>.
/// </param>
/// <param name="Criticality">
/// <c>Mandatory</c> when the host is ready only while the capability is available, otherwise <c>Optional</c>.
/// </param>
/// <param name="Availability">
/// Current availability: <c>Available</c>, <c>Unavailable</c>, <c>Unsupported</c>, <c>Misconfigured</c> or
/// <c>Unverified</c>.
/// </param>
/// <param name="ReasonCode">
/// Reason for the availability: <c>Ready</c>, <c>ProbePending</c>, <c>DisabledByProfile</c>,
/// <c>DependencyUnavailable</c>, <c>UnsupportedByProfile</c>, <c>InvalidConfiguration</c>, <c>PermissionDenied</c>,
/// <c>UnsafePath</c>, <c>IoFailure</c> or <c>ActualHostValidationDeferred</c>.
/// </param>
/// <param name="Remediation">
/// Human-readable action that would make the capability available, or <c>None.</c> when nothing is needed. Its
/// wording can change and must not be parsed.
/// </param>
/// <param name="SupportLevel">
/// Support level on this host: <c>Stable</c>, <c>BasicLocal</c>, <c>DevelopmentOnly</c>, <c>ActualHostUnverified</c>
/// or <c>Unsupported</c>.
/// </param>
/// <param name="ImplementationRegistration">
/// <c>Registered</c> when this build contains an implementation, <c>NotRegistered</c> when it has none.
/// </param>
/// <param name="ImplementationId">
/// Name of the implementing component, for example <c>CanDoItAll.Infrastructure.ControlPlane</c> or the secret-vault
/// provider name; null when there is none.
/// </param>
/// <param name="ImplementationVersion">Assembly version of the implementing component; null when unknown.</param>
/// <param name="ExecutionBoundary">
/// Where the implementation runs: <c>ManagedProcess</c>, <c>OperatingSystem</c>, <c>ExternalProcess</c> or
/// <c>ExternalService</c>.
/// </param>
/// <param name="SupportProfile">
/// The resolved host profile the entry was evaluated for, for example <c>WindowsHeadless</c>; the same as
/// <c>profile</c> of the snapshot.
/// </param>
/// <param name="ObservedAtUtc">Instant (UTC, with offset) at which the snapshot was taken.</param>
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

/// <summary>
/// Snapshot of the runtime host: its resolved profile, whether it is ready, the readiness of each application purpose
/// root and the availability of each host capability. Returned by <c>GET /api/runtime/capabilities</c> and inside
/// <c>GET /api/runtime/operations</c>; each read takes a new snapshot. It contains no paths or secret values.
/// </summary>
/// <param name="Profile">
/// The resolved host profile, as a string token: <c>WindowsInteractive</c>, <c>WindowsHeadless</c>,
/// <c>LinuxInteractive</c>, <c>LinuxHeadless</c>, <c>MacOsInteractive</c>, <c>MacOsHeadless</c> or <c>Test</c>
/// (development test hosts only).
/// </param>
/// <param name="OperatingSystem">
/// Operating system of the host, as a string token: <c>Windows</c>, <c>Linux</c> or <c>MacOs</c>.
/// </param>
/// <param name="IsInteractive">
/// True for an interactive (desktop) profile; false for a headless service or container profile, on which desktop
/// capabilities are unsupported.
/// </param>
/// <param name="IsReady">
/// True when every mandatory capability is available. Optional capabilities do not affect it.
/// </param>
/// <param name="ObservedAtUtc">Instant (UTC, with offset) at which the snapshot was taken.</param>
/// <param name="PurposeRoots">
/// Readiness of each application purpose root (workspace, control plane, database profiles, Data Protection keys,
/// state, logs and runtime temporary data). Empty when the path probe produced no result.
/// </param>
/// <param name="Capabilities">One entry per host capability, in a fixed order.</param>
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
