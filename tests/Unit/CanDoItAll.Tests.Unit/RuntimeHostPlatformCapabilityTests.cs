using CanDoItAll.Composition;
using CanDoItAll.FileTools.Desktop;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Security;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "UnixPortabilityCore")]
public sealed class RuntimeHostPlatformCapabilityTests
{
    private const string RepositoryRootEnvironmentVariable = "CANDOITALL_TEST_REPOSITORY_ROOT";

    [Theory]
    [InlineData(RuntimeHostProfileKind.WindowsInteractive, RuntimeHostOperatingSystem.Windows, SecretVaultUsageProfile.Interactive, true)]
    [InlineData(RuntimeHostProfileKind.WindowsHeadless, RuntimeHostOperatingSystem.Windows, SecretVaultUsageProfile.Headless, false)]
    [InlineData(RuntimeHostProfileKind.LinuxInteractive, RuntimeHostOperatingSystem.Linux, SecretVaultUsageProfile.Interactive, true)]
    [InlineData(RuntimeHostProfileKind.LinuxHeadless, RuntimeHostOperatingSystem.Linux, SecretVaultUsageProfile.Headless, false)]
    [InlineData(RuntimeHostProfileKind.MacOsInteractive, RuntimeHostOperatingSystem.MacOs, SecretVaultUsageProfile.Interactive, true)]
    [InlineData(RuntimeHostProfileKind.MacOsHeadless, RuntimeHostOperatingSystem.MacOs, SecretVaultUsageProfile.Headless, false)]
    public void Resolve_explicit_profile_requires_matching_host_and_usage(
        RuntimeHostProfileKind profile,
        RuntimeHostOperatingSystem operatingSystem,
        SecretVaultUsageProfile usageProfile,
        bool expectedInteractive)
    {
        var resolved = RuntimeHostProfileResolver.Resolve(
            new RuntimeHostProfileOptions { Profile = profile },
            usageProfile,
            new RuntimeHostFacts(operatingSystem, IsDevelopment: false));

        Assert.Equal(profile, resolved.Kind);
        Assert.Equal(operatingSystem, resolved.OperatingSystem);
        Assert.Equal(expectedInteractive, resolved.IsInteractive);
        Assert.False(resolved.IsTest);
    }

    [Fact]
    public void Resolve_explicit_profile_rejects_foreign_host()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            RuntimeHostProfileResolver.Resolve(
                new RuntimeHostProfileOptions { Profile = RuntimeHostProfileKind.WindowsInteractive },
                SecretVaultUsageProfile.Interactive,
                new RuntimeHostFacts(RuntimeHostOperatingSystem.Linux, IsDevelopment: false)));

        Assert.Contains("does not match", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_test_profile_requires_development_environment()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RuntimeHostProfileResolver.Resolve(
                new RuntimeHostProfileOptions { Profile = RuntimeHostProfileKind.Test },
                SecretVaultUsageProfile.Headless,
                new RuntimeHostFacts(RuntimeHostOperatingSystem.Linux, IsDevelopment: false)));

        ResolvedRuntimeHostProfile resolved = RuntimeHostProfileResolver.Resolve(
            new RuntimeHostProfileOptions { Profile = RuntimeHostProfileKind.Test },
            SecretVaultUsageProfile.Headless,
            new RuntimeHostFacts(RuntimeHostOperatingSystem.Linux, IsDevelopment: true));

        Assert.True(resolved.IsTest);
        Assert.False(resolved.IsInteractive);
    }

    [Fact]
    public void Project_optional_unavailability_does_not_block_ready_headless_core()
    {
        ResolvedRuntimeHostProfile profile = Resolve(
            RuntimeHostProfileKind.LinuxHeadless,
            RuntimeHostOperatingSystem.Linux,
            SecretVaultUsageProfile.Headless);
        var snapshot = Project(
            profile,
            SecretVaultProbeResult.Available(
                SecretVaultProviderKind.LocalUserFile,
                SecretVaultProtectionLevel.BasicLocal,
                "Protected from other operating-system users; same-user processes remain in scope."),
            desktopFileOpenAvailable: false);

        Assert.True(snapshot.IsReady);
        Assert.All(
            snapshot.Capabilities.Where(capability => capability.Criticality == HostCapabilityCriticality.Mandatory),
            capability => Assert.Equal(HostCapabilityAvailability.Available, capability.Availability));
        Assert.Contains(
            snapshot.Capabilities,
            capability => capability.Id == HostCapabilityId.DesktopFileOpen &&
                capability.Criticality == HostCapabilityCriticality.Optional &&
                capability.Availability == HostCapabilityAvailability.Unsupported);
    }

    [Fact]
    public void Project_unavailable_mandatory_vault_blocks_readiness_and_redacts_provider_detail()
    {
        ResolvedRuntimeHostProfile profile = Resolve(
            RuntimeHostProfileKind.LinuxInteractive,
            RuntimeHostOperatingSystem.Linux,
            SecretVaultUsageProfile.Interactive);
        var snapshot = Project(
            profile,
            new SecretVaultProbeResult(
                SecretVaultProviderKind.LinuxSecretService,
                SecretVaultAvailability.SessionUnavailable,
                "Open /home/alice/private/vault and use secret: do-not-copy"),
            desktopFileOpenAvailable: false);

        Assert.False(snapshot.IsReady);
        HostCapabilityDescriptor vault = Assert.Single(
            snapshot.Capabilities,
            capability => capability.Id == HostCapabilityId.SecretVault);
        Assert.Equal(HostCapabilityAvailability.Unavailable, vault.Availability);
        Assert.DoesNotContain("/home", vault.Remediation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret:", vault.Remediation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_mac_keychain_remains_actual_host_unverified_until_deferred_run_passes()
    {
        ResolvedRuntimeHostProfile profile = Resolve(
            RuntimeHostProfileKind.MacOsInteractive,
            RuntimeHostOperatingSystem.MacOs,
            SecretVaultUsageProfile.Interactive);
        var snapshot = Project(
            profile,
            SecretVaultProbeResult.Available(SecretVaultProviderKind.MacOsKeychain),
            desktopFileOpenAvailable: true);

        HostCapabilityDescriptor vault = Assert.Single(
            snapshot.Capabilities,
            capability => capability.Id == HostCapabilityId.SecretVault);
        Assert.True(snapshot.IsReady);
        Assert.Equal(HostCapabilitySupportLevel.ActualHostUnverified, vault.SupportLevel);
        Assert.Equal(HostCapabilityReasonCode.ActualHostValidationDeferred, vault.ReasonCode);
    }

    [Theory]
    [InlineData(RuntimeHostProfileKind.WindowsInteractive, RuntimeHostOperatingSystem.Windows, SecretVaultUsageProfile.Interactive, SecretVaultProviderKind.Dpapi, SecretVaultProtectionLevel.Strong, HostCapabilitySupportLevel.Stable)]
    [InlineData(RuntimeHostProfileKind.WindowsHeadless, RuntimeHostOperatingSystem.Windows, SecretVaultUsageProfile.Headless, SecretVaultProviderKind.Dpapi, SecretVaultProtectionLevel.Strong, HostCapabilitySupportLevel.Stable)]
    [InlineData(RuntimeHostProfileKind.LinuxInteractive, RuntimeHostOperatingSystem.Linux, SecretVaultUsageProfile.Interactive, SecretVaultProviderKind.LinuxSecretService, SecretVaultProtectionLevel.Strong, HostCapabilitySupportLevel.Stable)]
    [InlineData(RuntimeHostProfileKind.LinuxHeadless, RuntimeHostOperatingSystem.Linux, SecretVaultUsageProfile.Headless, SecretVaultProviderKind.LocalUserFile, SecretVaultProtectionLevel.BasicLocal, HostCapabilitySupportLevel.BasicLocal)]
    [InlineData(RuntimeHostProfileKind.MacOsInteractive, RuntimeHostOperatingSystem.MacOs, SecretVaultUsageProfile.Interactive, SecretVaultProviderKind.MacOsKeychain, SecretVaultProtectionLevel.Strong, HostCapabilitySupportLevel.ActualHostUnverified)]
    [InlineData(RuntimeHostProfileKind.MacOsHeadless, RuntimeHostOperatingSystem.MacOs, SecretVaultUsageProfile.Headless, SecretVaultProviderKind.ExternalWrappingKeyFile, SecretVaultProtectionLevel.Strong, HostCapabilitySupportLevel.Stable)]
    public void Project_profile_matrix_reports_selected_vault_without_inferring_authority(
        RuntimeHostProfileKind profileKind,
        RuntimeHostOperatingSystem operatingSystem,
        SecretVaultUsageProfile usageProfile,
        SecretVaultProviderKind provider,
        SecretVaultProtectionLevel protectionLevel,
        HostCapabilitySupportLevel expectedSupportLevel)
    {
        ResolvedRuntimeHostProfile profile = Resolve(profileKind, operatingSystem, usageProfile);
        HostCapabilitySnapshot snapshot = Project(
            profile,
            SecretVaultProbeResult.Available(provider, protectionLevel),
            desktopFileOpenAvailable: profile.IsInteractive);

        HostCapabilityDescriptor vault = Assert.Single(
            snapshot.Capabilities,
            capability => capability.Id == HostCapabilityId.SecretVault);
        Assert.True(snapshot.IsReady);
        Assert.Equal(provider.ToString(), vault.ImplementationId);
        Assert.Equal("2.3.4", vault.ImplementationVersion);
        Assert.Equal(HostCapabilityImplementationRegistration.Registered, vault.ImplementationRegistration);
        Assert.Equal(expectedSupportLevel, vault.SupportLevel);
    }

    [Fact]
    public void Project_test_profile_is_development_only()
    {
        ResolvedRuntimeHostProfile profile = RuntimeHostProfileResolver.Resolve(
            new RuntimeHostProfileOptions { Profile = RuntimeHostProfileKind.Test },
            SecretVaultUsageProfile.Headless,
            new RuntimeHostFacts(RuntimeHostOperatingSystem.Linux, IsDevelopment: true));
        HostCapabilitySnapshot snapshot = Project(
            profile,
            SecretVaultProbeResult.Available(
                SecretVaultProviderKind.InMemory,
                SecretVaultProtectionLevel.DevelopmentOnly),
            desktopFileOpenAvailable: false);

        HostCapabilityDescriptor vault = Assert.Single(
            snapshot.Capabilities,
            capability => capability.Id == HostCapabilityId.SecretVault);
        Assert.True(snapshot.IsReady);
        Assert.Equal(HostCapabilitySupportLevel.DevelopmentOnly, vault.SupportLevel);
    }

    [Fact]
    public void Project_emits_each_capability_exactly_once()
    {
        ResolvedRuntimeHostProfile profile = Resolve(
            RuntimeHostProfileKind.WindowsInteractive,
            RuntimeHostOperatingSystem.Windows,
            SecretVaultUsageProfile.Interactive);
        var snapshot = Project(
            profile,
            SecretVaultProbeResult.Available(SecretVaultProviderKind.Dpapi),
            desktopFileOpenAvailable: true);

        Assert.Equal(Enum.GetValues<HostCapabilityId>().Length, snapshot.Capabilities.Count);
        Assert.Equal(snapshot.Capabilities.Count, snapshot.Capabilities.Select(capability => capability.Id).Distinct().Count());
    }

    [Fact]
    public void Composition_registers_exactly_one_mandatory_contract_and_at_most_one_desktop_adapter()
    {
        string contentRoot = Path.Combine(Path.GetTempPath(), $"candoitall-a05-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:KeyProtection:Provider"] = "UnprotectedDevelopment"
                })
                .Build();
            var environment = new TestHostEnvironment(contentRoot, "CanDoItAll.A05.Tests");
            var services = new ServiceCollection();

            services.AddCanDoItAllInfrastructure(configuration, environment, []);
            services.AddCanDoItAllRuntimeModules(configuration, environment, contentRoot);

            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IControlPlanePathResolver));
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPhysicalFileSystemPathPolicyFactory));
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPathFoundationReadinessProbe));
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ISecretVault));
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ResolvedRuntimeHostProfile));
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRuntimeDeploymentSupportProvider));
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostCapabilitySnapshotProvider));
            Assert.True(services.Count(descriptor => descriptor.ServiceType == typeof(IDesktopFileLauncher)) <= 1);

            Type[] hostedServiceTypes = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .Select(descriptor => descriptor.ImplementationType)
                .OfType<Type>()
                .ToArray();
            Assert.True(
                Array.IndexOf(hostedServiceTypes, typeof(SecretVaultStartupValidator)) <
                Array.IndexOf(hostedServiceTypes, typeof(HostCapabilityStartupValidator)));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Headless_runtime_profile_overrides_desktop_feature_enablement()
    {
        string contentRoot = Path.Combine(Path.GetTempPath(), $"candoitall-b05-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        try
        {
            string profile = OperatingSystem.IsWindows()
                ? nameof(RuntimeHostProfileKind.WindowsHeadless)
                : OperatingSystem.IsLinux()
                    ? nameof(RuntimeHostProfileKind.LinuxHeadless)
                    : nameof(RuntimeHostProfileKind.MacOsHeadless);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileTools:DesktopLaunch:Enabled"] = "true",
                    ["RuntimeHost:Profile"] = profile,
                    ["SecretVault:UsageProfile"] = nameof(SecretVaultUsageProfile.Headless)
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddCanDoItAllFileToolsIntegration();
            services.AddRuntimeHostPlatformComposition(
                configuration,
                new TestHostEnvironment(contentRoot, "CanDoItAll.B05.Tests"));
            using ServiceProvider provider = services.BuildServiceProvider();

            FileToolsDesktopLaunchOptions options = provider
                .GetRequiredService<IOptions<FileToolsDesktopLaunchOptions>>()
                .Value;

            Assert.True(options.Enabled);
            Assert.False(options.HostProfileAllowsDesktop);
            Assert.False(provider.GetRequiredService<IDesktopFileLauncher>().IsAvailable);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Startup_validator_rejects_only_unavailable_mandatory_capabilities()
    {
        ResolvedRuntimeHostProfile profile = Resolve(
            RuntimeHostProfileKind.LinuxHeadless,
            RuntimeHostOperatingSystem.Linux,
            SecretVaultUsageProfile.Headless);
        HostCapabilitySnapshot blocked = HostCapabilitySnapshotProjector.Create(
            profile,
            ReadyPathFoundation(),
            secretVaultProbe: null,
            infrastructureImplementationVersion: "1.2.3",
            securityImplementationVersion: "2.3.4",
            desktopFileOpenAvailable: false,
            fileToolsImplementationVersion: "0.1.18",
            observedAtUtc: DateTimeOffset.UnixEpoch);
        HostCapabilitySnapshot ready = Project(
            profile,
            SecretVaultProbeResult.Available(
                SecretVaultProviderKind.LocalUserFile,
                SecretVaultProtectionLevel.BasicLocal),
            desktopFileOpenAvailable: false);

        var blockedValidator = new HostCapabilityStartupValidator(
            new FixedSnapshotProvider(blocked),
            NullLogger<HostCapabilityStartupValidator>.Instance);
        await Assert.ThrowsAsync<HostCapabilityUnavailableException>(() =>
            blockedValidator.StartAsync(CancellationToken.None));

        var readyValidator = new HostCapabilityStartupValidator(
            new FixedSnapshotProvider(ready),
            NullLogger<HostCapabilityStartupValidator>.Instance);
        await readyValidator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Owner_reported_path_failure_blocks_startup_without_disclosing_path_details()
    {
        ResolvedRuntimeHostProfile profile = Resolve(
            RuntimeHostProfileKind.LinuxHeadless,
            RuntimeHostOperatingSystem.Linux,
            SecretVaultUsageProfile.Headless);
        var pathReadiness = new PathFoundationReadinessSnapshot(
            new PathCapabilityReadiness(
                PathFoundationReadinessState.Unavailable,
                PathFoundationReadinessReason.AccessDenied),
            new PathCapabilityReadiness(
                PathFoundationReadinessState.Ready,
                PathFoundationReadinessReason.Ready),
            []);
        HostCapabilitySnapshot snapshot = HostCapabilitySnapshotProjector.Create(
            profile,
            pathReadiness,
            SecretVaultProbeResult.Available(
                SecretVaultProviderKind.LocalUserFile,
                SecretVaultProtectionLevel.BasicLocal),
            infrastructureImplementationVersion: "1.2.3",
            securityImplementationVersion: "2.3.4",
            desktopFileOpenAvailable: false,
            fileToolsImplementationVersion: "0.1.18",
            observedAtUtc: DateTimeOffset.UnixEpoch);

        HostCapabilityDescriptor paths = Assert.Single(
            snapshot.Capabilities,
            capability => capability.Id == HostCapabilityId.ControlPlanePaths);
        Assert.False(snapshot.IsReady);
        Assert.Equal(HostCapabilityReasonCode.PermissionDenied, paths.ReasonCode);
        Assert.DoesNotContain("/", paths.Remediation, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", paths.Remediation, StringComparison.Ordinal);

        var validator = new HostCapabilityStartupValidator(
            new FixedSnapshotProvider(snapshot),
            NullLogger<HostCapabilityStartupValidator>.Instance);
        await Assert.ThrowsAsync<HostCapabilityUnavailableException>(() =>
            validator.StartAsync(CancellationToken.None));
    }

    [Fact]
    public void Project_distinguishes_registered_implementation_identity_from_version()
    {
        ResolvedRuntimeHostProfile profile = Resolve(
            RuntimeHostProfileKind.WindowsInteractive,
            RuntimeHostOperatingSystem.Windows,
            SecretVaultUsageProfile.Interactive);
        HostCapabilitySnapshot snapshot = Project(
            profile,
            SecretVaultProbeResult.Available(SecretVaultProviderKind.Dpapi),
            desktopFileOpenAvailable: true);

        HostCapabilityDescriptor vault = Find(snapshot, HostCapabilityId.SecretVault);
        Assert.Equal(HostCapabilityImplementationRegistration.Registered, vault.ImplementationRegistration);
        Assert.Equal(nameof(SecretVaultProviderKind.Dpapi), vault.ImplementationId);
        Assert.Equal("2.3.4", vault.ImplementationVersion);

        HostCapabilityDescriptor terminal = Find(snapshot, HostCapabilityId.InteractiveTerminal);
        Assert.Equal(HostCapabilityImplementationRegistration.NotRegistered, terminal.ImplementationRegistration);
        Assert.Null(terminal.ImplementationId);
        Assert.Null(terminal.ImplementationVersion);
    }

    [Fact]
    public void Project_omits_unknown_implementation_versions()
    {
        ResolvedRuntimeHostProfile profile = Resolve(
            RuntimeHostProfileKind.LinuxHeadless,
            RuntimeHostOperatingSystem.Linux,
            SecretVaultUsageProfile.Headless);
        HostCapabilitySnapshot snapshot = HostCapabilitySnapshotProjector.Create(
            profile,
            ReadyPathFoundation(),
            SecretVaultProbeResult.Available(
                SecretVaultProviderKind.LocalUserFile,
                SecretVaultProtectionLevel.BasicLocal),
            infrastructureImplementationVersion: " ",
            securityImplementationVersion: null,
            desktopFileOpenAvailable: false,
            fileToolsImplementationVersion: string.Empty,
            observedAtUtc: DateTimeOffset.UnixEpoch);

        Assert.All(snapshot.Capabilities, capability => Assert.Null(capability.ImplementationVersion));
        Assert.DoesNotContain(
            snapshot.Capabilities,
            capability => string.Equals(capability.ImplementationId, "unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Path_foundation_probe_reports_owner_failures_as_typed_non_sensitive_state()
    {
        var probe = new PathFoundationReadinessProbe(
            new ThrowingControlPlanePathResolver(),
            new FixedWorkspacePathResolver(Path.GetTempPath()),
            new PhysicalFileSystemPathPolicyFactory());

        PathFoundationReadinessSnapshot snapshot = probe.Probe();

        Assert.Equal(PathFoundationReadinessState.Unavailable, snapshot.ControlPlanePaths.State);
        Assert.Equal(PathFoundationReadinessReason.AccessDenied, snapshot.ControlPlanePaths.Reason);
        Assert.Equal(snapshot.ControlPlanePaths, snapshot.PhysicalFileSystem);
    }

    [Fact]
    public void Path_foundation_probe_validates_writable_owner_roots()
    {
        string root = Path.Combine(Path.GetTempPath(), $"candoitall-a05-path-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var probe = new PathFoundationReadinessProbe(
                new FixedControlPlanePathResolver(root),
                new FixedWorkspacePathResolver(root),
                new PhysicalFileSystemPathPolicyFactory());

            PathFoundationReadinessSnapshot snapshot = probe.Probe();

            Assert.Equal(PathFoundationReadinessState.Ready, snapshot.ControlPlanePaths.State);
            Assert.Equal(PathFoundationReadinessState.Ready, snapshot.PhysicalFileSystem.State);
            Assert.Equal(7, snapshot.PurposeRoots.Count);
            Assert.Equal(7, snapshot.PurposeRoots.Select(item => item.Purpose).Distinct().Count());
            Assert.All(snapshot.PurposeRoots, item =>
                Assert.Equal(PathFoundationReadinessState.Ready, item.State));
            Assert.Equal(
                ApplicationPurposeRootConfigurationSource.ActiveDatabaseProfile,
                Assert.Single(
                    snapshot.PurposeRoots,
                    item => item.Purpose == ApplicationPurposeRootKind.Workspace).ConfigurationSource);
            Assert.Equal(
                ApplicationPurposeRootConfigurationSource.DerivedFromControlPlaneRoot,
                Assert.Single(
                    snapshot.PurposeRoots,
                    item => item.Purpose == ApplicationPurposeRootKind.DatabaseProfiles).ConfigurationSource);
            Assert.All(
                snapshot.PurposeRoots.Where(item =>
                    item.Purpose is not ApplicationPurposeRootKind.Workspace and
                    not ApplicationPurposeRootKind.DatabaseProfiles),
                item => Assert.Equal(
                    ApplicationPurposeRootConfigurationSource.ExplicitConfiguration,
                    item.ConfigurationSource));
            Assert.Empty(Directory.EnumerateFiles(root, ".path-readiness-*"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Path_foundation_probe_checks_each_owner_root_not_only_runtime_temporary()
    {
        string root = Path.Combine(Path.GetTempPath(), $"candoitall-a05-all-root-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string occupiedPath = Path.Combine(root, "not-a-directory");
        File.WriteAllText(occupiedPath, "occupied");
        try
        {
            var probe = new PathFoundationReadinessProbe(
                new FixedControlPlanePathResolver(root, occupiedPath),
                new FixedWorkspacePathResolver(root),
                new PhysicalFileSystemPathPolicyFactory());

            PathFoundationReadinessSnapshot snapshot = probe.Probe();

            Assert.Equal(PathFoundationReadinessState.Unavailable, snapshot.ControlPlanePaths.State);
            Assert.Equal(PathFoundationReadinessReason.IoFailure, snapshot.ControlPlanePaths.Reason);
            Assert.Equal(snapshot.ControlPlanePaths, snapshot.PhysicalFileSystem);
            ApplicationPurposeRootReadiness databaseProfiles = Assert.Single(
                snapshot.PurposeRoots,
                item => item.Purpose == ApplicationPurposeRootKind.DatabaseProfiles);
            Assert.Equal(PathFoundationReadinessState.Unavailable, databaseProfiles.State);
            Assert.All(
                snapshot.PurposeRoots.Where(item => item.Purpose != ApplicationPurposeRootKind.DatabaseProfiles),
                item => Assert.Equal(PathFoundationReadinessState.Ready, item.State));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Architecture_has_no_broad_platform_service_or_process_semantic_os_branch()
    {
        string root = FindRepositoryRoot();
        string sourceRoot = Path.Combine(root, "src");
        string broadPlatformContract = "I" + "PlatformService";
        string[] broadPlatformViolations = EnumerateSourceFiles(sourceRoot)
            .Where(path => File.ReadAllText(path).Contains(broadPlatformContract, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        Assert.Empty(broadPlatformViolations);

        string[] processRoots =
        [
            Path.Combine(sourceRoot, "Processes"),
            Path.Combine(sourceRoot, "Modules", "CanDoItAll.Modules.Processes")
        ];
        string[] processOsBranchViolations = processRoots
            .SelectMany(EnumerateSourceFiles)
            .Where(path => !path.Contains(
                Path.Combine("Services", "RuntimeIntegration", "Drivers"),
                StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains("OperatingSystem.Is", StringComparison.Ordinal) ||
                    source.Contains("RuntimeInformation.IsOSPlatform", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        Assert.Empty(processOsBranchViolations);
    }

    [Fact]
    public void Architecture_limits_host_os_branches_to_reviewed_owners()
    {
        string root = FindRepositoryRoot();
        string sourceRoot = Path.Combine(root, "src");
        string[] expected =
        [
            "src/App/CanDoItAll.Composition/RuntimeHostProfiles.cs",
            "src/Foundation/CanDoItAll.Git/GitRepositoryPath.cs",
            "src/Foundation/CanDoItAll.Infrastructure/Common/HostBoundPathPolicy.cs",
            "src/Foundation/CanDoItAll.Infrastructure/Common/PhysicalPathSyntaxPolicy.cs",
            "src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DataProtectionKeyRingProtection.cs",
            "src/Foundation/CanDoItAll.Infrastructure/FileSystem/DurableFileWriter.cs",
            "src/Foundation/CanDoItAll.Infrastructure/Storage/ExternalTargetPathRegistry.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/ProjectWorkspaceScopePolicy.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceExecutableLocator.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/ManagedProjectMediaPath.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathAliasSession.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathPolicy.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePhysicalPathSyntaxPolicy.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalHostPlatform.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs",
            "src/MAF/Tools/CanDoItAll.Tools.Documents/Spreadsheets/ClosedXmlSpreadsheetDocumentService.cs",
            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkflowSourceFileResolver.cs",
            "src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs",
            "src/Modules/CanDoItAll.Modules.Security/NativeSecretVaults.cs",
            "src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs",
            "src/Modules/CanDoItAll.Modules.Workbench/CrossModule/ProjectManagedStorageDeletion.cs",
            "src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureExternalAssetSourcePolicy.cs",
            "src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureOutputRootAuthorityResolver.cs",
            "src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeAdapters.cs",
            "src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerHostToolService.cs"
        ];
        string[] actual = EnumerateSourceFiles(sourceRoot)
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains("OperatingSystem.Is", StringComparison.Ordinal) ||
                    source.Contains("RuntimeInformation.IsOSPlatform", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Architecture_limits_native_process_starts_to_reviewed_runtime_owners()
    {
        string root = FindRepositoryRoot();
        string[] expectedMainRepositoryOwners =
        [
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs",
            "src/Modules/CanDoItAll.Modules.Security/NativeSecretVaults.cs",
            "src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeAdapters.cs"
        ];
        string[] actualMainRepositoryOwners = EnumerateSourceFiles(Path.Combine(root, "src"))
            .Where(ContainsNativeProcessStart)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedMainRepositoryOwners, actualMainRepositoryOwners);

        string fileToolsRoot = Path.GetFullPath(Path.Combine(root, "..", "CanDoItAll.FileTools"));
        string[] expectedFileToolsOwners =
        [
            "src/CanDoItAll.FileTools.Desktop/DesktopFileLauncher.cs"
        ];
        string[] actualFileToolsOwners = EnumerateSourceFiles(Path.Combine(fileToolsRoot, "src"))
            .Where(ContainsNativeProcessStart)
            .Select(path => Path.GetRelativePath(fileToolsRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedFileToolsOwners, actualFileToolsOwners);

        static bool ContainsNativeProcessStart(string path)
        {
            string source = File.ReadAllText(path);
            return source.Contains("Process.Start(", StringComparison.Ordinal) ||
                System.Text.RegularExpressions.Regex.IsMatch(
                    source,
                    @"\bnew\s+(?:System\.Diagnostics\.)?Process\s*[\{\(]",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }
    }

    [Fact]
    public void Production_configuration_has_no_windows_specific_physical_path_defaults()
    {
        string root = FindRepositoryRoot();
        string[] violations = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.json", SearchOption.AllDirectories)
            .Where(path =>
            {
                string fileName = Path.GetFileName(path);
                return fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("launchSettings.json", StringComparison.OrdinalIgnoreCase);
            })
            .Where(path => !IsBuildArtifact(path))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains("%LOCALAPPDATA%", StringComparison.OrdinalIgnoreCase) ||
                    System.Text.RegularExpressions.Regex.IsMatch(source, @"(?i)(?<![A-Z0-9])[A-Z]:\\\\");
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Launchd_system_daemon_template_requires_explicit_service_identity()
    {
        string root = FindRepositoryRoot();
        string template = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "install",
            "unix",
            "com.candoitall.web.plist.in"));

        Assert.Contains("<key>UserName</key>", template, StringComparison.Ordinal);
        Assert.Contains("<string>@@SERVICE_USER@@</string>", template, StringComparison.Ordinal);
        Assert.Contains("<key>GroupName</key>", template, StringComparison.Ordinal);
        Assert.Contains("<string>@@SERVICE_GROUP@@</string>", template, StringComparison.Ordinal);
        Assert.DoesNotContain("<string>root</string>", template, StringComparison.OrdinalIgnoreCase);
    }

    private static ResolvedRuntimeHostProfile Resolve(
        RuntimeHostProfileKind profile,
        RuntimeHostOperatingSystem operatingSystem,
        SecretVaultUsageProfile usageProfile)
        => RuntimeHostProfileResolver.Resolve(
            new RuntimeHostProfileOptions { Profile = profile },
            usageProfile,
            new RuntimeHostFacts(operatingSystem, IsDevelopment: false));

    private static HostCapabilitySnapshot Project(
        ResolvedRuntimeHostProfile profile,
        SecretVaultProbeResult secretVaultProbe,
        bool desktopFileOpenAvailable)
        => HostCapabilitySnapshotProjector.Create(
            profile,
            ReadyPathFoundation(),
            secretVaultProbe,
            infrastructureImplementationVersion: "1.2.3",
            securityImplementationVersion: "2.3.4",
            desktopFileOpenAvailable,
            fileToolsImplementationVersion: "0.1.18",
            observedAtUtc: DateTimeOffset.UnixEpoch);

    private static PathFoundationReadinessSnapshot ReadyPathFoundation()
    {
        var ready = new PathCapabilityReadiness(
            PathFoundationReadinessState.Ready,
            PathFoundationReadinessReason.Ready);
        return new PathFoundationReadinessSnapshot(ready, ready, []);
    }

    private static HostCapabilityDescriptor Find(
        HostCapabilitySnapshot snapshot,
        HostCapabilityId id)
        => Assert.Single(snapshot.Capabilities, capability => capability.Id == id);

    private static string FindRepositoryRoot()
    {
        string? configuredRoot = Environment.GetEnvironmentVariable(RepositoryRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            string resolvedRoot = Path.GetFullPath(configuredRoot);
            if (!File.Exists(Path.Combine(resolvedRoot, "CanDoItAll.slnx")))
            {
                throw new DirectoryNotFoundException(
                    $"{RepositoryRootEnvironmentVariable} does not identify the CanDoItAll repository root.");
            }

            return resolvedRoot;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the CanDoItAll repository root.");
    }

    private static bool IsBuildArtifact(string path)
        => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment =>
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.TryPop(out string? directory))
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }

            foreach (string child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (!IsBuildArtifact(child))
                {
                    pending.Push(child);
                }
            }
        }
    }

    private sealed class FixedSnapshotProvider(HostCapabilitySnapshot snapshot)
        : IHostCapabilitySnapshotProvider
    {
        public HostCapabilitySnapshot GetSnapshot() => snapshot;
    }

    private sealed class ThrowingControlPlanePathResolver : IControlPlanePathResolver
    {
        public string ResolveRootPath() => throw Denied();
        public string ResolveDatabaseProfilesRootPath() => throw Denied();
        public string ResolveCatalogFilePath() => throw Denied();
        public string ResolveActiveProfileStateFilePath() => throw Denied();
        public string ResolveFileApplicationPreferencesFilePath() => throw Denied();
        public string ResolveDataProtectionKeysPath() => throw Denied();
        public string ResolveStateRootPath() => throw Denied();
        public string ResolveLogsRootPath() => throw Denied();
        public string ResolveRuntimeTemporaryRootPath() => throw Denied();

        private static UnauthorizedAccessException Denied()
            => new("Sensitive path detail must not cross the readiness boundary.");
    }

    private sealed class FixedControlPlanePathResolver(
        string root,
        string? databaseProfilesRoot = null) :
        IControlPlanePathResolver,
        IApplicationPurposeRootConfigurationSource
    {
        public string ResolveRootPath() => root;
        public string ResolveDatabaseProfilesRootPath() => databaseProfilesRoot ?? root;
        public string ResolveCatalogFilePath() => Path.Combine(root, "catalog.json");
        public string ResolveActiveProfileStateFilePath() => Path.Combine(root, "active.json");
        public string ResolveFileApplicationPreferencesFilePath() => Path.Combine(root, "preferences.json");
        public string ResolveDataProtectionKeysPath() => root;
        public string ResolveStateRootPath() => root;
        public string ResolveLogsRootPath() => root;
        public string ResolveRuntimeTemporaryRootPath() => root;

        public ApplicationPurposeRootConfigurationSource GetConfigurationSource(
            ApplicationPurposeRootKind purpose)
            => purpose == ApplicationPurposeRootKind.DatabaseProfiles
                ? ApplicationPurposeRootConfigurationSource.DerivedFromControlPlaneRoot
                : ApplicationPurposeRootConfigurationSource.ExplicitConfiguration;
    }

    private sealed class FixedWorkspacePathResolver(string root) :
        IWorkspacePathResolver,
        IApplicationPurposeRootConfigurationSource
    {
        public string ResolveWorkspaceRoot() => root;
        public string ResolveManagedFilesRoot() => root;
        public string ResolveExportsRoot() => root;
        public string ResolveEvidenceRoot() => root;
        public string ResolveManagerArtifactsRoot() => root;

        public ApplicationPurposeRootConfigurationSource GetConfigurationSource(
            ApplicationPurposeRootKind purpose)
            => purpose == ApplicationPurposeRootKind.Workspace
                ? ApplicationPurposeRootConfigurationSource.ActiveDatabaseProfile
                : throw new ArgumentOutOfRangeException(nameof(purpose));
    }
}
