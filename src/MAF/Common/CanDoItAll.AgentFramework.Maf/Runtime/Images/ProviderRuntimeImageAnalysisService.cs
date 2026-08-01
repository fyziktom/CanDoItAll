using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ProviderRuntimeImageAnalysisService(
    IMafProviderRuntimeGateway providerRuntimeGateway) : IAgentImageAnalysisService
{
    private static readonly ProviderProfileService ProviderFeatureService = new();

    public async Task<AgentImageAnalysisResult> AnalyzeAsync(
        AgentImageAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureRequest(request);

        var result = await providerRuntimeGateway.RunProviderImageChatAsync(
                request.Provider,
                new ProviderTestChatRequest(
                    request.Model,
                    string.Empty,
                    [],
                    request.Prompt),
                request.Model,
                request.Sources
                    .Select(source => new ProviderChatAttachment(
                        source.Name,
                        source.ContentType,
                        source.Bytes))
                    .ToList(),
                request.ModelParameterConfigurationJson,
                cancellationToken)
            .ConfigureAwait(false);

        return new AgentImageAnalysisResult(
            result.Model,
            result.ResponseText,
            result.InputTokens,
            result.OutputTokens);
    }

    private static void EnsureRequest(AgentImageAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Provider);
        if (!request.Provider.IsEnabled)
        {
            throw new InvalidOperationException($"Image-analysis provider '{request.Provider.Name}' is disabled.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new InvalidOperationException($"Image-analysis provider '{request.Provider.Name}' does not define a model.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("Image analysis requires a prompt.");
        }

        if (request.Sources is null || request.Sources.Count == 0)
        {
            throw new InvalidOperationException("Image analysis requires at least one image source.");
        }

        foreach (var source in request.Sources)
        {
            if (source is null)
            {
                throw new InvalidOperationException("Image analysis sources cannot contain null entries.");
            }

            if (string.IsNullOrWhiteSpace(source.Name))
            {
                throw new InvalidOperationException("Image analysis sources require a file name.");
            }

            if (string.IsNullOrWhiteSpace(source.ContentType) ||
                !source.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Image analysis source '{source.Name}' requires an image content type.");
            }

            if (source.Bytes is null || source.Bytes.Length == 0)
            {
                throw new InvalidOperationException($"Image analysis source '{source.Name}' is empty.");
            }
        }

        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrixForModel(request.Provider, request.Model);
        if (!featureMatrix.SupportsVision)
        {
            throw new InvalidOperationException(
                $"Provider '{request.Provider.Name}' model '{request.Model}' does not support vision/image input.");
        }
    }
}
