using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Ports;

public sealed record LlmChatThinkingEffortOption(
    AgentThinkingEffortSupportStatus Status,
    AgentThinkingEffortControlMode ControlMode,
    IReadOnlyList<AgentReasoningEffortLevel> AllowedEfforts,
    AgentReasoningEffortLevel? ProviderDefault);

public sealed record LlmChatModelOption(
    string Model,
    LlmChatThinkingEffortOption ThinkingEffort);

public sealed record LlmChatProviderOption(
    Guid ProviderProfileId,
    string ProviderName,
    ProviderKind ProviderKind,
    IReadOnlyList<LlmChatModelOption> Models);

public sealed record LlmChatResolvedProvider(
    Guid ProviderProfileId,
    string ProviderName,
    ProviderKind ProviderKind,
    string Model,
    ProviderModelThinkingEffortCapability ThinkingEffortCapability,
    AgentReasoningEffortLevel? ProviderDefaultThinkingEffort);

public interface ILlmChatProviderResolver
{
    Task<Result<LlmChatResolvedProvider>> ResolveAsync(
        Guid providerProfileId,
        string model,
        AgentReasoningEffortLevel? thinkingEffort,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LlmChatProviderOption>>> ListOptionsAsync(
        CancellationToken cancellationToken = default);
}
