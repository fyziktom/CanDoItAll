using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Composition;
using Microsoft.Extensions.DependencyInjection;
using RuntimeCapabilitiesPage = CanDoItAll.Web.Components.Pages.RuntimeCapabilities;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class RuntimeCapabilitiesPageTests
{
    [Fact]
    public void Renders_optional_unavailability_without_blocking_core_readiness()
    {
        HostCapabilitySnapshot snapshot = CreateSnapshot(
            isReady: true,
            Descriptor(
                HostCapabilityId.InteractiveTerminal,
                HostCapabilityCriticality.Optional,
                HostCapabilityAvailability.Unavailable,
                HostCapabilityReasonCode.DependencyUnavailable,
                "Register a purpose-owned interactive terminal adapter for this host profile.",
                HostCapabilitySupportLevel.Unsupported,
                HostCapabilityImplementationRegistration.NotRegistered,
                implementationId: null,
                implementationVersion: null,
                HostCapabilityExecutionBoundary.ExternalProcess));

        using var context = CreateContext(snapshot);
        var cut = context.Render<RuntimeCapabilitiesPage>();

        Assert.Contains("Mandatory runtime capabilities are ready.", cut.Markup, StringComparison.Ordinal);
        var capability = cut.Find("[data-testid='runtime-capability-interactiveterminal']");
        Assert.Contains("Unavailable", capability.TextContent, StringComparison.Ordinal);
        Assert.Contains("Dependency Unavailable", capability.TextContent, StringComparison.Ordinal);
        Assert.Contains("External Process", capability.TextContent, StringComparison.Ordinal);
        Assert.Contains("Not Registered", capability.TextContent, StringComparison.Ordinal);
        Assert.Contains("Register a purpose-owned", capability.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Renders_basic_local_secret_support_without_claiming_strong_protection_or_leaking_paths()
    {
        HostCapabilitySnapshot snapshot = CreateSnapshot(
            isReady: true,
            Descriptor(
                HostCapabilityId.SecretVault,
                HostCapabilityCriticality.Mandatory,
                HostCapabilityAvailability.Available,
                HostCapabilityReasonCode.Ready,
                "None.",
                HostCapabilitySupportLevel.BasicLocal,
                HostCapabilityImplementationRegistration.Registered,
                nameof(Modules.Security.SecretVaultProviderKind.LocalUserFile),
                "1.0.0.0",
                HostCapabilityExecutionBoundary.OperatingSystem));

        using var context = CreateContext(snapshot);
        var cut = context.Render<RuntimeCapabilitiesPage>();
        var capability = cut.Find("[data-testid='runtime-capability-secretvault']");

        Assert.Contains("Basic Local", capability.TextContent, StringComparison.Ordinal);
        Assert.Contains("LocalUserFile 1.0.0.0", capability.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Strong", capability.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/home", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret:", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renders_deferred_actual_host_validation_as_visible_support_state()
    {
        HostCapabilitySnapshot snapshot = CreateSnapshot(
            isReady: true,
            Descriptor(
                HostCapabilityId.SecretVault,
                HostCapabilityCriticality.Mandatory,
                HostCapabilityAvailability.Available,
                HostCapabilityReasonCode.ActualHostValidationDeferred,
                "Complete MACOS-KEYCHAIN-VALIDATION-001 before claiming verified macOS Keychain support.",
                HostCapabilitySupportLevel.ActualHostUnverified,
                HostCapabilityImplementationRegistration.Registered,
                nameof(Modules.Security.SecretVaultProviderKind.MacOsKeychain),
                "1.0.0.0",
                HostCapabilityExecutionBoundary.OperatingSystem),
            RuntimeHostProfileKind.MacOsInteractive,
            RuntimeHostOperatingSystem.MacOs);

        using var context = CreateContext(snapshot);
        var cut = context.Render<RuntimeCapabilitiesPage>();
        var capability = cut.Find("[data-testid='runtime-capability-secretvault']");

        Assert.Contains("Actual Host Validation Deferred", capability.TextContent, StringComparison.Ordinal);
        Assert.Contains("Actual Host Unverified", capability.TextContent, StringComparison.Ordinal);
        Assert.Contains("MACOS-KEYCHAIN-VALIDATION-001", capability.TextContent, StringComparison.Ordinal);
    }

    private static BunitContext CreateContext(HostCapabilitySnapshot snapshot)
    {
        var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddScoped<AppToolbarState>();
        context.Services.AddSingleton<IHostCapabilitySnapshotProvider>(new FixedSnapshotProvider(snapshot));
        return context;
    }

    private static HostCapabilitySnapshot CreateSnapshot(
        bool isReady,
        HostCapabilityDescriptor capability,
        RuntimeHostProfileKind profile = RuntimeHostProfileKind.LinuxHeadless,
        RuntimeHostOperatingSystem operatingSystem = RuntimeHostOperatingSystem.Linux)
        => new(
            profile,
            operatingSystem,
            profile is RuntimeHostProfileKind.LinuxInteractive or RuntimeHostProfileKind.MacOsInteractive,
            isReady,
            DateTimeOffset.UnixEpoch,
            [],
            [capability]);

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
        HostCapabilityExecutionBoundary executionBoundary)
        => new(
            id,
            criticality,
            availability,
            reasonCode,
            remediation,
            supportLevel,
            implementationRegistration,
            implementationId,
            implementationVersion,
            executionBoundary,
            RuntimeHostProfileKind.LinuxHeadless,
            DateTimeOffset.UnixEpoch);

    private sealed class FixedSnapshotProvider(HostCapabilitySnapshot snapshot)
        : IHostCapabilitySnapshotProvider
    {
        public HostCapabilitySnapshot GetSnapshot() => snapshot;
    }
}
