using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentConfigurationChanges(EditingChangeFeed feed, Func<EditingScope> readScope, IEditingUiAccess access,
    AgentConfigurationWriteContext writes, ILogger<EditingObservation> logger) {
    public EditingScope Scope => readScope();
    public EditingObservation Observe(EditingOwner owner, Guid? id = null) => new(feed, access, new(Scope, owner, id), logger);
    public IAgentCatalogCommitObserver Publisher(EditingScope scope) => new PublisherObserver(feed, scope, writes);

    public IDisposable EnterUiWrite(EditingObservation original, EditingScope originalScope, Guid editorId, Func<bool> owns, Guid? operationId = null) {
        return writes.Enter(originalScope, new(editorId, operationId ?? Guid.NewGuid()), async token => {
            if (!owns() || originalScope != Scope) {
                throw new UnauthorizedAccessException("The original agent configuration editor is no longer current.");
            }
            await original.AuthorizeAsync(true, token);
            if (!owns() || originalScope != Scope) {
                throw new UnauthorizedAccessException("The original agent configuration scope changed during authorization.");
            }
        });
    }

    private sealed class PublisherObserver(EditingChangeFeed feed, EditingScope scope, AgentConfigurationWriteContext writes) : IAgentCatalogCommitObserver {
        public void Committed(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after) {
            var agents = before.Agents.ToDictionary(item => item.Id);
            foreach (var agent in after.Agents) {
                if (!agents.Remove(agent.Id, out var original) || original.UpdatedAtUtc != agent.UpdatedAtUtc) {
                    Publish(EditingOwner.AgentDefinition, agent.Id, EditingChangeKind.EditableContentChanged);
                }
            }
            foreach (var id in agents.Keys) {
                Publish(EditingOwner.AgentDefinition, id, EditingChangeKind.Deleted);
            }
            var teams = before.AgentTeams.ToDictionary(item => item.Id);
            foreach (var team in after.AgentTeams) {
                if (!teams.Remove(team.Id, out var original) || original.UpdatedAtUtc != team.UpdatedAtUtc) {
                    Publish(EditingOwner.AgentTeam, team.Id, EditingChangeKind.EditableContentChanged);
                }
            }
            foreach (var id in teams.Keys) {
                Publish(EditingOwner.AgentTeam, id, EditingChangeKind.Deleted);
            }
            var capabilities = before.Capabilities.ToDictionary(item => item.Id);
            foreach (var capability in after.Capabilities) {
                if (!capabilities.Remove(capability.Id, out var original) || !SameConfiguration(original, capability)) {
                    Publish(EditingOwner.AgentCapability, capability.Id, EditingChangeKind.EditableContentChanged);
                }
            }
            foreach (var id in capabilities.Keys) {
                Publish(EditingOwner.AgentCapability, id, EditingChangeKind.Deleted);
            }
        }

        private static bool SameConfiguration(CapabilityCatalogItem first, CapabilityCatalogItem second)
            => first.Kind == second.Kind && first.Key == second.Key && first.Name == second.Name &&
                first.Description == second.Description && first.EndpointOrPath == second.EndpointOrPath &&
                first.ConfigurationJson == second.ConfigurationJson && first.IsBuiltIn == second.IsBuiltIn &&
                first.Tags.SequenceEqual(second.Tags, StringComparer.Ordinal);

        private void Publish(EditingOwner owner, Guid id, EditingChangeKind kind) {
            var origin = writes.Origin;
            feed.Publish(new(scope, owner, id), kind, origin?.EditorId, origin?.OperationId);
        }
    }
}
