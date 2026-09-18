using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentCapabilityCorrectnessTests {
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-365)]
    public void Accepted_agent_updates_are_monotonic_and_assignment_recovery_finds_the_desired_set(int clockOffsetDays) {
        var now = DateTimeOffset.UnixEpoch.AddDays(20000);
        var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Skill, "revision-skill", "Revision skill",
            "Safe fixture", "inline://revision", "{}", CapabilityProofStatus.NotRun, "", null, false);
        var catalog = new SandboxWorkspaceCatalog("1", [], [], [capability], []);
        var initial = AgentDefinitionFactory.Create(catalog, new() { Name = "Revision fixture", ConfigurationJson = "{}" },
            Guid.NewGuid(), null, now, new ProviderProfileService(), "Fixture create");
        var attempt = new AgentCapabilityAssignmentAttempt(AgentEditorModel.FromDefinition(initial), capability.Id);
        var updated = AgentDefinitionFactory.Create(catalog with { Agents = [initial] }, attempt.CreateRequest(), initial.Id,
            initial, now.AddDays(clockOffsetDays), new ProviderProfileService(), "Fixture update");
        Assert.True(updated.UpdatedAtUtc > initial.UpdatedAtUtc, "An accepted update must advance its previous concurrency revision.");
        Assert.Equal(AgentCapabilityOperationStatus.DesiredStateSatisfied, attempt.Classify(AgentEditorModel.FromDefinition(updated)));
        var secondAttempt = new AgentCapabilityAssignmentAttempt(AgentEditorModel.FromDefinition(updated), capability.Id);
        var second = AgentDefinitionFactory.Create(catalog with { Agents = [updated] }, secondAttempt.CreateRequest(), initial.Id,
            updated, now.AddDays(clockOffsetDays), new ProviderProfileService(), "Fixture second update");
        Assert.True(second.UpdatedAtUtc > updated.UpdatedAtUtc);
        Assert.Equal(AgentCapabilityOperationStatus.DesiredStateSatisfied, secondAttempt.Classify(AgentEditorModel.FromDefinition(second)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Escaping_command_failure_finishes_its_own_entry_without_automatic_replay(bool diagnostic) {
        var commands = new EscapingCommands();
        var operations = new AgentCapabilityOperations(commands, new UnavailableReads());
        var draft = new AgentEditorModel { Id = Guid.NewGuid(), ExpectedUpdatedAtUtc = DateTimeOffset.UnixEpoch };
        var capabilityId = Guid.NewGuid();
        try {
            if (diagnostic) {
                await operations.DiagnoseAsync(draft.Id.Value, capabilityId);
            } else {
                await operations.AssignAsync(draft, capabilityId);
            }
        } catch (IOException) {
        }
        var state = Assert.IsType<AgentCapabilityOperationState>(operations.Find(draft.Id));
        Assert.False(state.IsActive);
        Assert.Equal(AgentCapabilityOperationStatus.Unconfirmed, state.Status);
        Assert.Null(await operations.AssignAsync(draft, capabilityId));
        Assert.Null(await operations.DiagnoseAsync(draft.Id.Value, capabilityId));
        Assert.Equal(1, commands.Calls);
    }

    private sealed class EscapingCommands : IAgentCapabilityCommands {
        public int Calls { get; private set; }
        public Task<AgentCapabilityOperationStatus> AssignAsync(AgentCapabilityAssignmentAttempt attempt, CancellationToken cancellationToken = default) {
            Calls++;
            return Task.FromException<AgentCapabilityOperationStatus>(new IOException("Unknown dispatch result"));
        }
        public Task<CapabilityVerificationOutcome> DiagnoseAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) {
            Calls++;
            return Task.FromException<CapabilityVerificationOutcome>(new IOException("Unknown diagnostic result"));
        }
    }

    private sealed class UnavailableReads : IAgentCapabilitiesReads {
        public Task<AgentEditorModel> ReadEditorAsync(Guid agentId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The liveness test must not read or replay automatically.");
        public Task<AgentCapabilitiesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The liveness test must not read or replay automatically.");
    }
}
