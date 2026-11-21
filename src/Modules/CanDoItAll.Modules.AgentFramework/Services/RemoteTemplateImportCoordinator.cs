using System.ComponentModel;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

public enum RemoteTemplateKind { Agent, SimpleChat, Workflow }

[Description("A selectable shared definition at a particular publisher revision.")]
public sealed record RemoteTemplateRow(
    [property: Description("Identity of the definition on the publisher.")] Guid Id,
    [property: Description("Public identity of the publishing instance.")] Guid PublisherId,
    [property: Description("Display name of the definition.")] string Name,
    [property: Description("Short description for the import catalog.")] string Summary,
    [property: Description("Optional avatar for the pre-import catalog.")] string? AvatarImageUrl,
    [property: Description("Time at which this definition revision was updated.")] DateTimeOffset UpdatedAtUtc,
    [property: Description("Catalog or definition concurrency revision when provided by its owner.")] long CatalogRevision = 0,
    [property: Description("Version identity when provided by the definition owner.")] Guid? VersionId = null) {
    [Description("Whether an embedded raster avatar is available on the authenticated avatar route.")]
    public bool HasEmbeddedAvatar { get; init; }
}

public abstract record RemoteTemplatePreview(RemoteTemplateRow Row, IReadOnlyList<RemoteTemplateModelRequirement> Models) {
    public virtual IReadOnlyList<string> Dependencies => [];
}

public interface IRemoteTemplateImportFeature {
    RemoteTemplateKind Kind { get; }
    Task<IReadOnlyList<RemoteTemplateRow>> ListAsync(RemoteInstanceSelection source, CancellationToken token);
    Task<RemoteTemplatePreview> PreviewAsync(RemoteInstanceSelection source, RemoteTemplateRow row, CancellationToken token);
    Task<Guid> ImportAsync(RemoteInstanceSelection source, RemoteTemplatePreview preview, IReadOnlyList<RemoteTemplateModelChoice> models,
        AgentExecutionActivityWorkspaceIdentity workspace, CancellationToken token);
}

public sealed class RemoteTemplateImportCommittedException(Guid acceptedId, string message, Exception innerException)
    : InvalidOperationException(message, innerException) {
    public Guid AcceptedId { get; } = acceptedId;
}

public sealed class RemoteTemplateImportCoordinator(RemoteTemplateConnectionReader connections, RemoteTemplateProviderCatalog providers,
    IAgentFrameworkWorkspaceService workspaces) {
    public async Task<Guid> ImportAsync(IRemoteTemplateImportFeature feature, RemoteInstanceSelection source, RemoteTemplatePreview preview,
        IReadOnlyList<RemoteTemplateModelChoice> models, AgentExecutionActivityWorkspaceIdentity workspace, CancellationToken token) {
        if (models.Count != preview.Models.Count) {
            throw new RemoteInstanceReadException("Select a provider and model for each requirement.");
        }
        await connections.RequireCurrentAsync(source.Id, source.Version, token);
        var current = await feature.PreviewAsync(source, preview.Row, token);
        if (current.Row != preview.Row || !current.Models.SequenceEqual(preview.Models)) {
            throw new RemoteInstanceReadException("The published definition or its model requirements changed. Refresh the catalog.");
        }
        if (models.Count > 0) {
            var options = await providers.ListAsync(source.Id, current.Row.PublisherId, token, feature.Kind);
            for (var i = 0; i < models.Count; i++) {
                if (!options.Any(option => option.Choice == models[i] && option.Supports(current.Models[i]))) {
                    throw new RemoteInstanceReadException("A selected provider or model is no longer available for this definition.");
                }
            }
        }
        await connections.RequireCurrentAsync(source.Id, source.Version, token);
        if (await workspaces.ReadAgentDecisionDataPolicyContextAsync(token) != workspace) {
            throw new RemoteInstanceReadException("The local workspace changed. Reopen the import dialog.");
        }
        return await feature.ImportAsync(source, current, models, workspace, token);
    }
}
