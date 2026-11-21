using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed record RemoteAgentPreview(RemoteTemplateRow TemplateRow, RemoteAgentTemplate Document, RemoteTemplateModelRequirement Requirement)
    : RemoteTemplatePreview(TemplateRow, [Requirement]) {
    public override IReadOnlyList<string> Dependencies { get; } = Document.Capabilities
        .Select(item => $"{item.Kind} · {item.Key} ({(item.InlineSkill is null ? "local" : "included")})").ToArray();
}

public sealed class RemoteAgentTemplateImportFeature(RemoteAgentTemplateDiscovery discovery, IAgentFrameworkWorkspaceService workspace)
    : IRemoteTemplateImportFeature {
    public RemoteTemplateKind Kind => RemoteTemplateKind.Agent;

    public async Task<IReadOnlyList<RemoteTemplateRow>> ListAsync(RemoteInstanceSelection source, CancellationToken token) =>
        (await discovery.ListAsync(source.Id, source.Version, token)).Select(row => new RemoteTemplateRow(row.Id, row.PublisherId,
            row.Name, row.Summary, row.AvatarImageUrl, row.UpdatedAtUtc, row.CatalogRevision)).ToArray();

    public async Task<RemoteTemplatePreview> PreviewAsync(RemoteInstanceSelection source, RemoteTemplateRow row, CancellationToken token) {
        var selected = new RemoteAgentTemplateSummary(row.Id, row.Name, row.Summary, row.UpdatedAtUtc, 0) {
            CatalogRevision = row.CatalogRevision, PublisherId = row.PublisherId
        };
        var document = await discovery.FetchAsync(source.Id, source.Version, selected, token);
        var needsTools = document.Capabilities.Any(item => item.Kind is CapabilityKind.McpServer or CapabilityKind.Tool);
        var requirement = document.PreferredModel ?? new(document.Name, ProviderProfilePurpose.Chat);
        if (requirement.Purpose != ProviderProfilePurpose.Chat) {
            throw new RemoteInstanceReadException("The remote agent requests a model that cannot run a chat.");
        }
        return new RemoteAgentPreview(row, document, requirement with { Tools = requirement.Tools || needsTools });
    }

    public async Task<Guid> ImportAsync(RemoteInstanceSelection source, RemoteTemplatePreview preview, IReadOnlyList<RemoteTemplateModelChoice> models,
        AgentExecutionActivityWorkspaceIdentity original, CancellationToken token) {
        if (preview is not RemoteAgentPreview agent || models.Count != 1) {
            throw new RemoteInstanceReadException("A reviewed agent and a chat model are required.");
        }
        try {
            return await workspace.ImportRemoteAgentTemplateAsync(source.Id, agent.Document, original, token, models[0]);
        } catch (AgentCatalogCommittedException<Guid> committed) {
            throw new RemoteTemplateImportCommittedException(committed.Accepted, "The agent was saved, but its catalog needs refreshing.", committed);
        } catch (AgentDirectoryProjectionSynchronizationException committed) {
            throw new RemoteTemplateImportCommittedException(committed.AgentId, "The agent was saved, but its directory entry needs refreshing.", committed);
        } catch (RemoteAgentTemplateImportException error) {
            throw new RemoteInstanceReadException(error.Message);
        }
    }
}
