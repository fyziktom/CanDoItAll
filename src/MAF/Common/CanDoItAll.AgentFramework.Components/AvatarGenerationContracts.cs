namespace CanDoItAll.AgentFramework.Components;

public interface IAvatarGenerationGateway
{
    Task<AvatarGenerationSource?> GetDefaultSourceAsync(CancellationToken cancellationToken = default);

    Task<AvatarGenerationResult> GenerateAsync(
        AvatarGenerationRequest request,
        CancellationToken cancellationToken = default);
}
