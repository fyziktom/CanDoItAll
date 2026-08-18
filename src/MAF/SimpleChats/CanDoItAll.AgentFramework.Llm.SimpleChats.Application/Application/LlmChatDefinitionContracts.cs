using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

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
    public const int MaximumSearchLength = 200;
    public const int MaximumTagFilters = 6;

    public LlmChatDefinitionQuery(
        int take = 50,
        LlmChatDefinitionStatus? status = null,
        LlmChatDefinitionCursor? cursor = null,
        string? searchText = null,
        IReadOnlyList<string>? tags = null)
    {
        if (take is < 1 or > MaximumTake)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, $"Take must be between 1 and {MaximumTake}.");
        }

        if (status is { } value && !Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown definition status.");
        }

        var normalizedSearchText = searchText?.Trim() ?? string.Empty;
        if (normalizedSearchText.Length > MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Definition search cannot exceed {MaximumSearchLength} characters.",
                nameof(searchText));
        }

        Take = take;
        Status = status;
        Cursor = cursor;
        SearchText = normalizedSearchText;
        Tags = NormalizeTagFilters(tags);
    }

    public int Take { get; }

    public LlmChatDefinitionStatus? Status { get; }

    public LlmChatDefinitionCursor? Cursor { get; }

    public string SearchText { get; }

    public IReadOnlyList<string> Tags { get; }

    private static IReadOnlyList<string> NormalizeTagFilters(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        if (tags.Count > MaximumTagFilters)
        {
            throw new ArgumentException(
                $"A definition query cannot contain more than {MaximumTagFilters} tag filters.",
                nameof(tags));
        }

        return tags
            .Select(tag => LlmChatDefinitionValidation.NormalizeRequired(
                tag,
                LlmChatDefinitionValidation.MaximumTagLength,
                nameof(tags)))
            .Select(tag => tag.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
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

    Task<Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>> ListPageAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default);
}
