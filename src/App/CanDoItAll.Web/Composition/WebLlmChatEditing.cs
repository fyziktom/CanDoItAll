using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Web.Composition;

internal sealed class WebLlmChatEditorAccess(EditingChangeFeed feed, IEditingUiAccess access, WebCurrentPrincipalResolver principals,
    ICanonicalRuntimeDatabase database, IDatabaseRuntimeState runtime, ILogger<EditingObservation> logger)
    : ILlmChatDefinitionEditorAccess, ILlmChatConversationEditorAccess {
    public Task<LlmChatEditorOpening> OpenAsync(Guid? target, Guid editorId, Func<Task> changed,
        Func<Task> retired, CancellationToken cancellationToken)
        => OpenAsync(target, editorId, true, EditingOwner.SimpleChatDefinition, LlmChatUiPermission.Manage, changed, retired, cancellationToken);

    public Task<LlmChatEditorOpening> OpenCatalogAsync(Func<Task> changed, Func<Task> retired, CancellationToken cancellationToken)
        => OpenAsync(null, Guid.NewGuid(), false, EditingOwner.SimpleChatDefinition, LlmChatUiPermission.Read, changed, retired, cancellationToken);

    public Task<LlmChatEditorOpening> OpenAsync(Guid? conversationId, LlmChatConversationAccessKind kind, Guid editorId,
        Func<Task> changed, Func<Task> retired, CancellationToken cancellationToken)
        => OpenAsync(conversationId, editorId, false, EditingOwner.SimpleChatConversation, kind switch {
            LlmChatConversationAccessKind.Read => LlmChatUiPermission.Read,
            LlmChatConversationAccessKind.Metadata or LlmChatConversationAccessKind.Recovery => LlmChatUiPermission.Manage,
            LlmChatConversationAccessKind.Execute => LlmChatUiPermission.Execute,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        }, changed, retired, cancellationToken,
            kind is LlmChatConversationAccessKind.Execute or LlmChatConversationAccessKind.Recovery
                ? LlmChatWriteResource.ConversationOperation : LlmChatWriteResource.ConversationMetadata);

    private async Task<LlmChatEditorOpening> OpenAsync(Guid? target, Guid editorId, bool editor, EditingOwner owner,
        LlmChatUiPermission permission, Func<Task> changed,
        Func<Task> retired, CancellationToken cancellationToken, LlmChatWriteResource resource = LlmChatWriteResource.Definition) {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = CurrentScope();
        var observation = new EditingObservation(feed, access, new(scope, owner, target), logger);
        LlmChatEditorOpening? opening = null;
        try {
            await observation.StartAsync(batch => batch.RequiresResync || batch.Changes.Any(item => item.OriginEditorId != editorId && (!target.HasValue && !editor ||
                    item.Resource.ResourceId == (opening?.Target ?? target) && (opening?.Target ?? target).HasValue)
                ) ? changed() : Task.CompletedTask, retired, catalog: !target.HasValue);
            cancellationToken.ThrowIfCancellationRequested();
            opening = new(target, editorId, async (write, token) => {
                if (scope != CurrentScope()) {
                    throw new UnauthorizedAccessException("The original Simple Chat dataset was retired.");
                }
                await observation.AuthorizeAsync(editor || write && permission == LlmChatUiPermission.Manage, token);
                if (write && permission == LlmChatUiPermission.Read) {
                    throw new UnauthorizedAccessException("This Simple Chat opening is read-only.");
                }
                if (write && permission == LlmChatUiPermission.Execute) {
                    var principal = await principals.ResolveAsync(token);
                    if (principal.Stamp != observation.PrincipalStamp || !principal.IsLocalOperator &&
                        !await principals.HasScopeAsync(principal, ApiAuthorizationPolicies.ExecuteLlmChats, ApiAccessScopeNames.ExecuteLlmChats, token)) {
                        throw new UnauthorizedAccessException("The original conversation execution access changed.");
                    }
                    await observation.AuthorizeAsync(false, token);
                }
                if (scope != CurrentScope()) {
                    throw new UnauthorizedAccessException("The original Simple Chat dataset changed during authorization.");
                }
            }, observation.Lifetime, observation.Stop, observation.DisposeAsync, resource);
            return opening;
        } catch {
            await observation.DisposeAsync();
            throw;
        }
    }

    private EditingScope CurrentScope() {
        var current = runtime.GetSnapshot();
        if (current.ActiveProfileId != database.Profile.Profile.Id || current.Generation != database.Generation ||
            current.ActiveFingerprint != database.Profile.Profile.Runtime.Fingerprint) {
            throw new UnauthorizedAccessException("The original Simple Chat database activation is no longer current.");
        }
        return new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation);
    }
}

internal sealed class WebLlmChatConversationCommitObserver(ICanonicalRuntimeDatabase database, EditingChangeFeed feed)
    : ILlmChatConversationCommitObserver {
    public void Committed(Guid conversationId, bool lifecycle) => feed.Publish(new(
        new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation), EditingOwner.SimpleChatConversation, conversationId),
        lifecycle ? EditingChangeKind.LifecycleChanged : EditingChangeKind.EditableContentChanged, LlmChatWriteAdmission.OriginEditorId);
}

internal sealed class WebLlmChatDefinitionCommitObserver(ICanonicalRuntimeDatabase database, EditingChangeFeed feed)
    : ILlmChatDefinitionCommitObserver {
    public void Committed(Guid id, bool lifecycle) => feed.Publish(new(
        new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation), EditingOwner.SimpleChatDefinition, id),
        lifecycle ? EditingChangeKind.LifecycleChanged : EditingChangeKind.EditableContentChanged, LlmChatWriteAdmission.OriginEditorId);
}
