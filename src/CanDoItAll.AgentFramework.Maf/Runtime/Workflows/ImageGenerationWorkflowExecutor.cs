using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class ImageGenerationWorkflowExecutor : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.ImageGeneration;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowImageGenerationExecutorSettings>(context.SettingsJson);
        if (string.IsNullOrWhiteSpace(settings.Prompt))
        {
            throw new InvalidOperationException("Image-generation executor setting 'Prompt' is required.");
        }

        throw new InvalidOperationException("Workflow image generation requires a provider bridge extracted from the existing MafAgentRuntime image-generation tool. The descriptor and setup contract are registered, but no workflow-safe provider bridge is registered in this host.");
    }
}

