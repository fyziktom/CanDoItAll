using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using ProviderProfileMapper = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfileMapper;

internal sealed class SharedProviderImageCapabilityRelay(
    ISharedProviderImageExecutionTargetResolver targetResolver,
    ProviderProfileMapper providerMapper,
    IAgentImageGenerationService imageGenerationService,
    ILogger<SharedProviderImageCapabilityRelay> logger) :
    ISharedProviderImageCapabilityRelay
{
    private const int MaximumImageBytes = 16 * 1024 * 1024;
    private const int MaximumTotalImageBytes = 64 * 1024 * 1024;
    private const int MaximumPromptCharacters = 1024 * 1024;

    private static readonly IReadOnlySet<string> SupportedSizes = new HashSet<string>(StringComparer.Ordinal)
    {
        "256x256",
        "512x512",
        "1024x1024",
        "1024x1536",
        "1536x1024",
        "auto"
    };

    private static readonly IReadOnlySet<string> SupportedQualities = new HashSet<string>(StringComparer.Ordinal)
    {
        "standard",
        "hd",
        "low",
        "medium",
        "high",
        "auto"
    };

    public async ValueTask<IReadOnlyList<SharedProviderGeneratedImage>> GenerateAsync(
        SharedProviderImageCapabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var target = await targetResolver.ResolveAsync(request, cancellationToken)
            ?? throw new KeyNotFoundException("The shared-provider image target was not found.");
        AgentFrameworkProviderProfile provider;
        try
        {
            provider = providerMapper.Map(target.Profile);
        }
        catch
        {
            throw new InvalidOperationException("The shared-provider image target is unavailable.");
        }

        if (!provider.IsEnabled ||
            provider.Purpose != CanDoItAll.AgentFramework.Models.ProviderProfilePurpose.ImageGeneration)
        {
            throw new InvalidOperationException("The shared-provider image target is unavailable.");
        }

        var format = ParseFormat(request.OutputFormat);
        var expectedContentType = ContentType(format);
        var images = new List<SharedProviderGeneratedImage>(request.Count);
        var totalBytes = 0;
        try
        {
            while (images.Count < request.Count)
            {
                var result = await imageGenerationService.GenerateAsync(
                    new AgentImageGenerationRequest(
                        provider,
                        request.Model,
                        request.Prompt,
                        request.Size,
                        request.Quality,
                        format,
                        Sources: []),
                    cancellationToken);
                if (result.Images.Count == 0)
                {
                    throw new InvalidOperationException("Image generation returned no image data.");
                }

                foreach (var image in result.Images)
                {
                    if (images.Count == request.Count)
                    {
                        break;
                    }

                    if (!string.Equals(image.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase) ||
                        image.Bytes.Length is <= 0 or > MaximumImageBytes ||
                        checked(totalBytes + image.Bytes.Length) > MaximumTotalImageBytes)
                    {
                        throw new InvalidOperationException("Image generation returned invalid or oversized image data.");
                    }

                    totalBytes += image.Bytes.Length;
                    images.Add(new SharedProviderGeneratedImage(
                        expectedContentType,
                        image.Bytes.ToArray(),
                        RevisedPrompt: null));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            logger.LogWarning(
                "Shared-provider image generation failed for provider profile {ProviderProfileId}.",
                request.ProviderProfileId);
            throw new InvalidOperationException("Shared-provider image generation failed.");
        }

        return images.AsReadOnly();
    }

    private static void Validate(SharedProviderImageCapabilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProviderProfileId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Model) ||
            request.Model.Length > SharedProviderRoutingModelIdCodec.MaximumUpstreamModelIdLength ||
            request.Model != request.Model.Trim() ||
            request.Model.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(request.Prompt) ||
            request.Prompt.Length > MaximumPromptCharacters ||
            !SupportedSizes.Contains(request.Size) ||
            !SupportedQualities.Contains(request.Quality) ||
            request.OutputFormat is not ("png" or "jpeg" or "webp") ||
            request.Count is < 1 or > SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount)
        {
            throw new ArgumentException("The shared-provider image request is invalid.", nameof(request));
        }
    }

    private static AgentGeneratedImageFormat ParseFormat(string outputFormat)
        => outputFormat switch
        {
            "jpeg" => AgentGeneratedImageFormat.Jpeg,
            "webp" => AgentGeneratedImageFormat.Webp,
            _ => AgentGeneratedImageFormat.Png
        };

    private static string ContentType(AgentGeneratedImageFormat format)
        => format switch
        {
            AgentGeneratedImageFormat.Jpeg => "image/jpeg",
            AgentGeneratedImageFormat.Webp => "image/webp",
            _ => "image/png"
        };
}
