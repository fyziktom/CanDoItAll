using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class HrAgentAvatarGenerationService(
    IAgentFrameworkWorkspaceService workspaceService,
    IAgentImageGenerationService imageGenerationService,
    ILogger<HrAgentAvatarGenerationService> logger)
{
    private const int MaximumVisualBriefLength = 2_000;

    public async Task<HrAgentAvatarGenerateResult> GenerateAsync(
        Guid actorAgentId,
        HrAgentAvatarGenerateInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.AgentId == Guid.Empty)
        {
            throw new ArgumentException("Target agent id cannot be empty.", nameof(input));
        }

        if (input.AgentId == actorAgentId || input.AgentId == HrAgentIdentity.AgentId)
        {
            throw new InvalidOperationException("The managed HR agent cannot replace its own avatar or authority.");
        }

        if (input.ExpectedUpdatedAtUtc == default)
        {
            throw new InvalidOperationException("ExpectedUpdatedAtUtc is required for optimistic concurrency.");
        }

        if (string.IsNullOrWhiteSpace(input.VisualBrief) ||
            input.VisualBrief.Trim().Length > MaximumVisualBriefLength)
        {
            throw new InvalidOperationException(
                $"VisualBrief is required and cannot exceed {MaximumVisualBriefLength} characters.");
        }

        if (input.OutputCompression is < 0 or > 100)
        {
            throw new InvalidOperationException("OutputCompression must be between 0 and 100.");
        }

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: true, cancellationToken);
        var actor = agents.FirstOrDefault(agent => agent.Id == actorAgentId)
            ?? throw new UnauthorizedAccessException("Only the managed HR agent can generate agent avatars.");
        if (!HrAgentIdentity.Matches(actor))
        {
            throw new UnauthorizedAccessException("Only the managed HR agent can generate agent avatars.");
        }

        var target = agents.FirstOrDefault(agent => agent.Id == input.AgentId)
            ?? throw new InvalidOperationException($"Agent '{input.AgentId:D}' was not found.");
        if (target.UpdatedAtUtc != input.ExpectedUpdatedAtUtc)
        {
            throw new AgentCatalogConcurrencyException(
                target.Id,
                input.ExpectedUpdatedAtUtc,
                target.UpdatedAtUtc);
        }

        var imageAccess = AgentImageGenerationAccessMetadata.Normalize(
            AgentImageGenerationAccessMetadata.Read(actor.ConfigurationJson));
        if (!imageAccess.CanGenerateImages)
        {
            throw new InvalidOperationException("The HR agent is not allowed to generate images.");
        }

        if (!imageAccess.PreferredProviderProfileId.HasValue)
        {
            throw new InvalidOperationException(
                "The HR agent must explicitly configure a preferred image-generation provider.");
        }

        if (string.IsNullOrWhiteSpace(imageAccess.DefaultModel))
        {
            throw new InvalidOperationException(
                "The HR agent must explicitly configure an image-generation model.");
        }

        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var provider = providers.FirstOrDefault(item => item.Id == imageAccess.PreferredProviderProfileId.Value)
            ?? throw new InvalidOperationException(
                $"Image-generation provider '{imageAccess.PreferredProviderProfileId.Value:D}' was not found.");
        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException($"Image-generation provider '{provider.Name}' is disabled.");
        }

        if (provider.Purpose != ProviderProfilePurpose.ImageGeneration)
        {
            throw new InvalidOperationException($"Provider '{provider.Name}' is not an image-generation provider.");
        }

        var generated = await imageGenerationService.GenerateAsync(
            new AgentImageGenerationRequest(
                provider,
                imageAccess.DefaultModel,
                BuildPrompt(input.VisualBrief),
                "1024x1024",
                "low",
                AgentGeneratedImageFormat.Jpeg,
                [])
            {
                OutputCompression = input.OutputCompression
            },
            cancellationToken);
        if (generated.Format != AgentGeneratedImageFormat.Jpeg)
        {
            throw new InvalidOperationException(
                "Avatar generation returned a format that does not match the requested JPEG avatar format.");
        }

        if (generated.Images.Count != 1)
        {
            throw new InvalidOperationException(
                "Avatar generation must return exactly one image.");
        }

        var image = generated.Images[0];
        var imageInfo = AgentAvatarImagePolicy.InspectGeneratedJpeg(image.ContentType, image.Bytes);
        var avatarDataUrl = AgentAvatarImagePolicy.BuildDataUrl(imageInfo.ContentType, image.Bytes);

        var editor = await workspaceService.GetAgentEditorAsync(target.Id, cancellationToken);
        editor.ExpectedUpdatedAtUtc = input.ExpectedUpdatedAtUtc;
        editor.AvatarImageUrl = avatarDataUrl;
        var warnings = new List<string>();
        try
        {
            await workspaceService.SaveAgentAsync(editor, cancellationToken);
        }
        catch (AgentDirectoryProjectionSynchronizationException exception)
        {
            logger.LogError(
                exception,
                "HR agent {ActorAgentId} saved an avatar for target agent {TargetAgentId}, but CRM projection synchronization failed.",
                actorAgentId,
                target.Id);
            warnings.Add(
                "The avatar was saved, but CRM projection synchronization failed. Inspect the CRM AI-agent binding.");
        }

        var updated = (await workspaceService.ListAgentsAsync(includeTemplates: true, cancellationToken))
            .FirstOrDefault(agent => agent.Id == target.Id)
            ?? throw new InvalidOperationException(
                $"Agent '{target.Id:D}' was not found after its avatar was saved.");
        logger.LogInformation(
            "HR agent {ActorAgentId} generated an avatar for target agent {TargetAgentId} with provider {ProviderProfileId}, model {Model}, and {ContentLength} bytes.",
            actorAgentId,
            target.Id,
            provider.Id,
            imageAccess.DefaultModel,
            image.Bytes.Length);

        return new HrAgentAvatarGenerateResult(
            target.Id,
            provider.Name,
            generated.Model,
            imageInfo.ContentType,
            image.Bytes.Length,
            updated.UpdatedAtUtc,
            warnings);
    }

    private static string BuildPrompt(string visualBrief)
    {
        return $"""
            Create a square professional avatar for a software agent.
            Use an abstract or illustrated identity. Do not depict a real identifiable person.
            Keep the composition readable at small sizes. Do not include text, logos, badges, or watermarks.

            Visual brief:
            {visualBrief.Trim()}
            """;
    }
}
