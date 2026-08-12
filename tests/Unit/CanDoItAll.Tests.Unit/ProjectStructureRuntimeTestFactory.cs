using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

internal static class ProjectStructureRuntimeTestFactory
{
    public static ProjectStructureRuntimeLauncher CreateLauncher(
        string workspaceRoot,
        IProjectStructureDotNetProjectTargetResolver projectTargetResolver,
        IExternalTargetPathRegistryFactory? externalTargetPathRegistryFactory = null,
        ProjectStructureRuntimeHostPlatform? platform = null,
        IProjectStructureRuntimeExecutionAdapter? executionAdapter = null,
        IProjectStructureTerminalPresenter? terminalPresenter = null,
        IProjectStructureRuntimeElevationAdapter? elevationAdapter = null)
    {
        var workspacePathResolver = new TestWorkspacePathResolver(workspaceRoot);
        var hostContext = new ProjectStructureRuntimeHostContext(
            platform ?? CaptureCurrentPlatform());
        var pathResolver = new ProjectStructureRuntimePathResolver(
            new WorkspacePathAccessGuard(
                workspacePathResolver,
                TestWorkspaceServices.PhysicalPathPolicyFactory),
            externalTargetPathRegistryFactory ?? new ExternalTargetPathRegistryFactory(),
            new FileSystemStoragePathPolicy(workspacePathResolver),
            hostContext);
        return new ProjectStructureRuntimeLauncher(
            pathResolver,
            NullLogger<ProjectStructureRuntimeLauncher>.Instance,
            projectTargetResolver,
            new ProjectStructureRuntimePlanCompiler(),
            hostContext,
            executionAdapter ?? new AvailableRuntimeExecutionAdapter(),
            terminalPresenter ?? new AvailableTerminalPresenter(),
            elevationAdapter ?? new PlatformElevationAdapter(hostContext.Platform));
    }

    private static ProjectStructureRuntimeHostPlatform CaptureCurrentPlatform()
        => OperatingSystem.IsWindows()
            ? ProjectStructureRuntimeHostPlatform.Windows
            : OperatingSystem.IsLinux()
                ? ProjectStructureRuntimeHostPlatform.Linux
                : ProjectStructureRuntimeHostPlatform.MacOS;

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }

    private sealed class AvailableRuntimeExecutionAdapter : IProjectStructureRuntimeExecutionAdapter
    {
        public bool IsRunning(string nodeId) => false;

        public ProjectStructureRuntimeCapability Probe(ProjectStructureRuntimeLaunchPlan plan)
            => plan.TerminalOnly
                ? ProjectStructureRuntimeCapability.Unavailable(
                    ProjectStructureRuntimeCapabilityStatus.PolicyDenied,
                    "A headless entry point is required.")
                : ProjectStructureRuntimeCapability.Available("Direct execution is available in the test host.");

        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
            ProjectStructureRuntimeLaunchPlan plan,
            string nodeId,
            CancellationToken cancellationToken)
            => Task.FromResult(new ProjectStructureRuntimeLaunchResult(true, "Direct execution recorded."));

        public Task<ProjectStructureRuntimeLaunchResult> StopAsync(
            string nodeId,
            CancellationToken cancellationToken)
            => Task.FromResult(new ProjectStructureRuntimeLaunchResult(true, "Direct execution stopped."));
    }

    private sealed class AvailableTerminalPresenter : IProjectStructureTerminalPresenter
    {
        public ProjectStructureRuntimeCapability Probe(ProjectStructureRuntimeLaunchPlan plan)
            => ProjectStructureRuntimeCapability.Available("Terminal presentation is available in the test host.");

        public Task<ProjectStructureRuntimeLaunchResult> OpenAsync(
            ProjectStructureRuntimeLaunchPlan plan,
            string nodeId,
            CancellationToken cancellationToken)
            => Task.FromResult(new ProjectStructureRuntimeLaunchResult(true, "Terminal presentation recorded."));
    }

    private sealed class PlatformElevationAdapter(
        ProjectStructureRuntimeHostPlatform platform) : IProjectStructureRuntimeElevationAdapter
    {
        public ProjectStructureRuntimeCapability Probe(ProjectStructureRuntimeLaunchPlan plan)
            => platform == ProjectStructureRuntimeHostPlatform.Windows &&
               !plan.TerminalOnly &&
               plan.EnvironmentVariables.Count == 0
                ? ProjectStructureRuntimeCapability.Available("Windows elevation is available in the test host.")
                : ProjectStructureRuntimeCapability.Unavailable(
                    ProjectStructureRuntimeCapabilityStatus.Unsupported,
                    "Elevation is unavailable in the test host.");

        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
            ProjectStructureRuntimeLaunchPlan plan,
            string nodeId,
            CancellationToken cancellationToken)
            => Task.FromResult(new ProjectStructureRuntimeLaunchResult(true, "Elevation recorded."));
    }
}
