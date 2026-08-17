using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentAvatarGenerationGateway(
    IAgentFrameworkWorkspaceService workspaceService,
    AgentAvatarGenerationService generationService) : IAvatarGenerationGateway
{
    public async Task<AvatarGenerationSource?> GetDefaultSourceAsync(
        CancellationToken cancellationToken = default)
    {
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var provider = ImageGenerationProviderSelectionPolicy.ResolveDefault(providers, runtimeProvider: null);
        return provider is { IsEnabled: true, Purpose: ProviderProfilePurpose.ImageGeneration } &&
               !string.IsNullOrWhiteSpace(provider.DefaultModel)
            ? new(provider.Id, provider.Name, provider.DefaultModel.Trim())
            : null;
    }

    public async Task<AvatarGenerationResult> GenerateAsync(
        AvatarGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var provider = providers.FirstOrDefault(item => item.Id == request.Source.ProviderProfileId);
        if (provider is null)
        {
            throw new InvalidOperationException("The selected image-generation provider no longer exists.");
        }

        var result = await generationService.GenerateAsync(
            provider,
            request.Source.Model,
            request.VisualBrief,
            cancellationToken: cancellationToken);
        return new(result.AvatarDataUrl);
    }
}
