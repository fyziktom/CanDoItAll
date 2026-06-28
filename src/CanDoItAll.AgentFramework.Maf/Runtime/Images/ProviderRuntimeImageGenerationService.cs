using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class ProviderRuntimeImageGenerationService(
    IProviderRuntimeDescriptorStore descriptorStore,
    IProviderRuntimePool runtimePool) : IAgentImageGenerationService
{
    public async Task<AgentImageGenerationResult> GenerateAsync(
        AgentImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureImageRequest(request);

        var handle = await GetRuntimeHandleAsync(request.Provider, cancellationToken).ConfigureAwait(false);
        var operation = request.Sources.Count == 0
            ? AgentProviderOperationKind.GenerateImage
            : AgentProviderOperationKind.EditImage;
        var query = new ProviderDispatchQuery(
            request.Provider,
            AgentProviderCapabilityKind.ImageGeneration,
            operation,
            request.Model);
        var payload = new ProviderImageGenerationRequest(
            request.Provider,
            request.Model,
            request.Prompt,
            request.Size,
            request.Quality,
            MapFormat(request.Format),
            request.Sources
                .Select(source => new ProviderImageSource(source.Name, source.ContentType, source.Bytes))
                .ToList());

        var result = await handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<ProviderImageGenerationRequest>(query, payload),
            async (context, token) =>
            {
                EnsureProviderKindMatches(context.Descriptor, context.Query.Provider);
                var driver = handle.ProviderFactory.Resolve<IProviderImageGenerationDriver>(context.Query.Provider.Kind);
                return await driver.GenerateImageAsync(context.Payload, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        if (result.Images.Count == 0)
        {
            throw new InvalidOperationException("Image generation completed without image data.");
        }

        return new AgentImageGenerationResult(
            result.Model,
            MapFormat(result.Format),
            result.Images
                .Select(image => new AgentGeneratedImage(image.ContentType, image.Bytes, image.RevisedPrompt))
                .ToList());
    }

    private async ValueTask<IProviderRuntimeHandle> GetRuntimeHandleAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        descriptorStore.Upsert(provider, secretReferenceIdentity: provider.ApiKeyEnvironmentVariable);
        return await runtimePool.GetRequiredAsync(provider.Id, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureImageRequest(AgentImageGenerationRequest request)
    {
        if (!request.Provider.IsEnabled)
        {
            throw new InvalidOperationException($"Image-generation provider '{request.Provider.Name}' is disabled.");
        }

        if (request.Provider.Purpose != ProviderProfilePurpose.ImageGeneration)
        {
            throw new InvalidOperationException($"Provider '{request.Provider.Name}' is not an image-generation provider profile.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new InvalidOperationException($"Image-generation provider '{request.Provider.Name}' does not define a model.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("Image generation requires a prompt.");
        }

        if (string.IsNullOrWhiteSpace(request.Size))
        {
            throw new InvalidOperationException("Image generation requires an image size.");
        }

        if (string.IsNullOrWhiteSpace(request.Quality))
        {
            throw new InvalidOperationException("Image generation requires an image quality.");
        }

        foreach (var source in request.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.Name))
            {
                throw new InvalidOperationException("Image edit sources require a file name.");
            }

            if (string.IsNullOrWhiteSpace(source.ContentType))
            {
                throw new InvalidOperationException($"Image edit source '{source.Name}' requires a content type.");
            }

            if (source.Bytes.Length == 0)
            {
                throw new InvalidOperationException($"Image edit source '{source.Name}' is empty.");
            }
        }
    }

    private static void EnsureProviderKindMatches(
        ProviderRuntimeDescriptor descriptor,
        ProviderProfile provider)
    {
        if (descriptor.ProviderKind != provider.Kind)
        {
            throw new InvalidOperationException("Provider runtime descriptor kind does not match the image-generation provider kind.");
        }
    }

    private static ProviderGeneratedImageFormat MapFormat(AgentGeneratedImageFormat format)
    {
        return format switch
        {
            AgentGeneratedImageFormat.Jpeg => ProviderGeneratedImageFormat.Jpeg,
            AgentGeneratedImageFormat.Webp => ProviderGeneratedImageFormat.Webp,
            _ => ProviderGeneratedImageFormat.Png
        };
    }

    private static AgentGeneratedImageFormat MapFormat(ProviderGeneratedImageFormat format)
    {
        return format switch
        {
            ProviderGeneratedImageFormat.Jpeg => AgentGeneratedImageFormat.Jpeg,
            ProviderGeneratedImageFormat.Webp => AgentGeneratedImageFormat.Webp,
            _ => AgentGeneratedImageFormat.Png
        };
    }
}
