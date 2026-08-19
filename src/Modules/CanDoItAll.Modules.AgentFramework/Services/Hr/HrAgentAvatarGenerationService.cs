using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class HrAgentAvatarGenerationService(
    IAgentFrameworkWorkspaceService workspaceService,
    IProviderRuntimeProfileSource providerSource,
    AgentAvatarGenerationService avatarGenerationService,
    ILogger<HrAgentAvatarGenerationService> logger)
{
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

        var provider = await providerSource.GetProviderAsync(
                imageAccess.PreferredProviderProfileId.Value,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Image-generation provider '{imageAccess.PreferredProviderProfileId.Value:D}' was not found.");
        var generated = await avatarGenerationService.GenerateAsync(
            provider,
            imageAccess.DefaultModel,
            input.VisualBrief,
            input.OutputCompression,
            cancellationToken);

        var editor = await workspaceService.GetAgentEditorAsync(target.Id, cancellationToken);
        editor.ExpectedUpdatedAtUtc = input.ExpectedUpdatedAtUtc;
        editor.AvatarImageUrl = generated.AvatarDataUrl;
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
            generated.Model,
            generated.ContentLength);

        return new HrAgentAvatarGenerateResult(
            target.Id,
            generated.ProviderName,
            generated.Model,
            generated.ContentType,
            generated.ContentLength,
            updated.UpdatedAtUtc,
            warnings);
    }
}
