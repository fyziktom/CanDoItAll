using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;

namespace CanDoItAll.Modules.LlmChats.Ui;

public enum LlmChatUiResponseFormatKind
{
    Text,
    Json,
    JsonSchema
}

public sealed record LlmChatDefinitionMutation(
    string Name,
    string Summary,
    string AvatarImageUrl,
    string SystemPrompt,
    Guid ProviderProfileId,
    string Model,
    double? Temperature,
    LlmChatThinkingEffort? ThinkingEffort,
    string ModelParameterConfigurationJson,
    TimeSpan? Timeout,
    LlmChatUiResponseFormatKind ResponseFormat,
    string SchemaJson,
    string SchemaName,
    string SchemaDescription,
    string RevisionReason,
    IReadOnlyList<string> Tags);

public sealed record LlmChatDefinitionListItem(
    Guid DefinitionId,
    string Name,
    string Summary,
    string AvatarImageUrl,
    LlmChatDefinitionStatus Status,
    int Revision,
    long ConcurrencyToken,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> Tags);

public sealed record LlmChatDefinitionEditor(
    LlmChatDefinitionListItem Definition,
    string SystemPrompt,
    Guid ProviderProfileId,
    string ProviderName,
    string Model,
    double? Temperature,
    LlmChatThinkingEffort? ThinkingEffort,
    string ModelParameterConfigurationJson,
    TimeSpan? Timeout,
    LlmChatUiResponseFormatKind ResponseFormat,
    string SchemaJson,
    string SchemaName,
    string SchemaDescription,
    string RevisionReason);

public interface ILlmChatDefinitionUiGateway
{
    Task<LlmChatUiResult<LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>>> ListPageAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatDefinitionListItem>> GetAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatDefinitionEditor>> GetEditorAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatDefinitionEditor>> CreateAsync(
        LlmChatDefinitionMutation mutation,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatDefinitionEditor>> UpdateAsync(
        Guid definitionId,
        LlmChatDefinitionMutation mutation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatDefinitionListItem>> ChangeStatusAsync(
        Guid definitionId,
        LlmChatDefinitionStatus status,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default);
}

public sealed class LlmChatDefinitionUiGateway(
    ILlmChatDefinitionApplicationService definitions,
    ILlmChatUiAuthorizationFacade authorization) : ILlmChatDefinitionUiGateway
{
    public async Task<LlmChatUiResult<LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>>> ListPageAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Read, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>>(
                LlmChatUiPermission.Read);
        }

        var result = await definitions.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, page => new LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>(
            [.. page.Items.Select(ToListItem)],
            page.NextCursor));
    }

    public async Task<LlmChatUiResult<LlmChatDefinitionListItem>> GetAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Read, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatDefinitionListItem>(LlmChatUiPermission.Read);
        }

        if (!TryCreateId(definitionId, out var id))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatDefinitionListItem>("Select a valid Simple Chat definition.");
        }

        var result = await definitions.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToListItem);
    }

    public async Task<LlmChatUiResult<LlmChatDefinitionEditor>> GetEditorAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatDefinitionEditor>(LlmChatUiPermission.Manage);
        }

        if (!TryCreateId(definitionId, out var id))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatDefinitionEditor>("Select a valid Simple Chat definition.");
        }

        var result = await definitions.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToEditor);
    }

    public async Task<LlmChatUiResult<LlmChatDefinitionEditor>> CreateAsync(
        LlmChatDefinitionMutation mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatDefinitionEditor>(LlmChatUiPermission.Manage);
        }

        var result = await definitions.CreateAsync(new CreateLlmChatDefinitionCommand(
            mutation.Name,
            mutation.Summary,
            mutation.AvatarImageUrl,
            mutation.SystemPrompt,
            mutation.ProviderProfileId,
            mutation.Model,
            ToSettings(mutation),
            mutation.Timeout,
            ToResponseFormat(mutation),
            mutation.RevisionReason,
            mutation.Tags), cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToEditor);
    }

    public async Task<LlmChatUiResult<LlmChatDefinitionEditor>> UpdateAsync(
        Guid definitionId,
        LlmChatDefinitionMutation mutation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatDefinitionEditor>(LlmChatUiPermission.Manage);
        }

        if (!TryCreateId(definitionId, out var id))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatDefinitionEditor>("Select a valid Simple Chat definition.");
        }

        var result = await definitions.UpdateAsync(new UpdateLlmChatDefinitionCommand(
            id,
            mutation.Name,
            mutation.Summary,
            mutation.AvatarImageUrl,
            mutation.SystemPrompt,
            mutation.ProviderProfileId,
            mutation.Model,
            ToSettings(mutation),
            mutation.Timeout,
            ToResponseFormat(mutation),
            mutation.RevisionReason,
            expectedConcurrencyToken,
            mutation.Tags), cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToEditor);
    }

    public async Task<LlmChatUiResult<LlmChatDefinitionListItem>> ChangeStatusAsync(
        Guid definitionId,
        LlmChatDefinitionStatus status,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatDefinitionListItem>(LlmChatUiPermission.Manage);
        }

        if (!TryCreateId(definitionId, out var id))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatDefinitionListItem>("Select a valid Simple Chat definition.");
        }

        var result = await definitions.ChangeStatusAsync(
            new ChangeLlmChatDefinitionStatusCommand(id, status, expectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToListItem);
    }

    private static bool TryCreateId(Guid value, out LlmChatDefinitionId id)
    {
        if (value == Guid.Empty)
        {
            id = default;
            return false;
        }

        id = new(value);
        return true;
    }

    private static LlmChatDefinitionListItem ToListItem(LlmChatDefinitionDetails details)
        => new(
            details.Definition.Id.Value,
            details.Definition.Name,
            details.Definition.Summary,
            details.Definition.AvatarImageUrl,
            details.Definition.Status,
            details.Definition.CurrentRevision.Value,
            details.Definition.ConcurrencyToken,
            details.Definition.UpdatedAtUtc,
            details.NormalizedTags.ToArray());

    private static LlmChatDefinitionEditor ToEditor(LlmChatDefinitionDetails details)
    {
        var revision = details.Revision;
        var (kind, schemaJson, schemaName, schemaDescription) = FromResponseFormat(revision.ResponseFormat);
        return new(
            ToListItem(details),
            revision.SystemPrompt,
            revision.ProviderProfileId,
            revision.ProviderName,
            revision.Model,
            revision.Settings.Temperature,
            LlmChatThinkingEffortMapper.FromProvider(revision.Settings.ThinkingEffort),
            revision.Settings.ModelParameterConfigurationJson,
            revision.Timeout,
            kind,
            schemaJson,
            schemaName,
            schemaDescription,
            revision.Reason);
    }

    private static LlmModelSettings ToSettings(LlmChatDefinitionMutation mutation)
        => new(mutation.Temperature, mutation.ModelParameterConfigurationJson)
        {
            ThinkingEffort = LlmChatThinkingEffortMapper.ToProvider(mutation.ThinkingEffort)
        };

    private static LlmResponseFormat? ToResponseFormat(LlmChatDefinitionMutation mutation)
        => mutation.ResponseFormat switch
        {
            LlmChatUiResponseFormatKind.Text => null,
            LlmChatUiResponseFormatKind.Json => new(true),
            LlmChatUiResponseFormatKind.JsonSchema => new(
                true,
                mutation.SchemaJson,
                mutation.SchemaName,
                mutation.SchemaDescription),
            _ => throw new ArgumentOutOfRangeException(
                nameof(mutation),
                mutation.ResponseFormat,
                "Unknown response format.")
        };

    private static (LlmChatUiResponseFormatKind Kind, string SchemaJson, string SchemaName, string SchemaDescription)
        FromResponseFormat(LlmResponseFormat? format)
        => format switch
        {
            null or { RequireJson: false } => (LlmChatUiResponseFormatKind.Text, string.Empty, string.Empty, string.Empty),
            { SchemaJson.Length: 0 } => (LlmChatUiResponseFormatKind.Json, string.Empty, string.Empty, string.Empty),
            _ => (
                LlmChatUiResponseFormatKind.JsonSchema,
                format.SchemaJson,
                format.SchemaName,
                format.SchemaDescription)
        };
}
