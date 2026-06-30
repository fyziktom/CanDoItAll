using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public static class DockerPluginConstants
{
    public static PluginId PluginId { get; } = new("candoitall.docker");

    public static PluginPackageId PackageId { get; } = new("candoitall.docker.package");

    public static UiIconDescriptor Icon { get; } = UiIconDescriptor.MaterialIcon("deployed_code", "Docker");

    public static PluginRendererKey SettingsRendererKey { get; } = new("docker.workflow-settings");

    public static WorkflowExecutorId ListContainersExecutorId { get; } = new("docker.list-containers");

    public static WorkflowExecutorId PullImageExecutorId { get; } = new("docker.pull-image");

    public static WorkflowExecutorId StartContainerExecutorId { get; } = new("docker.start-container");

    public static WorkflowExecutorId ReadLogsExecutorId { get; } = new("docker.read-logs");
}
