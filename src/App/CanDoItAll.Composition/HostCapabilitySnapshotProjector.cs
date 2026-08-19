using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Modules.Security;

namespace CanDoItAll.Composition;

public static class HostCapabilitySnapshotProjector
{
    private const string NoRemediation = "None.";
    private const string ControlPlaneImplementationId = "CanDoItAll.Infrastructure.ControlPlane";
    private const string FileSystemImplementationId = "CanDoItAll.Infrastructure.FileSystem";
    private const string FileToolsImplementationId = "CanDoItAll.FileTools.Desktop";
    private const string FileToolsRemediation =
        "Enable desktop file launching only on an interactive host with a supported desktop session.";
    private const string TerminalRemediation =
        "Register a purpose-owned interactive terminal adapter for this host profile.";
    private const string ProcessDiscoveryRemediation =
        "Run native process recovery through the Manager-owned discovery adapter when that feature is required.";

    public static HostCapabilitySnapshot Create(
        ResolvedRuntimeHostProfile profile,
        PathFoundationReadinessSnapshot? pathFoundationReadiness,
        SecretVaultProbeResult? secretVaultProbe,
        string? infrastructureImplementationVersion,
        string? securityImplementationVersion,
        bool desktopFileOpenAvailable,
        string? fileToolsImplementationVersion,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var capabilities = new List<HostCapabilityDescriptor>
        {
            ProjectPathCapability(
                HostCapabilityId.ControlPlanePaths,
                pathFoundationReadiness?.ControlPlanePaths,
                ControlPlaneImplementationId,
                infrastructureImplementationVersion,
                HostCapabilityExecutionBoundary.ManagedProcess,
                profile,
                observedAtUtc),
            ProjectPathCapability(
                HostCapabilityId.PhysicalFileSystem,
                pathFoundationReadiness?.PhysicalFileSystem,
                FileSystemImplementationId,
                infrastructureImplementationVersion,
                HostCapabilityExecutionBoundary.OperatingSystem,
                profile,
                observedAtUtc),
            ProjectSecretVault(
                profile,
                secretVaultProbe,
                securityImplementationVersion,
                observedAtUtc),
            ProjectDesktopCapability(
                HostCapabilityId.FileToolsDesktop,
                profile,
                desktopFileOpenAvailable,
                fileToolsImplementationVersion,
                observedAtUtc),
            ProjectDesktopCapability(
                HostCapabilityId.DesktopFileOpen,
                profile,
                desktopFileOpenAvailable,
                fileToolsImplementationVersion,
                observedAtUtc),
            ProjectAbsentOptionalCapability(
                HostCapabilityId.InteractiveTerminal,
                profile,
                TerminalRemediation,
                HostCapabilityExecutionBoundary.ExternalProcess,
                observedAtUtc),
            ProjectAbsentOptionalCapability(
                HostCapabilityId.NativeProcessDiscovery,
                profile,
                ProcessDiscoveryRemediation,
                HostCapabilityExecutionBoundary.OperatingSystem,
                observedAtUtc)
        };

        bool isReady = capabilities
            .Where(capability => capability.Criticality == HostCapabilityCriticality.Mandatory)
            .All(capability => capability.Availability == HostCapabilityAvailability.Available);

        return new HostCapabilitySnapshot(
            profile.Kind,
            profile.OperatingSystem,
            profile.IsInteractive,
            isReady,
            observedAtUtc,
            pathFoundationReadiness?.PurposeRoots ?? [],
            capabilities);
    }

    private static HostCapabilityDescriptor ProjectPathCapability(
        HostCapabilityId id,
        PathCapabilityReadiness? readiness,
        string implementationId,
        string? implementationVersion,
        HostCapabilityExecutionBoundary boundary,
        ResolvedRuntimeHostProfile profile,
        DateTimeOffset observedAtUtc)
    {
        if (readiness is null)
        {
            return Descriptor(
                id,
                HostCapabilityCriticality.Mandatory,
                HostCapabilityAvailability.Unverified,
                HostCapabilityReasonCode.ProbePending,
                "Wait for the mandatory path and filesystem readiness probe to complete.",
                HostCapabilitySupportLevel.Unsupported,
                HostCapabilityImplementationRegistration.Registered,
                implementationId,
                implementationVersion,
                boundary,
                profile,
                observedAtUtc);
        }

        if (readiness.State == PathFoundationReadinessState.Ready)
        {
            return Descriptor(
                id,
                HostCapabilityCriticality.Mandatory,
                HostCapabilityAvailability.Available,
                HostCapabilityReasonCode.Ready,
                NoRemediation,
                HostCapabilitySupportLevel.Stable,
                HostCapabilityImplementationRegistration.Registered,
                implementationId,
                implementationVersion,
                boundary,
                profile,
                observedAtUtc);
        }

        HostCapabilityAvailability availability = readiness.Reason is
            PathFoundationReadinessReason.InvalidConfiguration or
            PathFoundationReadinessReason.UnsafePath
                ? HostCapabilityAvailability.Misconfigured
                : HostCapabilityAvailability.Unavailable;
        return Descriptor(
            id,
            HostCapabilityCriticality.Mandatory,
            availability,
            ResolvePathReason(readiness.Reason),
            ResolvePathRemediation(id, readiness.Reason),
            HostCapabilitySupportLevel.Unsupported,
            HostCapabilityImplementationRegistration.Registered,
            implementationId,
            implementationVersion,
            boundary,
            profile,
            observedAtUtc);
    }

    private static HostCapabilityDescriptor ProjectSecretVault(
        ResolvedRuntimeHostProfile profile,
        SecretVaultProbeResult? probe,
        string? implementationVersion,
        DateTimeOffset observedAtUtc)
    {
        if (probe is null)
        {
            return Descriptor(
                HostCapabilityId.SecretVault,
                HostCapabilityCriticality.Mandatory,
                HostCapabilityAvailability.Unverified,
                HostCapabilityReasonCode.ProbePending,
                "Wait for the mandatory secret-vault startup probe to complete.",
                HostCapabilitySupportLevel.Unsupported,
                HostCapabilityImplementationRegistration.Registered,
                implementationId: null,
                implementationVersion,
                HostCapabilityExecutionBoundary.OperatingSystem,
                profile,
                observedAtUtc);
        }

        if (!probe.IsAvailable)
        {
            HostCapabilityAvailability availability = probe.Availability switch
            {
                SecretVaultAvailability.UnsupportedPlatform => HostCapabilityAvailability.Unsupported,
                SecretVaultAvailability.InvalidConfiguration or
                    SecretVaultAvailability.InsecureConfiguration => HostCapabilityAvailability.Misconfigured,
                _ => HostCapabilityAvailability.Unavailable
            };
            HostCapabilityReasonCode reason = availability == HostCapabilityAvailability.Misconfigured
                ? HostCapabilityReasonCode.InvalidConfiguration
                : availability == HostCapabilityAvailability.Unsupported
                    ? HostCapabilityReasonCode.UnsupportedByProfile
                    : HostCapabilityReasonCode.DependencyUnavailable;

            return Descriptor(
                HostCapabilityId.SecretVault,
                HostCapabilityCriticality.Mandatory,
                availability,
                reason,
                ResolveSecretVaultRemediation(probe.Availability),
                HostCapabilitySupportLevel.Unsupported,
                HostCapabilityImplementationRegistration.Registered,
                probe.Provider.ToString(),
                implementationVersion,
                ResolveSecretBoundary(probe.Provider),
                profile,
                observedAtUtc);
        }

        bool actualMacValidationDeferred =
            probe.Provider == SecretVaultProviderKind.MacOsKeychain &&
            !profile.ActualHostSupportVerified;
        return Descriptor(
            HostCapabilityId.SecretVault,
            HostCapabilityCriticality.Mandatory,
            HostCapabilityAvailability.Available,
            actualMacValidationDeferred
                ? HostCapabilityReasonCode.ActualHostValidationDeferred
                : HostCapabilityReasonCode.Ready,
            actualMacValidationDeferred
                ? "Complete MACOS-KEYCHAIN-VALIDATION-001 before claiming verified macOS Keychain support."
                : NoRemediation,
            actualMacValidationDeferred
                ? HostCapabilitySupportLevel.ActualHostUnverified
                : ResolveSecretSupportLevel(probe.ProtectionLevel),
            HostCapabilityImplementationRegistration.Registered,
            probe.Provider.ToString(),
            implementationVersion,
            ResolveSecretBoundary(probe.Provider),
            profile,
            observedAtUtc);
    }

    private static HostCapabilityDescriptor ProjectDesktopCapability(
        HostCapabilityId id,
        ResolvedRuntimeHostProfile profile,
        bool available,
        string? implementationVersion,
        DateTimeOffset observedAtUtc)
    {
        if (!profile.IsInteractive)
        {
            return Descriptor(
                id,
                HostCapabilityCriticality.Optional,
                HostCapabilityAvailability.Unsupported,
                HostCapabilityReasonCode.DisabledByProfile,
                "Use an interactive runtime host profile to enable desktop features.",
                HostCapabilitySupportLevel.Unsupported,
                HostCapabilityImplementationRegistration.Registered,
                FileToolsImplementationId,
                implementationVersion,
                HostCapabilityExecutionBoundary.OperatingSystem,
                profile,
                observedAtUtc);
        }

        return Descriptor(
            id,
            HostCapabilityCriticality.Optional,
            available ? HostCapabilityAvailability.Available : HostCapabilityAvailability.Unavailable,
            available ? HostCapabilityReasonCode.Ready : HostCapabilityReasonCode.DependencyUnavailable,
            available ? NoRemediation : FileToolsRemediation,
            available ? HostCapabilitySupportLevel.Stable : HostCapabilitySupportLevel.Unsupported,
            HostCapabilityImplementationRegistration.Registered,
            FileToolsImplementationId,
            implementationVersion,
            HostCapabilityExecutionBoundary.OperatingSystem,
            profile,
            observedAtUtc);
    }

    private static HostCapabilityDescriptor ProjectAbsentOptionalCapability(
        HostCapabilityId id,
        ResolvedRuntimeHostProfile profile,
        string interactiveRemediation,
        HostCapabilityExecutionBoundary boundary,
        DateTimeOffset observedAtUtc)
    {
        bool disabledByProfile = !profile.IsInteractive;
        return Descriptor(
            id,
            HostCapabilityCriticality.Optional,
            disabledByProfile
                ? HostCapabilityAvailability.Unsupported
                : HostCapabilityAvailability.Unavailable,
            disabledByProfile
                ? HostCapabilityReasonCode.DisabledByProfile
                : HostCapabilityReasonCode.DependencyUnavailable,
            disabledByProfile
                ? "Use an interactive runtime host profile when this optional capability is required."
                : interactiveRemediation,
            HostCapabilitySupportLevel.Unsupported,
            HostCapabilityImplementationRegistration.NotRegistered,
            implementationId: null,
            implementationVersion: null,
            boundary,
            profile,
            observedAtUtc);
    }

    private static HostCapabilityDescriptor Descriptor(
        HostCapabilityId id,
        HostCapabilityCriticality criticality,
        HostCapabilityAvailability availability,
        HostCapabilityReasonCode reasonCode,
        string remediation,
        HostCapabilitySupportLevel supportLevel,
        HostCapabilityImplementationRegistration implementationRegistration,
        string? implementationId,
        string? implementationVersion,
        HostCapabilityExecutionBoundary boundary,
        ResolvedRuntimeHostProfile profile,
        DateTimeOffset observedAtUtc)
        => new(
            id,
            criticality,
            availability,
            reasonCode,
            remediation,
            supportLevel,
            implementationRegistration,
            implementationId,
            NormalizeVersion(implementationVersion),
            boundary,
            profile.Kind,
            observedAtUtc);

    private static string? NormalizeVersion(string? version)
        => string.IsNullOrWhiteSpace(version) ? null : version.Trim();

    private static HostCapabilityReasonCode ResolvePathReason(PathFoundationReadinessReason reason)
        => reason switch
        {
            PathFoundationReadinessReason.AccessDenied => HostCapabilityReasonCode.PermissionDenied,
            PathFoundationReadinessReason.UnsafePath => HostCapabilityReasonCode.UnsafePath,
            PathFoundationReadinessReason.IoFailure => HostCapabilityReasonCode.IoFailure,
            _ => HostCapabilityReasonCode.InvalidConfiguration
        };

    private static string ResolvePathRemediation(
        HostCapabilityId id,
        PathFoundationReadinessReason reason)
    {
        if (reason == PathFoundationReadinessReason.AccessDenied)
        {
            return "Grant the current operating-system account access to the configured application purpose roots.";
        }

        return id == HostCapabilityId.ControlPlanePaths
            ? "Correct the configured application purpose roots before startup."
            : "Restore safe native filesystem access for the configured application purpose roots.";
    }

    private static string ResolveSecretVaultRemediation(SecretVaultAvailability availability)
        => availability switch
        {
            SecretVaultAvailability.UnsupportedPlatform =>
                "Select a secret-vault provider supported by the resolved runtime host profile.",
            SecretVaultAvailability.DependencyMissing =>
                "Install or configure the selected secret-vault dependency, or explicitly choose the basic local provider.",
            SecretVaultAvailability.SessionUnavailable =>
                "Start the required interactive secret service, or explicitly choose a headless or basic local provider.",
            SecretVaultAvailability.Locked =>
                "Unlock the selected operating-system secret store before startup.",
            SecretVaultAvailability.InvalidConfiguration =>
                "Correct the selected secret-vault configuration before startup.",
            SecretVaultAvailability.InsecureConfiguration =>
                "Select an allowed production provider or explicitly opt into a development-only provider in Development.",
            _ => "Restore the selected secret-vault dependency before startup."
        };

    private static HostCapabilitySupportLevel ResolveSecretSupportLevel(
        SecretVaultProtectionLevel protectionLevel)
        => protectionLevel switch
        {
            SecretVaultProtectionLevel.Strong => HostCapabilitySupportLevel.Stable,
            SecretVaultProtectionLevel.BasicLocal => HostCapabilitySupportLevel.BasicLocal,
            SecretVaultProtectionLevel.DevelopmentOnly => HostCapabilitySupportLevel.DevelopmentOnly,
            _ => HostCapabilitySupportLevel.Unsupported
        };

    private static HostCapabilityExecutionBoundary ResolveSecretBoundary(
        SecretVaultProviderKind provider)
        => provider switch
        {
            SecretVaultProviderKind.AzureKeyVault or SecretVaultProviderKind.HashiCorp =>
                HostCapabilityExecutionBoundary.ExternalService,
            SecretVaultProviderKind.LinuxSecretService =>
                HostCapabilityExecutionBoundary.ExternalProcess,
            SecretVaultProviderKind.InMemory =>
                HostCapabilityExecutionBoundary.ManagedProcess,
            _ => HostCapabilityExecutionBoundary.OperatingSystem
        };
}
