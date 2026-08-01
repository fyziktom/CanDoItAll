using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;

public sealed class ImageInspectWorkflowExecutor(
    IWorkspaceImageOperationService imageOperationService) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.ImageInspect;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowImageInspectExecutorSettings>(context.SettingsJson);
        var path = WorkflowInputJsonStringResolver.ResolveRequired(
            settings.Path,
            settings.PathJsonPath,
            input,
            "Image-inspection",
            nameof(settings.Path),
            nameof(settings.PathJsonPath));
        var result = await imageOperationService.InspectImageFile(path).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(CreateFailureMessage(result.Message, result.Diagnostics));
        }

        return WorkflowExecutorJson.Result(context, result);
    }

    private static string CreateFailureMessage(string message, string diagnostics)
    {
        var detail = string.IsNullOrWhiteSpace(diagnostics) ? message : diagnostics;
        return string.IsNullOrWhiteSpace(detail)
            ? "Image inspection failed."
            : $"Image inspection failed: {detail}";
    }
}
