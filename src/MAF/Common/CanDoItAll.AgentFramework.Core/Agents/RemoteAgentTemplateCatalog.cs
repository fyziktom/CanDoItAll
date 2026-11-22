using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.AgentFramework.Core;

public static class RemoteAgentTemplateCatalog {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Inline = "inline";

    public static IReadOnlyList<RemoteAgentTemplateSummary> List(SandboxWorkspaceCatalog catalog, Guid publisherId) =>
        catalog.Agents.Where(IsPublished).OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(agent => agent.Id)
            .Select(agent => Summary(agent, publisherId, catalog.CatalogDataRevision.Value))
            .ToArray();

    private static RemoteAgentTemplateSummary Summary(AgentDefinition agent, Guid publisherId, long revision) {
        var avatar = RemoteTemplateAvatar.ForDisplay(agent.AvatarImageUrl);
        var embedded = IsEmbedded(avatar);
        return new(agent.Id, agent.Name, agent.Summary, agent.UpdatedAtUtc, agent.Capabilities.Count) {
            PublisherId = publisherId, CatalogRevision = revision, HasEmbeddedAvatar = embedded, AvatarImageUrl = embedded ? null : avatar
        };
    }

    public static RemoteAgentTemplate? Find(SandboxWorkspaceCatalog catalog, Guid publisherId, Guid id) {
        var agent = catalog.Agents.SingleOrDefault(item => item.Id == id && IsPublished(item));
        if (agent is null) {
            return null;
        }
        var capabilities = catalog.Capabilities.ToDictionary(item => item.Id);
        var requirements = agent.Capabilities.Select(assignment => {
            if (!capabilities.TryGetValue(assignment.CapabilityId, out var item) || assignment.Kind != item.Kind ||
                !StringComparer.OrdinalIgnoreCase.Equals(assignment.CapabilityKey, item.Key)) {
                throw new InvalidOperationException("The template has an unresolved capability assignment.");
            }
            return new RemoteAgentTemplateCapability(item.Kind, item.Key, ReadInlineSkill(item));
        }).ToArray();
        return new(id, agent.Name, agent.RoleTitle, agent.Summary, agent.Instructions, agent.Workload,
            DefinitionSharingTags.Custom(agent.Tags), requirements, agent.UpdatedAtUtc) {
            PublisherId = publisherId, CatalogRevision = catalog.CatalogDataRevision.Value,
            AvatarImageUrl = RemoteTemplateAvatar.ForDisplay(agent.AvatarImageUrl)
        };
    }

    private static bool IsPublished(AgentDefinition agent) => agent.SharesRemoteTemplate && agent.Status is AgentLifecycleStatus.Draft or AgentLifecycleStatus.Active;

    private static bool IsEmbedded(string? url) => url?.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) == true;

    public static RemoteTemplateEmbeddedAvatar? FindAvatar(SandboxWorkspaceCatalog catalog, Guid publisherId, Guid id) {
        var agent = catalog.Agents.SingleOrDefault(item => item.Id == id && IsPublished(item));
        return IsEmbedded(agent?.AvatarImageUrl) && RemoteTemplateAvatar.ForDisplay(agent?.AvatarImageUrl) is { } url
            ? new(publisherId, id, catalog.CatalogDataRevision.Value, url) : null;
    }

    internal static string InlineConfiguration(RemoteAgentInlineSkill skill) =>
        JsonSerializer.Serialize(new SkillConfiguration(Inline, skill), JsonOptions);

    internal static RemoteAgentInlineSkill? ReadInlineSkill(CapabilityCatalogItem item) {
        if (item.Kind != CapabilityKind.Skill || string.IsNullOrWhiteSpace(item.ConfigurationJson)) {
            return null;
        }
        var config = JsonSerializer.Deserialize<SkillConfiguration>(item.ConfigurationJson, JsonOptions);
        if (config?.SkillSource is { Length: > 0 } source && !StringComparer.OrdinalIgnoreCase.Equals(source, Inline)) {
            return null;
        }
        if (config?.InlineSkill is { } skill) {
            return skill with {
                Name = string.IsNullOrWhiteSpace(skill.Name) ? item.Key : skill.Name,
                Description = string.IsNullOrWhiteSpace(skill.Description) ? item.Description : skill.Description,
                Resources = skill.Resources ?? []
            };
        }
        if (StringComparer.OrdinalIgnoreCase.Equals(config?.SkillSource, Inline) || item.EndpointOrPath.StartsWith("inline://", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("The inline skill has no portable definition.");
        }
        return null;
    }

    private sealed record SkillConfiguration(string? SkillSource, RemoteAgentInlineSkill? InlineSkill);
}
