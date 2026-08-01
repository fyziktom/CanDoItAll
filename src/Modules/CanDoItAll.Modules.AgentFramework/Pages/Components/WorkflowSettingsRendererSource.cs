using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed class WorkflowSettingsRendererSource : ISettingsRendererSource
{
    private static readonly WorkflowExecutorDescriptor ImageGenerationDescriptor =
        BuiltInWorkflowExecutorDescriptors.ImageGeneration;

    private static readonly IReadOnlyList<SettingsRendererDescriptor> Renderers =
    [
        new SettingsRendererDescriptor(
            ImageGenerationDescriptor.SetupRendererKey,
            typeof(WorkflowImageGenerationSettingsRenderer),
            SettingsRendererTrustLevel.Application,
            ImageGenerationDescriptor.Source.SourceId,
            ImageGenerationDescriptor.ConfigurationSchema.Version)
    ];

    public IReadOnlyList<SettingsRendererDescriptor> ListRenderers()
        => Renderers;
}
