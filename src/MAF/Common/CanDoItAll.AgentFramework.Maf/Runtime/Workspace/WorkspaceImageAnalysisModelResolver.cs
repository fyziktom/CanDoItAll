using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class WorkspaceImageAnalysisModelResolver
{
    private static readonly ProviderProfileService ProviderFeatureService = new();
    private static readonly string[] ImageAnalysisModelConfigurationKeys =
    [
        "imageAnalysisModel",
        "visionModel",
        "defaultVisionModel"
    ];

    public static string ResolveProviderImageAnalysisModel(
        ProviderProfile provider,
        string? runtimeModel)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (IsVisionCapableProviderModel(provider, runtimeModel))
        {
            return runtimeModel!.Trim();
        }

        foreach (var configurationKey in ImageAnalysisModelConfigurationKeys)
        {
            var configuredModel = TryReadConfigurationString(provider.ConfigurationJson, configurationKey);
            if (IsVisionCapableProviderModel(provider, configuredModel))
            {
                return configuredModel!.Trim();
            }
        }

        if (IsVisionCapableProviderModel(provider, provider.DefaultModel))
        {
            return provider.DefaultModel.Trim();
        }

        foreach (var suggestedModel in provider.SuggestedModels)
        {
            if (IsVisionCapableProviderModel(provider, suggestedModel))
            {
                return suggestedModel.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(runtimeModel)
            ? provider.DefaultModel.Trim()
            : runtimeModel.Trim();
    }

    private static bool IsVisionCapableProviderModel(
        ProviderProfile provider,
        string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        return ProviderFeatureService
            .ResolveFeatureMatrixForModel(provider, model.Trim())
            .SupportsVision;
    }

    private static string? TryReadConfigurationString(
        string? configurationJson,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(propertyName, out var property) &&
                   property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
