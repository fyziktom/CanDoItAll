namespace CanDoItAll.AgentFramework.Components;

public sealed record AvatarGenerationSource(
    Guid ProviderProfileId,
    string ProviderName,
    string Model);

public sealed record AvatarGenerationRequest(
    AvatarGenerationSource Source,
    string VisualBrief);

public sealed record AvatarGenerationResult(string AvatarDataUrl);

public interface IAvatarGenerationGateway
{
    Task<AvatarGenerationSource?> GetDefaultSourceAsync(CancellationToken cancellationToken = default);

    Task<AvatarGenerationResult> GenerateAsync(
        AvatarGenerationRequest request,
        CancellationToken cancellationToken = default);
}
