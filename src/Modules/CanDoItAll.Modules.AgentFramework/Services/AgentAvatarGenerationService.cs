using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record AgentAvatarGenerationResult(
    string AvatarDataUrl,
    string ProviderName,
    string Model,
    string ContentType,
    int ContentLength);

public sealed class AgentAvatarGenerationService(
    IAgentImageGenerationService imageGenerationService,
    ILogger<AgentAvatarGenerationService> logger)
{
    public const int MaximumVisualBriefLength = 2_000;
    public const int DefaultOutputCompression = 35;

    public async Task<AgentAvatarGenerationResult> GenerateAsync(
        ProviderProfile provider,
        string model,
        string visualBrief,
        int outputCompression = DefaultOutputCompression,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var normalizedModel = model?.Trim() ?? string.Empty;
        var normalizedVisualBrief = visualBrief?.Trim() ?? string.Empty;
        ValidateRequest(provider, normalizedModel, normalizedVisualBrief, outputCompression);

        var generated = await imageGenerationService.GenerateAsync(
            new AgentImageGenerationRequest(
                provider,
                normalizedModel,
                BuildPrompt(normalizedVisualBrief),
                "1024x1024",
                "low",
                AgentGeneratedImageFormat.Jpeg,
                [])
            {
                OutputCompression = outputCompression
            },
            cancellationToken);
        if (generated.Format != AgentGeneratedImageFormat.Jpeg)
        {
            throw new InvalidOperationException(
                "Avatar generation returned a format that does not match the requested JPEG avatar format.");
        }

        if (generated.Images.Count != 1)
        {
            throw new InvalidOperationException("Avatar generation must return exactly one image.");
        }

        var image = generated.Images[0];
        var imageInfo = AgentAvatarImagePolicy.InspectGeneratedJpeg(image.ContentType, image.Bytes);
        var avatarDataUrl = AgentAvatarImagePolicy.BuildDataUrl(imageInfo.ContentType, image.Bytes);
        var generatedModel = string.IsNullOrWhiteSpace(generated.Model)
            ? normalizedModel
            : generated.Model.Trim();

        logger.LogInformation(
            "Generated an agent avatar draft with provider {ProviderProfileId}, model {Model}, and {ContentLength} bytes.",
            provider.Id,
            generatedModel,
            imageInfo.ByteCount);

        return new AgentAvatarGenerationResult(
            avatarDataUrl,
            provider.Name,
            generatedModel,
            imageInfo.ContentType,
            imageInfo.ByteCount);
    }

    private static void ValidateRequest(
        ProviderProfile provider,
        string model,
        string visualBrief,
        int outputCompression)
    {
        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException($"Image-generation provider '{provider.Name}' is disabled.");
        }

        if (provider.Purpose != ProviderProfilePurpose.ImageGeneration)
        {
            throw new InvalidOperationException($"Provider '{provider.Name}' is not an image-generation provider.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                $"Image-generation provider '{provider.Name}' does not define a default model.");
        }

        if (string.IsNullOrWhiteSpace(visualBrief) || visualBrief.Length > MaximumVisualBriefLength)
        {
            throw new InvalidOperationException(
                $"Avatar prompt is required and cannot exceed {MaximumVisualBriefLength} characters.");
        }

        if (outputCompression is < 0 or > 100)
        {
            throw new InvalidOperationException("Output compression must be between 0 and 100.");
        }
    }

    private static string BuildPrompt(string visualBrief)
    {
        return $"""
            Create a square professional avatar for a software agent.
            Use an abstract or illustrated identity. Do not depict a real identifiable person.
            Keep the composition readable at small sizes. Do not include text, logos, badges, or watermarks.

            Visual brief:
            {visualBrief}
            """;
    }
}
