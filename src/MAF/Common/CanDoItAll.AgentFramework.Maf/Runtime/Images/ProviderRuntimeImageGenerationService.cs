using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class ProviderRuntimeImageGenerationService(
    IProviderRuntimeDescriptorStore descriptorStore,
    IProviderRuntimePool runtimePool,
    IAgentProviderCredentialResolver? providerCredentialResolver = null,
    ILogger<ProviderRuntimeImageGenerationService>? logger = null) :
    IAgentImageGenerationService
{
    public async Task<AgentImageGenerationResult> GenerateAsync(
        AgentImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureImageRequest(request);

        ProviderImageGenerationResult result;
        using (var credentialPreparation =
               await PrepareProviderCredentialDispatchAsync(
                       request.Provider,
                       cancellationToken)
                   .ConfigureAwait(false))
        {
            using var credentialScope = credentialPreparation?.BeginScope();
            var handle = await GetRuntimeHandleAsync(
                    request.Provider,
                    cancellationToken)
                .ConfigureAwait(false);
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
                    .Select(source => new ProviderImageSource(
                        source.Name,
                        source.ContentType,
                        source.Bytes))
                    .ToList())
            {
                OutputCompression = request.OutputCompression
            };

            try
            {
                result = await handle.DispatchAsync(
                        new ProviderRuntimeDispatchRequest<
                            ProviderImageGenerationRequest>(query, payload),
                        async (context, token) =>
                        {
                            EnsureProviderKindMatches(
                                context.Descriptor,
                                context.Query.Provider);
                            var driver = handle.ProviderFactory.Resolve<
                                IProviderImageGenerationDriver>(
                                context.Query.Provider.Kind);
                            return await driver.GenerateImageAsync(
                                    context.Payload,
                                    token)
                                .ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ProviderFailureBoundaryException exception)
            {
                logger?.LogError(
                    "Source-managed image generation failed for provider {ProviderProfileId}. FailureType={FailureType} StatusCode={StatusCode}",
                    exception.ProviderId,
                    exception.DiagnosticFailureType ?? "Unavailable",
                    exception.DiagnosticStatusCode);
                throw;
            }
        }

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

    private async ValueTask<IAgentProviderCredentialDispatchScopePreparation?>
        PrepareProviderCredentialDispatchAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        if (providerCredentialResolver is not
            IAgentProviderCredentialDispatchScopeFactory scopeFactory)
        {
            return null;
        }

        return await scopeFactory
            .PrepareAsync([provider], cancellationToken)
            .ConfigureAwait(false);
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

        if (request.OutputCompression is < 0 or > 100)
        {
            throw new InvalidOperationException("Image output compression must be between 0 and 100.");
        }

        if (request.OutputCompression.HasValue && request.Format == AgentGeneratedImageFormat.Png)
        {
            throw new InvalidOperationException("Image output compression is supported only for JPEG or WebP output.");
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
