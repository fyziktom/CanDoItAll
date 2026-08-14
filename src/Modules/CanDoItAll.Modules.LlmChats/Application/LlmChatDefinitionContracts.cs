using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed record CreateLlmChatDefinitionCommand(
    string Name,
    string Summary,
    string AvatarImageUrl,
    string SystemPrompt,
    Guid ProviderProfileId,
    string Model,
    LlmModelSettings Settings,
    TimeSpan? Timeout,
    LlmResponseFormat? ResponseFormat,
    string RevisionReason,
    IReadOnlyList<string>? Tags = null);

public sealed record UpdateLlmChatDefinitionCommand(
    LlmChatDefinitionId DefinitionId,
    string Name,
    string Summary,
    string AvatarImageUrl,
    string SystemPrompt,
    Guid ProviderProfileId,
    string Model,
    LlmModelSettings Settings,
    TimeSpan? Timeout,
    LlmResponseFormat? ResponseFormat,
    string RevisionReason,
    long ExpectedConcurrencyToken,
    IReadOnlyList<string>? Tags = null);

public sealed record ChangeLlmChatDefinitionStatusCommand(
    LlmChatDefinitionId DefinitionId,
    LlmChatDefinitionStatus Status,
    long ExpectedConcurrencyToken);

public sealed record LlmChatDefinitionQuery
{
    public const int MaximumTake = 100;

    public LlmChatDefinitionQuery(
        int take = 50,
        LlmChatDefinitionStatus? status = null,
        int offset = 0)
    {
        if (take is < 1 or > MaximumTake)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, $"Take must be between 1 and {MaximumTake}.");
        }

        if (status is { } value && !Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown definition status.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        Take = take;
        Status = status;
        Offset = offset;
    }

    public int Take { get; }

    public LlmChatDefinitionStatus? Status { get; }

    public int Offset { get; }
}

public sealed record LlmChatDefinitionDetails(
    LlmChatDefinition Definition,
    LlmChatDefinitionRevision Revision,
    IReadOnlyList<string>? Tags = null)
{
    public IReadOnlyList<string> NormalizedTags => Tags ?? [];
}

public interface ILlmChatDefinitionApplicationService
{
    Task<Result<LlmChatDefinitionDetails>> CreateAsync(
        CreateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatDefinitionDetails>> UpdateAsync(
        UpdateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatDefinitionDetails>> ChangeStatusAsync(
        ChangeLlmChatDefinitionStatusCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatDefinitionDetails>> GetAsync(
        LlmChatDefinitionId definitionId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LlmChatDefinitionDetails>>> ListAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatPage<LlmChatDefinitionDetails>>> ListPageAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default);
}
