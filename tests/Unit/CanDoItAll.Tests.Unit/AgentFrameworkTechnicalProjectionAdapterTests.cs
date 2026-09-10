using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentFrameworkTechnicalProjectionAdapterTests {
    [Fact]
    public async Task Synchronization_uses_one_owner_snapshot_with_its_profile_scope_and_revision() {
        var fixture = new Fixture();

        await fixture.Bridge.SynchronizeDirectoryProjectionAsync();

        Assert.Equal(1, fixture.Workspace.SnapshotReads);
        var projection = Assert.Single(fixture.Projection.Writes);
        Assert.Equal(fixture.Identity.DatabaseProfileId, projection.Source.DatabaseProfileId);
        Assert.Equal(fixture.Identity.WorkspaceScope, projection.Source.Scope);
        Assert.Equal(new CatalogDataRevision(3), projection.Revision);
        var agent = Assert.Single(projection.Agents);
        Assert.Equal(fixture.Agent.Id, agent.TechnicalAgentId);
        Assert.Equal("Technical name", agent.DisplayName);
        Assert.Equal("Owned instructions", agent.Instructions);
        Assert.Equal("Runtime provider", agent.ProviderName);
        Assert.Equal("chosen-model", agent.DefaultModel);
    }

    [Fact]
    public async Task Existing_technical_identity_and_lifecycle_survive_crm_provider_choices() {
        var fixture = new Fixture();

        var result = await fixture.Bridge.SaveAsync(new() {
            PartyId = fixture.PartyId, ProviderProfileId = fixture.ProviderId, DefaultModel = "new-model",
            ExecutionMode = AiExecutionMode.Remote, Notes = "New local business notes"
        });

        Assert.True(result.IsSuccess);
        var saved = Assert.IsType<AgentEditorModel>(fixture.Workspace.Saved);
        Assert.Equal("Technical name", saved.Name);
        Assert.Equal("Technical summary", saved.Summary);
        Assert.Equal("Owned instructions", saved.Instructions);
        Assert.Equal(AgentLifecycleStatus.Suspended, saved.Status);
        Assert.Equal(fixture.ProviderId, saved.ProviderProfileId);
        Assert.Equal("new-model", saved.Model);
        Assert.Equal(fixture.Agent.UpdatedAtUtc, saved.ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Projection_failure_preserves_committed_agent_and_party_identity() {
        var fixture = new Fixture();
        fixture.Projection.Failure = new IOException("CRM projection unavailable.");

        var failure = await Assert.ThrowsAsync<AgentDirectoryProjectionSynchronizationException>(() => fixture.Bridge.SaveAsync(new() {
            PartyId = fixture.PartyId, ProviderProfileId = fixture.ProviderId
        }));

        Assert.Equal(fixture.Agent.Id, failure.AgentId);
        Assert.Equal(fixture.PartyId, failure.PartyId);
        Assert.Same(fixture.Projection.Failure, failure.InnerException);
        Assert.Equal(1, fixture.Workspace.SaveCalls);
    }

    [Fact]
    public async Task Known_precommit_validation_failure_returns_failure_without_projection_write() {
        var fixture = new Fixture();
        fixture.Workspace.SaveFailure = new AgentEditorValidationException("Invalid provider configuration.");

        var result = await fixture.Bridge.SaveAsync(new() { PartyId = fixture.PartyId, ProviderProfileId = fixture.ProviderId });

        Assert.True(result.IsFailure);
        Assert.Empty(fixture.Projection.Writes);
    }

    [Fact]
    public async Task Unavailable_bound_agent_is_not_replaced_implicitly() {
        var fixture = new Fixture();
        fixture.Workspace.Agents = [];

        var result = await fixture.Bridge.SaveAsync(new() { PartyId = fixture.PartyId });

        Assert.True(result.IsFailure);
        Assert.Equal(0, fixture.Workspace.SaveCalls);
    }

    private sealed class Fixture {
        public Guid PartyId { get; } = Guid.NewGuid();
        public Guid ProviderId { get; } = Guid.NewGuid();
        public AgentDefinition Agent { get; }
        public AgentExecutionActivityWorkspaceIdentity Identity { get; } = new(Guid.NewGuid(), WorkspaceScopeDescriptor.Organization("projection-tests"), new(0));
        public ProjectionPort Projection { get; }
        public WorkspaceProxy Workspace { get; }
        public IAiTechnicalAgentBridge Bridge { get; }

        public Fixture() {
            var now = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
            Agent = new(Guid.NewGuid(), "Technical name", "Technical role", "Technical summary", "Owned instructions",
                AgentLifecycleStatus.Suspended, ProviderId, "chosen-model", AgentWorkloadKind.General, AgentChatHistoryMode.FrameworkManaged,
                0.2, false, false, "{}", false, "", AgentPermissionsPolicy.Default, [], [], now, now);
            var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceProxy>();
            Workspace = (WorkspaceProxy)(object)workspace;
            Workspace.Identity = Identity;
            Workspace.Agents = [Agent];
            Workspace.Providers = [new(ProviderId, "Runtime provider", ProviderKind.OpenAi, "https://example.test", "", "default-model",
                ProviderTransportKind.Responses, true, true, true, true, false, "{}", "", "", null, [], ProviderProfilePurpose.Chat)];
            var route = $"/agents?tab=agents&agentId={Agent.Id:D}";
            Projection = new(new(PartyId, PartyType.AiAgent, "Human CRM name", "Human CRM summary",
                new(Agent.Id, AiResourceBindingStatus.Bound, "Bound", AiExecutionMode.Remote, "Runtime provider", "chosen-model", 0, true, route),
                new(PartyId, Agent.Id, "Human CRM name", "Technical role", "Human CRM summary", "Owned instructions",
                    AiResourceBindingStatus.Bound, "Bound", AiExecutionMode.Remote, "Runtime provider", "chosen-model", "", [], [], route)));
            var type = typeof(AgentFrameworkModuleServiceCollectionExtensions).Assembly.GetType(
                "CanDoItAll.Modules.AgentFramework.AgentFrameworkAiTechnicalAgentBridge", throwOnError: true)!;
            Bridge = Assert.IsAssignableFrom<IAiTechnicalAgentBridge>(Activator.CreateInstance(type, Projection, new WorkspaceFactory(workspace, Identity.WorkspaceScope)));
        }
    }

    public class WorkspaceProxy : DispatchProxy {
        public AgentExecutionActivityWorkspaceIdentity Identity { get; set; } = null!;
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];
        public IReadOnlyList<ProviderProfile> Providers { get; set; } = [];
        public int SnapshotReads { get; private set; }
        public int SaveCalls { get; private set; }
        public AgentEditorModel? Saved { get; private set; }
        public Exception? SaveFailure { get; set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            ArgumentNullException.ThrowIfNull(method);
            if (method.Name == nameof(IAgentFrameworkWorkspaceService.LoadCatalogSnapshotAsync)) {
                SnapshotReads++;
                var catalog = SandboxWorkspaceCatalog.Empty with { Agents = Agents, Providers = Providers, CatalogDataRevision = new(3) };
                return Task.FromResult(new AgentWorkspaceCatalogSnapshot(Identity, new(catalog, catalog.CatalogDataRevision)));
            }

            if (method.Name == nameof(IAgentFrameworkWorkspaceService.GetAgentEditorAsync)) {
                return Task.FromResult(AgentEditorModel.FromDefinition(Assert.Single(Agents)));
            }

            if (method.Name == nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync)) {
                SaveCalls++;
                Saved = Assert.IsType<AgentEditorModel>(args![0]);
                return SaveFailure is null ? Task.FromResult(Assert.Single(Agents).Id) : Task.FromException<Guid>(SaveFailure);
            }

            throw new NotSupportedException($"Unexpected catalog call '{method.Name}'.");
        }
    }

    private sealed class ProjectionPort(AiTechnicalProjectionResource resource) : IAiTechnicalAgentProjectionStore {
        public List<AiTechnicalCatalogProjection> Writes { get; } = [];
        public Exception? Failure { get; set; }
        public Task<AiTechnicalProjectionApplyResult> ApplyAsync(AiTechnicalCatalogProjection projection, CancellationToken cancellationToken = default) {
            Writes.Add(projection);
            return Failure is null ? Task.FromResult(new AiTechnicalProjectionApplyResult(AiTechnicalProjectionApplyDisposition.Applied, projection.Revision))
                : Task.FromException<AiTechnicalProjectionApplyResult>(Failure);
        }

        public Task<IReadOnlyDictionary<Guid, AiTechnicalProjectionResource>> ReadAsync(IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, AiTechnicalProjectionResource>>(new Dictionary<Guid, AiTechnicalProjectionResource> { [resource.PartyId] = resource });
        public Task<int> CountBoundAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<AiTechnicalCatalogRepairFacts> ReadCatalogRepairFactsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class WorkspaceFactory(IAgentFrameworkWorkspaceService workspace, WorkspaceScopeDescriptor scope) : ICanDoItAllAgentWorkspaceFactory {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => workspace;
        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor requestedScope) => workspace;
        public WorkspaceScopeDescriptor GetOrganizationScope() => scope;
        public string GetWorkspaceRoot() => string.Empty;
    }
}
