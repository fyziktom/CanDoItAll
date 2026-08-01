using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

internal sealed class DockerBundledPlugin : IBundledPlugin
{
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
        DockerWorkflowExecutorDescriptors.All
            .Select(PluginWorkflowExecutorDescriptor.FromWorkflowExecutorDescriptor)
            .ToArray(),
        PluginSettingsDescriptor.Empty,
        [],
        new PluginPackageDescriptor(
            DockerPluginConstants.PackageId,
            "1.0.0",
            "1.0.0",
            Sha256: string.Empty,
            Signature: string.Empty),
        Icon: DockerPluginConstants.Icon)
    {
        Tags = [PluginDescriptorTags.Docker, PluginDescriptorTags.HostCommand, PluginDescriptorTags.Workflow]
    };

    public void ConfigurePluginServices(IPluginServiceRegistry services)
    {
    }

}
