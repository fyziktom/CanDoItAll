using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureNodeActionCapabilityResolverTests
{
    [Fact]
    public void Resolve_keeps_invalid_runtime_diagnostics_when_run_actions_are_unavailable()
    {
        const string diagnostic =
            "The configured projectPath directory contains no top-level .NET project file.";
        var node = CreateRuntimeNode();

        var result = ProjectStructureNodeActionCapabilityResolver.Resolve(
            node,
            new FailedRuntimeLauncher(diagnostic),
            new UnavailableLocalFileOpener(),
            ProjectStructureRuntimePathAuthorityMode.OperatorSelected);

        Assert.NotNull(result);
        Assert.False(result.CanRunNormally);
        Assert.False(result.CanRunAsAdministrator);
        Assert.Empty(result.Actions);
        Assert.Contains(result.Guidance, item => item.Contains(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_does_not_expose_runtime_capability_for_non_docker_infrastructure_nodes()
    {
        var node = CreateRuntimeNode() with
        {
            ObjectType = ProjectObjectType.Infrastructure,
            ObjectSubtype = "domain",
            MetadataJson = ProjectObjectMetadataSerializer.Serialize(
                new ProjectObjectMetadataEnvelope
                {
                    Infrastructure = new ProjectInfrastructureMetadata
                    {
                        InfrastructureKind = ProjectInfrastructureKind.Domain
                    }
                })
        };

        var result = ProjectStructureNodeActionCapabilityResolver.Resolve(
            node,
            new UnexpectedRuntimeLauncher(),
            new UnavailableLocalFileOpener(),
            ProjectStructureRuntimePathAuthorityMode.OperatorSelected);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_exposes_only_runtime_actions_available_on_the_current_host()
    {
        var capabilities = new ProjectStructureRuntimeLaunchCapabilities(
            ProjectStructureRuntimeCapability.Available("Direct execution is available."),
            ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.Headless,
                "No terminal is configured."),
            ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.Unsupported,
                "Elevation is unsupported."));

        var result = ProjectStructureNodeActionCapabilityResolver.Resolve(
            CreateRuntimeNode(),
            new ResolvedRuntimeLauncher(capabilities),
            new UnavailableLocalFileOpener(),
            ProjectStructureRuntimePathAuthorityMode.OperatorSelected);

        Assert.NotNull(result);
        Assert.True(result.CanRunNormally);
        Assert.False(result.CanRunAsAdministrator);
        Assert.Contains(result.Actions, action => action.ActionId == "runtime:open" && action.Label == "Run");
        Assert.DoesNotContain(result.Actions, action => action.ActionId is "runtime:terminal" or "runtime:admin");
        Assert.Contains(result.Guidance, item => item.Contains("headless", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Guidance, item => item.Contains("unsupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_agent_projection_does_not_disclose_resolved_runtime_physical_paths()
    {
        const string privateRoot = "/srv/private/customer-workspace";
        var result = ProjectStructureNodeActionCapabilityResolver.Resolve(
            CreateRuntimeNode(),
            new ResolvedRuntimeLauncher(
                new ProjectStructureRuntimeLaunchCapabilities(
                    ProjectStructureRuntimeCapability.Available("Direct execution is available."),
                    ProjectStructureRuntimeCapability.Unavailable(
                        ProjectStructureRuntimeCapabilityStatus.Headless,
                        "No terminal is configured."),
                    ProjectStructureRuntimeCapability.Unavailable(
                        ProjectStructureRuntimeCapabilityStatus.Unsupported,
                        "Elevation is unsupported.")),
                privateRoot),
            new UnavailableLocalFileOpener(),
            ProjectStructureRuntimePathAuthorityMode.AgentExecution);

        Assert.NotNull(result);
        Assert.Empty(result.RuntimeDisplayCommand);
        Assert.Empty(result.RuntimeWorkingDirectory);
        Assert.DoesNotContain(privateRoot, string.Join(' ', result.Guidance), StringComparison.Ordinal);
    }

    private static ProjectStructureNode CreateRuntimeNode()
        => new(
            "runtime-node",
            "project:1",
            ProjectObjectType.Environment,
            "dotnet-watch",
            "Start Calculator",
            "dotnet watch",
            "Planned",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "terminal", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0,
            MetadataJson: ProjectObjectMetadataSerializer.Serialize(
                new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                        ProjectPath = @"C:\unverified\calculator-e2e-test"
                    }
                }));

    private sealed class FailedRuntimeLauncher(string message) : IProjectStructureRuntimeLauncher
    {
        public bool IsAvailable => true;

        public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node)
            => new(null, message);

        public ProjectStructureRuntimeLaunchResolution Resolve(
            ProjectObjectType objectType,
            string? objectSubtype,
            string? notes,
            string metadataJson,
            ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
            => new(null, message);

        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
            ProjectStructureNode node,
            ProjectStructureRuntimeLaunchMode mode,
            ProjectStructureRuntimeLaunchApproval approval,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProjectStructureRuntimeLaunchResult(false, message));
    }

    private sealed class UnexpectedRuntimeLauncher : IProjectStructureRuntimeLauncher
    {
        public bool IsAvailable => true;

        public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node)
            => throw new InvalidOperationException("Non-Docker infrastructure must not query runtime resolution.");

        public ProjectStructureRuntimeLaunchResolution Resolve(
            ProjectObjectType objectType,
            string? objectSubtype,
            string? notes,
            string metadataJson,
            ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
            => throw new InvalidOperationException("Non-Docker infrastructure must not query runtime resolution.");

        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
            ProjectStructureNode node,
            ProjectStructureRuntimeLaunchMode mode,
            ProjectStructureRuntimeLaunchApproval approval,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Non-Docker infrastructure must not launch a runtime.");
    }

    private sealed class ResolvedRuntimeLauncher(
        ProjectStructureRuntimeLaunchCapabilities capabilities,
        string workingDirectory = "workspace") : IProjectStructureRuntimeLauncher
    {
        private readonly ProjectStructureRuntimeLaunchResolution resolution = new(
            new ProjectStructureRuntimeLaunchPlan(
                ProjectStructureRuntimePlanKind.DotNet,
                ["dotnet"],
                ["run"],
                new Dictionary<string, string?>(),
                workingDirectory,
                $"dotnet run --project {workingDirectory}/App.csproj",
                ".NET runtime",
                [],
                RequiresApproval: false,
                TerminalOnly: false),
            "Runtime launch plan resolved.",
            capabilities);

        public bool IsAvailable => true;

        public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node) => resolution;

        public ProjectStructureRuntimeLaunchResolution Resolve(
            ProjectObjectType objectType,
            string? objectSubtype,
            string? notes,
            string metadataJson,
            ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
            => resolution;

        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
            ProjectStructureNode node,
            ProjectStructureRuntimeLaunchMode mode,
            ProjectStructureRuntimeLaunchApproval approval,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Capability projection must not launch a process.");
    }

    private sealed class UnavailableLocalFileOpener : IProjectStructureLocalFileOpener
    {
        public bool IsAvailable => false;

        public bool CanOpen(ProjectStructureNode? node) => false;

        public bool CanOpenInPreferredApplication(ProjectStructureNode? node) => false;

        public Task<ProjectStructureLocalFileOpenResult> OpenAsync(
            ProjectStructureNode node,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProjectStructureLocalFileOpenResult(false, "Unavailable."));

        public Task<ProjectStructureLocalFileOpenResult> OpenInPreferredApplicationAsync(
            ProjectStructureNode node,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProjectStructureLocalFileOpenResult(false, "Unavailable."));
    }
}
