using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

public static class RemoteSimpleChatRoutes {
    public const string Catalog = "api/shared-simple-chats/v1/templates";
    public static string Details(Guid id) => $"{Catalog}/{id:D}";
    public static string Avatar(Guid id) => $"{Details(id)}/avatar";
}

public sealed class RemoteSimpleChatPublisher(ILlmChatDefinitionApplicationService definitions, ILlmChatTemplateSummaryReader summaries,
    RemoteTemplatePublicationStore publications, RemoteTemplateProviderCatalog providers, SharedProviderServiceIdentityStore identities,
    IAgentFrameworkWorkspaceService workspace) {
    public async Task<IReadOnlyList<RemoteTemplateRow>> ListAsync(CancellationToken token) {
        var opening = await workspace.ReadAgentDecisionDataPolicyContextAsync(token);
        var publisherId = await identities.GetOrCreateAsync(token);
        var published = await publications.ListAsync(RemoteTemplateKind.SimpleChat, token);
        var rows = await summaries.ListAsync(published, token);
        var current = await publications.ListAsync(RemoteTemplateKind.SimpleChat, token);
        await RequireCurrentAsync(opening, token);
        return rows.Where(row => current.Contains(row.Id)).Select(row => new RemoteTemplateRow(row.Id, publisherId.Value, row.Name, row.Summary,
            RemoteTemplateAvatar.ForDisplay(row.AvatarImageUrl), row.UpdatedAtUtc, row.Revision) { HasEmbeddedAvatar = row.HasEmbeddedAvatar }).ToArray();
    }

    public async Task<RemoteTemplateEmbeddedAvatar?> FindAvatarAsync(Guid id, CancellationToken token) {
        var opening = await workspace.ReadAgentDecisionDataPolicyContextAsync(token);
        if (!await publications.IsPublishedAsync(RemoteTemplateKind.SimpleChat, id, token)) {
            return null;
        }
        var avatar = await summaries.ReadAvatarAsync(id, token);
        if (avatar is null || RemoteTemplateAvatar.ForDisplay(avatar.ImageUrl) is not { } image) {
            return null;
        }
        var publisherId = await identities.GetOrCreateAsync(token);
        if (!await publications.IsPublishedAsync(RemoteTemplateKind.SimpleChat, id, token)) {
            return null;
        }
        await RequireCurrentAsync(opening, token);
        return new(publisherId.Value, id, avatar.Revision, image);
    }

    public async Task SetPublicationAsync(Guid id, int expectedRevision, bool publish, Func<CancellationToken, ValueTask> authorize, CancellationToken token) {
        await authorize(token);
        if (publish) {
            await RequireRevisionAsync(id, expectedRevision, true, token);
        }
        await publications.SetAsync(RemoteTemplateKind.SimpleChat, id, publish, token, authorize);
    }

    public Task SetSharingAsync(Guid id, int revision, DefinitionSharingSettings expected, DefinitionSharingSettings desired,
        Func<CancellationToken, ValueTask> authorize, CancellationToken token) =>
        publications.SetSharingAsync(RemoteTemplateKind.SimpleChat, id, expected, desired, async ct => {
            await authorize(ct);
            await RequireRevisionAsync(id, revision, !expected.SharedTemplate && desired.SharedTemplate, ct);
            await authorize(ct);
        }, token);

    private async Task RequireRevisionAsync(Guid id, int revision, bool publishing, CancellationToken token) {
        var result = await definitions.GetAsync(new(id), token);
        if (result.IsFailure || result.Value!.Definition.CurrentRevision.Value != revision) {
            throw new RemoteInstanceReadException("The Simple Chat revision changed. Reopen the definition to change sharing.");
        }
        if (publishing) {
            if (result.Value.Definition.Status is not (LlmChatDefinitionStatus.Draft or LlmChatDefinitionStatus.Active)) {
                throw new RemoteInstanceReadException("Only a draft or active Simple Chat can be shared as a template.");
            }
            RemoteSimpleChatSettings.From(result.Value.Revision.Settings);
        }
    }

    public async Task<RemoteSimpleChatTemplate?> FindAsync(Guid id, CancellationToken token) {
        var opening = await workspace.ReadAgentDecisionDataPolicyContextAsync(token);
        if (id == Guid.Empty || !await publications.IsPublishedAsync(RemoteTemplateKind.SimpleChat, id, token)) {
            return null;
        }
        var result = await definitions.GetAsync(new(id), token);
        if (result.IsFailure) {
            if (result.Errors.All(error => error.Code == LlmChatErrorCodes.DefinitionNotFound)) {
                return null;
            }
            throw new RemoteInstanceReadException("The shared Simple Chat revision could not be read.");
        }
        var details = result.Value!;
        if (details.Definition.Status is not (LlmChatDefinitionStatus.Draft or LlmChatDefinitionStatus.Active)) {
            return null;
        }
        var publisherId = await identities.GetOrCreateAsync(token);
        var revision = details.Revision;
        var model = await providers.DescribeAsync(new(revision.Name, CanDoItAll.AgentFramework.Models.ProviderProfilePurpose.Chat,
                StructuredOutput: revision.ResponseFormat?.RequireJson == true || !string.IsNullOrWhiteSpace(revision.ResponseFormat?.SchemaJson),
                ThinkingEffort: revision.Settings.ThinkingEffort),
            revision.ProviderProfileId, revision.Model, token);
        if (!await publications.IsPublishedAsync(RemoteTemplateKind.SimpleChat, id, token)) {
            return null;
        }
        await RequireCurrentAsync(opening, token);
        var row = new RemoteTemplateRow(id, publisherId.Value, revision.Name, revision.Summary, RemoteTemplateAvatar.ForDisplay(revision.AvatarImageUrl),
            details.Definition.UpdatedAtUtc, revision.Revision.Value) {
            HasEmbeddedAvatar = revision.AvatarImageUrl.StartsWith(LlmChatDefinitionValidation.EmbeddedAvatarPrefix, StringComparison.Ordinal)
        };
        return new(row, revision.SystemPrompt, details.NormalizedTags, RemoteSimpleChatSettings.From(revision.Settings), revision.Timeout,
            revision.ResponseFormat is { } format ? new(format.RequireJson, format.SchemaJson, format.SchemaName, format.SchemaDescription) : null, model);
    }

    private async Task RequireCurrentAsync(AgentExecutionActivityWorkspaceIdentity opening, CancellationToken token) {
        if (opening != await workspace.ReadAgentDecisionDataPolicyContextAsync(token)) {
            throw new RemoteInstanceReadException("The publisher workspace changed. Refresh the Simple Chat catalog.");
        }
    }
}
