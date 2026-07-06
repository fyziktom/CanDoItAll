using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class InputAttachmentSupport
{
    private static readonly ProviderProfileService ProviderFeatureService = new();

    public static void EnsureSupported(
        ProviderProfile provider,
        string model,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        var attachments = runtimeOptions.InputAttachments ?? [];
        if (attachments.Count == 0)
        {
            return;
        }

        var selectedModel = string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model.Trim();
        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrixForModel(provider, selectedModel);
        if (featureMatrix.SupportsVision)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Provider '{provider.Name}' model '{selectedModel}' does not support vision/image input, but the request includes {attachments.Count:N0} image attachment(s). Choose a vision-capable provider/model or remove the attachment(s).");
    }

    public static string ResolveRuntimeModel(
        ProviderProfile provider,
        string model,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        var selectedModel = string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model.Trim();
        var attachments = runtimeOptions.InputAttachments ?? [];
        if (attachments.Count == 0)
        {
            return selectedModel;
        }

        if (ProviderFeatureService.ResolveFeatureMatrixForModel(provider, selectedModel).SupportsVision)
        {
            return selectedModel;
        }

        var imageAnalysisModel = WorkspaceRuntimePlugin.ResolveProviderImageAnalysisModel(provider, selectedModel);
        return ProviderFeatureService.ResolveFeatureMatrixForModel(provider, imageAnalysisModel).SupportsVision
            ? imageAnalysisModel
            : selectedModel;
    }

    public static bool HasRequestScopedInputAttachments(AgentRuntimeExecutionOptions runtimeOptions)
        => runtimeOptions.InputAttachments?.Count > 0;
}
