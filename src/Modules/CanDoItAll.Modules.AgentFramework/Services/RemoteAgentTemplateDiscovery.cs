using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class RemoteAgentTemplateDiscovery(RemoteTemplateConnectionReader connection) {
    public async Task<IReadOnlyList<RemoteAgentTemplateSummary>> ListAsync(Guid sourceId, Guid version, CancellationToken token) {
        var rows = await connection.ReadAsync<RemoteAgentTemplateSummary[]>(sourceId, version, RemoteAgentTemplateRoutes.Catalog, token);
        if (rows.Any(row => row is null || row.Id == Guid.Empty || row.UpdatedAtUtc == default ||
                row.PublisherId == Guid.Empty || row.PublisherId != rows[0].PublisherId ||
                row.CatalogRevision <= 0 || row.CatalogRevision != rows[0].CatalogRevision ||
                string.IsNullOrWhiteSpace(row.Name) || row.Name.Length > 512 || row.CapabilityCount < 0) ||
            rows.DistinctBy(row => row.Id).Count() != rows.Length) {
            throw new RemoteInstanceReadException("The remote template catalog contains invalid entries.");
        }
        for (var i = 0; i < rows.Length; i++) {
            var row = rows[i];
            if (!row.HasEmbeddedAvatar) {
                rows[i] = row with { AvatarImageUrl = RemoteTemplateAvatar.ForDisplay(row.AvatarImageUrl) };
                continue;
            }
            var avatar = await connection.ReadAsync<RemoteTemplateEmbeddedAvatar>(sourceId, version, RemoteAgentTemplateRoutes.Avatar(row.Id), token);
            if (avatar.TemplateId != row.Id || avatar.PublisherId != row.PublisherId || avatar.CatalogRevision != row.CatalogRevision ||
                RemoteTemplateAvatar.ForDisplay(avatar.ImageUrl) is not { } image) {
                throw new RemoteInstanceReadException("The template avatar changed or is unavailable. Refresh the catalog.");
            }
            rows[i] = row with { AvatarImageUrl = image };
        }
        return rows;
    }

    public async Task<RemoteAgentTemplate> FetchAsync(Guid sourceId, Guid version, RemoteAgentTemplateSummary selected, CancellationToken token) {
        var template = await connection.ReadAsync<RemoteAgentTemplate>(sourceId, version, RemoteAgentTemplateRoutes.Details(selected.Id), token);
        if (template.Id != selected.Id || template.UpdatedAtUtc != selected.UpdatedAtUtc ||
            template.PublisherId != selected.PublisherId || template.CatalogRevision != selected.CatalogRevision ||
            template.Capabilities is null || template.Capabilities.Any(item => item is null)) {
            throw new RemoteInstanceReadException("The selected template changed or is unavailable. Refresh the catalog.");
        }
        return template;
    }

    public Task<RemoteInstanceConnection> RequireCurrentAsync(Guid sourceId, Guid version, CancellationToken token) =>
        connection.RequireCurrentAsync(sourceId, version, token);
}
