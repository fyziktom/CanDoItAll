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
        // Every request and configuration check below runs before the provider call and the catalog save, so a
        // rejection has changed nothing and the HR agent can correct the request or report the configuration gap.
        if (input.AgentId == Guid.Empty)
        {
            throw AgentToolInputValidationException.Create("Target agent id cannot be empty.");
        }

        if (input.AgentId == actorAgentId || input.AgentId == HrAgentIdentity.AgentId)
        {
            throw AgentToolInputValidationException.Create("The managed HR agent cannot replace its own avatar or authority.");
        }

        if (input.ExpectedUpdatedAtUtc == default)
        {
            throw AgentToolInputValidationException.Create("ExpectedUpdatedAtUtc is required for optimistic concurrency.");
        }

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: true, cancellationToken);
        var actor = agents.FirstOrDefault(agent => agent.Id == actorAgentId)
            ?? throw new UnauthorizedAccessException("Only the managed HR agent can generate agent avatars.");
        if (!HrAgentIdentity.Matches(actor))
        {
            throw new UnauthorizedAccessException("Only the managed HR agent can generate agent avatars.");
        }

        var target = agents.FirstOrDefault(agent => agent.Id == input.AgentId)
            ?? throw AgentToolInputValidationException.Create(
                $"Agent '{input.AgentId:D}' was not found. Search the agent catalog and retry with an existing agent id.");
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
            throw AgentToolInputValidationException.Create(
                "The HR agent is not allowed to generate images. Ask the operator to enable image generation for the HR agent.");
        }

        if (!imageAccess.PreferredProviderProfileId.HasValue)
        {
            throw AgentToolInputValidationException.Create(
                "The HR agent must explicitly configure a preferred image-generation provider. Ask the operator to configure one.");
        }

        if (string.IsNullOrWhiteSpace(imageAccess.DefaultModel))
        {
            throw AgentToolInputValidationException.Create(
                "The HR agent must explicitly configure an image-generation model. Ask the operator to configure one.");
        }

        var provider = await providerSource.GetProviderAsync(
                imageAccess.PreferredProviderProfileId.Value,
                cancellationToken)
            ?? throw AgentToolInputValidationException.Create(
                $"Image-generation provider '{imageAccess.PreferredProviderProfileId.Value:D}' was not found. Ask the operator to repair the HR agent's image provider.");
        AgentAvatarGenerationResult generated;
        try
        {
            generated = await avatarGenerationService.GenerateAsync(
                provider,
                imageAccess.DefaultModel,
                input.VisualBrief,
                input.OutputCompression,
                cancellationToken);
        }
        catch (AgentAvatarGenerationRejectedException exception)
        {
            // Only the avatar service's own request and image checks are shown; a provider failure stays opaque.
            throw new AvatarGenerationFailure(
                $"The avatar for agent '{target.Id:D}' was not stored, so the agent is unchanged. {exception.Message} " +
                "Adjust the visual brief or compression and retry.",
                exception);
        }

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

    // The avatar is generated and inspected before the catalog save, so a failed generation leaves the target agent
    // unchanged and the HR agent may adjust the brief or retry.
    private sealed class AvatarGenerationFailure(string message, Exception innerException)
        : InvalidOperationException(message, innerException), IAgentToolFailureEffectEvidence
    {
        public string ErrorCode => "AvatarGenerationFailed";

        public string SafeMessage => Message;

        public bool IsSafeToExpose => true;

        public bool CanRetryWithCorrectedInput => true;

        public AgentToolEffectState EffectState => AgentToolEffectState.NotCommitted;
    }
}
