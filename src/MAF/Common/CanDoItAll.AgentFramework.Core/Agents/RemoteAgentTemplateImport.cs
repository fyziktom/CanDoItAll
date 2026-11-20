using CanDoItAll.AgentFramework.Models;
using CapabilityKey = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKey;
using SkillName = CanDoItAll.AgentFramework.Capabilities.Abstractions.SkillName;

namespace CanDoItAll.AgentFramework.Core;

public sealed class RemoteAgentTemplateImportException(string message) : InvalidOperationException(message);

internal sealed class RemoteAgentTemplateImport(
    ISandboxWorkspaceCatalogStore store, IProviderProfileService providers,
    AgentExecutionActivityWorkspaceIdentity workspace, IAgentExecutionProfileGenerationSource generation) {
    private const int MaximumKeyLength = 200;

    public async Task<Guid> ImportAsync(Guid sourceId, RemoteAgentTemplate template, AgentExecutionActivityWorkspaceIdentity expectedWorkspace,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(template);
        if (sourceId == Guid.Empty || template.Id == Guid.Empty || template.UpdatedAtUtc == default ||
            !Text(template.Name, 512) || !Text(template.RoleTitle, 1024, true) || !Text(template.Summary, 64 * 1024, true) ||
            !Text(template.Instructions, 1024 * 1024, true) || !Enum.IsDefined(template.Workload) ||
            template.Tags is not { Count: <= 128 } || template.Tags.Any(tag => !Text(tag, 256)) ||
            template.Capabilities is not { Count: <= 256 }) {
            throw InvalidTemplate();
        }

        var origin = new AgentExternalIdentity($"remote-instance-{sourceId:N}", template.Id.ToString("N"));
        var id = Guid.NewGuid();
        RequireOriginalWorkspace();
        try {
            var accepted = await store.UpdateCatalogAsync(catalog => {
                RequireOriginalWorkspace();
                var existing = FindBinding(catalog, origin);
                if (existing is not null) {
                    if (!catalog.Agents.Any(agent => agent.Id == existing.AgentId)) {
                        throw new RemoteAgentTemplateImportException("The existing local template binding points to a missing agent.");
                    }
                    return catalog;
                }
                return Materialize(catalog, origin, id, template);
            }, _ => RequireOriginalWorkspace(), cancellationToken);
            return FindBinding(accepted, origin)!.AgentId;
        } catch (SandboxWorkspaceCatalogCommittedException committed) {
            throw new AgentCatalogCommittedException<Guid>(FindBinding(committed.Accepted, origin)!.AgentId, committed);
        }

        void RequireOriginalWorkspace() {
            if (workspace != expectedWorkspace || generation.GetGeneration() != workspace.DatabaseProfileGeneration) {
                throw new RemoteAgentTemplateImportException("The active workspace changed. Select the remote template again.");
            }
        }
    }

    private SandboxWorkspaceCatalog Materialize(SandboxWorkspaceCatalog catalog, AgentExternalIdentity origin, Guid id, RemoteAgentTemplate template) {
        var capabilities = catalog.Capabilities.ToList();
        var selected = new List<Guid>(template.Capabilities.Count);
        var seen = new HashSet<(CapabilityKind, string)>();
        foreach (var requirement in template.Capabilities) {
            if (requirement is null || !Enum.IsDefined(requirement.Kind) || requirement.Kind == CapabilityKind.Memory ||
                !Text(requirement.Key, MaximumKeyLength) || !CapabilityKey.TryCreate(requirement.Key, out var key) || key.Value != requirement.Key ||
                !seen.Add((requirement.Kind, requirement.Key))) {
                throw InvalidTemplate();
            }
            if (requirement.InlineSkill is { } inline) {
                if (requirement.Kind != CapabilityKind.Skill) {
                    throw InvalidTemplate();
                }
                var item = MaterializeSkill(id, requirement.Key, inline);
                if (capabilities.Any(capability => capability.Kind == item.Kind && StringComparer.OrdinalIgnoreCase.Equals(capability.Key, item.Key))) {
                    throw new RemoteAgentTemplateImportException("The generated local skill key is already in use.");
                }
                capabilities.Add(item);
                selected.Add(item.Id);
                continue;
            }
            var matches = catalog.Capabilities.Where(capability => capability.Kind == requirement.Kind &&
                StringComparer.OrdinalIgnoreCase.Equals(capability.Key, requirement.Key)).Take(2).ToArray();
            if (matches.Length != 1) {
                throw new RemoteAgentTemplateImportException($"Install or resolve the local {requirement.Kind} capability '{requirement.Key}' before importing this template.");
            }
            selected.Add(matches[0].Id);
        }
        var candidate = catalog with { Capabilities = capabilities };
        var editor = new AgentEditorModel {
            Name = template.Name,
            RoleTitle = template.RoleTitle,
            Summary = template.Summary,
            Instructions = template.Instructions,
            Workload = template.Workload,
            Tags = [.. template.Tags],
            TemplateKey = $"remote-{id:N}",
            Status = AgentLifecycleStatus.Draft,
            Permissions = AgentPermissionsPolicy.Default with { CanAskOtherAgents = false },
            SelectedCapabilityIds = selected
        };
        var agent = AgentDefinitionFactory.Create(candidate, editor, id, null, DateTimeOffset.UtcNow, providers, "Remote template import");
        var binding = new AgentExternalBindingRecord(origin.Namespace, origin.Key, id, AgentConfigurationVersion.Create(agent), false, agent.UpdatedAtUtc);
        return candidate with {
            Agents = candidate.Agents.Append(agent).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            AgentExternalBindings = [.. candidate.AgentExternalBindings, binding]
        };
    }

    private static CapabilityCatalogItem MaterializeSkill(Guid agentId, string originalKey, RemoteAgentInlineSkill skill) {
        if (!Text(skill.Name, 128) || !SkillName.TryCreate(skill.Name, out _) || !Text(skill.Description, 512) ||
            !Text(skill.Instructions, 512 * 1024) || skill.Resources is not { Count: <= 128 } ||
            skill.Resources.Any(resource => resource is null || !Text(resource.Name, 256) || !Text(resource.Content, 512 * 1024) ||
                resource.Description?.Length > 4096) ||
            skill.Resources.Select(resource => resource.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != skill.Resources.Count) {
            throw InvalidTemplate();
        }
        var key = $"remote-{agentId:N}-{originalKey}";
        return new(Guid.NewGuid(), CapabilityKind.Skill, key, skill.Name, skill.Description, $"inline://{key}",
            RemoteAgentTemplateCatalog.InlineConfiguration(skill), CapabilityProofStatus.NotRun, string.Empty, null, false);
    }

    private static AgentExternalBindingRecord? FindBinding(SandboxWorkspaceCatalog catalog, AgentExternalIdentity origin) =>
        catalog.AgentExternalBindings.SingleOrDefault(binding => binding.Namespace == origin.Namespace && binding.Key == origin.Key);

    private static bool Text(string? value, int maximumLength, bool allowEmpty = false) =>
        value is not null && value.Length <= maximumLength && (allowEmpty || !string.IsNullOrWhiteSpace(value));

    private static RemoteAgentTemplateImportException InvalidTemplate() => new("The remote template contains invalid or unsupported definition data.");
}
