using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class RemoteSimpleChatImportFeature(RemoteTemplateConnectionReader reader, ILlmChatDefinitionCreateReceiptService receipts,
    IAgentFrameworkWorkspaceService workspace) : IRemoteTemplateImportFeature {
    public RemoteTemplateKind Kind => RemoteTemplateKind.SimpleChat;

    public async Task<IReadOnlyList<RemoteTemplateRow>> ListAsync(RemoteInstanceSelection source, CancellationToken token) {
        var rows = await reader.ReadAsync<RemoteTemplateRow[]>(source.Id, source.Version, RemoteSimpleChatRoutes.Catalog, token);
        if (rows.Any(row => row is null || row.Id == Guid.Empty || row.PublisherId == Guid.Empty || row.PublisherId != rows[0].PublisherId ||
                row.CatalogRevision < 1 || row.UpdatedAtUtc == default || string.IsNullOrWhiteSpace(row.Name) ||
                row.Name.Length > LlmChatDefinitionValidation.MaximumNameLength || row.Summary is null ||
                row.Summary.Length > LlmChatDefinitionValidation.MaximumSummaryLength ||
                row.AvatarImageUrl is { } avatar && (row.HasEmbeddedAvatar || avatar.Length > LlmChatDefinitionValidation.MaximumAvatarImageUrlLength ||
                    avatar.StartsWith(LlmChatDefinitionValidation.EmbeddedAvatarPrefix, StringComparison.OrdinalIgnoreCase))) ||
            rows.DistinctBy(row => row.Id).Count() != rows.Length) {
            throw new RemoteInstanceReadException("The remote Simple Chat catalog contains invalid definitions.");
        }
        for (var index = 0; index < rows.Length; index++) {
            var row = rows[index];
            if (!row.HasEmbeddedAvatar) {
                rows[index] = row with { AvatarImageUrl = RemoteTemplateAvatar.ForDisplay(row.AvatarImageUrl) };
                continue;
            }
            var avatar = await reader.ReadAsync<RemoteTemplateEmbeddedAvatar>(source.Id, source.Version, RemoteSimpleChatRoutes.Avatar(row.Id), token);
            if (avatar.TemplateId != row.Id || avatar.PublisherId != row.PublisherId || avatar.CatalogRevision != row.CatalogRevision ||
                RemoteTemplateAvatar.ForDisplay(avatar.ImageUrl) is not { } image ||
                !image.StartsWith(LlmChatDefinitionValidation.EmbeddedAvatarPrefix, StringComparison.Ordinal)) {
                throw new RemoteInstanceReadException("The shared Simple Chat avatar changed. Refresh the catalog.");
            }
            rows[index] = row with { AvatarImageUrl = image };
        }
        return rows;
    }

    public async Task<RemoteTemplatePreview> PreviewAsync(RemoteInstanceSelection source, RemoteTemplateRow row, CancellationToken token) {
        var document = await reader.ReadAsync<RemoteSimpleChatTemplate>(source.Id, source.Version, RemoteSimpleChatRoutes.Details(row.Id), token);
        if (document.Row is null || (document.Row with { AvatarImageUrl = RemoteTemplateAvatar.ForDisplay(document.Row.AvatarImageUrl) }) != row ||
            document.Settings is not { ModelParameterConfigurationJson: not null } || document.Tags is null ||
            document.SystemPrompt is null or { Length: > LlmMessage.MaximumTextLength } ||
            document.ResponseFormat?.SchemaName is { Length: > LlmChatDefinitionValidation.MaximumSchemaNameLength } ||
            document.ResponseFormat?.SchemaDescription is { Length: > LlmChatDefinitionValidation.MaximumSchemaDescriptionLength } ||
            document.Model is not { Purpose: ProviderProfilePurpose.Chat } ||
            document.Model.ThinkingEffort is { } advertised && advertised != document.Settings.ThinkingEffort ||
            (document.ResponseFormat?.RequireJson == true || !string.IsNullOrWhiteSpace(document.ResponseFormat?.SchemaJson)) && !document.Model.StructuredOutput) {
            throw new RemoteInstanceReadException("The selected Simple Chat changed or is invalid. Refresh the remote catalog.");
        }
        try {
            LlmChatDefinitionValidation.ValidateSettings(document.Settings.ToLocal(), document.Timeout);
            LlmChatDefinitionValidation.NormalizeTags(DefinitionSharingTags.Custom(document.Tags));
            LlmChatDefinitionValidation.NormalizeAvatarImageUrl(row.AvatarImageUrl);
        } catch (ArgumentException) {
            throw new RemoteInstanceReadException("The shared Simple Chat has invalid settings, tags or avatar.");
        }
        return new Preview(document with { Row = row, Model = document.Model with { ThinkingEffort = document.Settings.ThinkingEffort } });
    }

    public async Task<Guid> ImportAsync(RemoteInstanceSelection source, RemoteTemplatePreview preview, IReadOnlyList<RemoteTemplateModelChoice> models,
        AgentExecutionActivityWorkspaceIdentity original, CancellationToken token) {
        if (preview is not Preview chat || models is not [var choice] || choice.ProviderId == Guid.Empty || string.IsNullOrWhiteSpace(choice.Model)) {
            throw new RemoteInstanceReadException("Choose a provider and model for the selected Simple Chat.");
        }
        await reader.RequireCurrentAsync(source.Id, source.Version, token);
        await RequireWorkspaceAsync(original, token);
        var row = chat.Row;
        var key = new LlmChatDefinitionCreateKey(new("remote-simple-chat-import", source.Id.ToString("D"), row.PublisherId.ToString("D")), new(row.Id));
        var existing = await receipts.FindReceiptAsync(key, token);
        if (existing.IsFailure) {
            throw new RemoteInstanceReadException("The existing local import could not be verified.");
        }
        if (existing.Value is { } previous) {
            return previous.DefinitionId.Value;
        }
        var fresh = (Preview)await PreviewAsync(source, row, token);
        if (fresh.Document.Model != chat.Document.Model) {
            throw new RemoteInstanceReadException("The shared model changed. Refresh the Simple Chat before importing.");
        }
        await RequireWorkspaceAsync(original, token);
        var template = fresh.Document;
        var create = new CreateLlmChatDefinitionCommand(row.Name, row.Summary, row.AvatarImageUrl ?? string.Empty, template.SystemPrompt,
            choice.ProviderId, choice.Model, template.Settings.ToLocal(), template.Timeout, template.ResponseFormat?.ToLocal(),
            "Imported shared Simple Chat", DefinitionSharingTags.Custom(template.Tags));
        Result<LlmChatDefinitionCreateResponse> result;
        try {
            result = await receipts.CreateOnceAsync(new(key, create), token);
        } catch (LlmChatCommittedException<Result<LlmChatDefinitionCreateResponse>> saved) when (saved.Accepted is { IsSuccess: true, Value: { } accepted }) {
            throw new RemoteTemplateImportCommittedException(accepted.Receipt.DefinitionId.Value, "The Simple Chat was saved. Refresh the local catalog.", saved);
        }
        if (result.IsSuccess) {
            return result.Value!.Receipt.DefinitionId.Value;
        }
        if (result.Errors.Any(error => error.Code == LlmChatErrorCodes.DefinitionCreateIntentConflict) &&
            await receipts.FindReceiptAsync(key, token) is { IsSuccess: true, Value: { } concurrent }) {
            return concurrent.DefinitionId.Value;
        }
        if (result.Errors.Any(error => error.Code == LlmChatErrorCodes.InvalidRequest)) {
            throw new RemoteInstanceReadException("The shared Simple Chat contains invalid instructions or model settings. Check the published definition.");
        }
        throw new RemoteInstanceReadException("The local Simple Chat could not be created. Check that the provider supports the selected model, thinking effort and response format.");
    }

    private async Task RequireWorkspaceAsync(AgentExecutionActivityWorkspaceIdentity expected, CancellationToken token) {
        if (expected != await workspace.ReadAgentDecisionDataPolicyContextAsync(token)) {
            throw new RemoteInstanceReadException("The local workspace changed. Reopen the Simple Chat import.");
        }
    }

    private sealed record Preview(RemoteSimpleChatTemplate Document) : RemoteTemplatePreview(Document.Row, [Document.Model]);
}
