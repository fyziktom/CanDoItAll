using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed record LlmChatThinkingEffortPresentation(
    LlmChatThinkingEffortSupport Support,
    LlmChatThinkingEffortControl Control,
    IReadOnlyList<LlmChatThinkingEffort> AllowedEfforts,
    LlmChatThinkingEffort? ProviderDefault);

public sealed record LlmChatModelOptionPresentation(
    string Model,
    LlmChatThinkingEffortPresentation ThinkingEffort) {
    public string DisplayName { get; init; } = Model;
    public bool IsSuggested { get; init; } = true;
}

public sealed record LlmChatProviderOptionPresentation(
    Guid ProviderProfileId,
    string ProviderName,
    IReadOnlyList<LlmChatModelOptionPresentation> Models) {
    public bool IsSourceManaged { get; init; }
}

public interface ILlmChatProviderUiGateway
{
    Task<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>> ListAsync(
        CancellationToken cancellationToken = default);
}

public sealed class LlmChatProviderUiGateway(
    ILlmChatProviderResolver providers,
    ILlmChatUiAuthorizationFacade authorization) : ILlmChatProviderUiGateway
{
    public async Task<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Read, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<IReadOnlyList<LlmChatProviderOptionPresentation>>(
                LlmChatUiPermission.Read);
        }

        var result = await providers.ListOptionsAsync(cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(
            result,
            options => (IReadOnlyList<LlmChatProviderOptionPresentation>)[.. options.Select(ToPresentation)]);
    }

    private static LlmChatProviderOptionPresentation ToPresentation(LlmChatProviderOption option)
        => new(
            option.ProviderProfileId,
            option.ProviderName,
            [.. option.Models.Select(model => new LlmChatModelOptionPresentation(
                model.Model,
                new LlmChatThinkingEffortPresentation(
                    LlmChatThinkingEffortMapper.FromProvider(model.ThinkingEffort.Status),
                    LlmChatThinkingEffortMapper.FromProvider(model.ThinkingEffort.ControlMode),
                    [.. model.ThinkingEffort.AllowedEfforts.Select(LlmChatThinkingEffortMapper.FromProvider)],
                    LlmChatThinkingEffortMapper.FromProvider(model.ThinkingEffort.ProviderDefault))) {
                DisplayName = model.DisplayName,
                IsSuggested = model.IsSuggested
            })]) {
            IsSourceManaged = option.IsSourceManaged
        };
}
