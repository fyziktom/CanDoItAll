using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins;

internal sealed class DockerBundledPlugin : IBundledPlugin
{
    private static readonly WorkflowValueShape JsonShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Docker command JSON result");

    public PluginDescriptor Descriptor { get; } = new(
        DockerPluginConstants.PluginId,
        "Docker",
        "Provides guarded workflow executors for listing containers, pulling images, starting containers, and reading bounded logs.",
        "1.0.0",
        "CanDoItAll",
        PluginSourceKind.Bundled,
        PluginTrustLevel.Bundled,
        "1.0.0",
        PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.HostCommand,
        [
            CreateExecutor(
                DockerPluginConstants.ListContainersExecutorId,
                "Docker containers",
                "Lists running Docker containers through a constrained docker ps host-tool recipe.",
                WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 20 }),
            CreateExecutor(
                DockerPluginConstants.PullImageExecutorId,
                "Docker pull image",
                "Pulls a validated Docker image reference through a constrained docker pull host-tool recipe.",
                WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 900, CaptureOutputArtifact = true }),
            CreateExecutor(
                DockerPluginConstants.StartContainerExecutorId,
                "Docker start container",
                "Starts an existing container or creates a container from a validated image through constrained Docker recipes.",
                WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 120, CaptureOutputArtifact = true }),
            CreateExecutor(
                DockerPluginConstants.ReadLogsExecutorId,
                "Docker logs",
                "Reads bounded logs from a validated running Docker container.",
                WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 30, CaptureOutputArtifact = true })
        ],
        PluginSettingsDescriptor.Empty,
        [],
        new PluginPackageDescriptor(
            DockerPluginConstants.PackageId,
            "1.0.0",
            "1.0.0",
            Sha256: string.Empty,
            Signature: string.Empty),
        Icon: DockerPluginConstants.Icon);

    public void ConfigurePluginServices(IPluginServiceRegistry services)
    {
    }

    private static PluginWorkflowExecutorDescriptor CreateExecutor(
        WorkflowExecutorId executorId,
        string name,
        string description,
        WorkflowExecutorExecutionPolicy defaultPolicy)
        => new(
            executorId,
            name,
            description,
            WorkflowExecutorCategoryKind.Command,
            DockerPluginConstants.SettingsRendererKey,
            ConfigurationSchema.Empty(),
            WorkflowValueShape.Text,
            JsonShape,
            defaultPolicy)
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.RunsHostCommand |
                WorkflowExecutorCapabilityFlags.EmitsArtifacts |
                WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
                WorkflowExecutorApprovalRequirement.AlwaysRequired),
            DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Run Preview simulates Docker host-tool output without invoking Docker.")
        };
}
