using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentExternalProvisioningServiceTests
{
    [Fact]
    public async Task UpsertAsync_EquivalentNormalizedRequest_ReplaysStableFingerprint()
    {
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var sut = new AgentExternalProvisioningService(store, new ProviderProfileService());
        var firstEditor = CreateEditor();
        firstEditor.Tags = [" beta ", "alpha", "alpha"];
        var firstCommand = new AgentExternalProvisioningCommand(
            " Partner-System ",
            " Review_Agent ",
            " provisioning-001 ",
            ExpectedConfigurationVersion: null,
            firstEditor);

        var first = await sut.UpsertAsync(firstCommand);
        var replayed = await sut.UpsertAsync(
            new AgentExternalProvisioningCommand(
                "partner-system",
                "review_agent",
                "provisioning-001",
                ExpectedConfigurationVersion: null,
                CreateEditor()));

        Assert.Equal("partner-system", first.Namespace);
        Assert.Equal("review_agent", first.Key);
        Assert.True(first.Created);
        Assert.False(first.Replayed);
        Assert.Equal(first with { Replayed = true }, replayed);
        var binding = Assert.Single(store.Document.AgentExternalBindings);
        Assert.Equal(first.AgentId, binding.AgentId);
        Assert.Equal(first.ConfigurationVersion, binding.ConfigurationVersion);
        var agent = Assert.Single(store.Document.Agents);
        Assert.Equal(["alpha", "beta"], agent.Tags);
        var operation = Assert.Single(store.Document.AgentExternalProvisioningOperations);
        Assert.Equal("provisioning-001", operation.IdempotencyKey);
        Assert.Equal(first, operation.Receipt);
    }

    [Fact]
    public async Task UpsertAsync_StaleExpectedVersion_FailsWithoutCatalogMutation()
    {
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var sut = new AgentExternalProvisioningService(store, new ProviderProfileService());
        var created = await sut.UpsertAsync(
            new AgentExternalProvisioningCommand(
                "partner-system",
                "review-agent",
                "provisioning-002",
                ExpectedConfigurationVersion: null,
                CreateEditor()));
        var documentBeforeUpdate = store.Document;
        var changedEditor = CreateEditor();
        changedEditor.Summary = "Changed summary that must not be persisted under a stale precondition.";

        var exception = await Assert.ThrowsAsync<AgentExternalProvisioningException>(
            () => sut.UpsertAsync(
                new AgentExternalProvisioningCommand(
                    "partner-system",
                    "review-agent",
                    "provisioning-003",
                    ExpectedConfigurationVersion: new string('0', 64),
                    changedEditor)));

        Assert.Equal(AgentExternalProvisioningFailureKind.PreconditionFailed, exception.Kind);
        Assert.Equal("agents.external-key-version-conflict", exception.Code);
        Assert.Same(documentBeforeUpdate, store.Document);
        var binding = Assert.Single(store.Document.AgentExternalBindings);
        Assert.Equal(created.ConfigurationVersion, binding.ConfigurationVersion);
        var agent = Assert.Single(store.Document.Agents);
        Assert.NotEqual(changedEditor.Summary, agent.Summary);
        Assert.Single(store.Document.AgentExternalProvisioningOperations);
    }

    private static AgentEditorModel CreateEditor()
    {
        return new AgentEditorModel
        {
            Name = "Partner policy reviewer",
            RoleTitle = "Review specialist",
            Summary = "Reviews partner automation policy.",
            Instructions = "Review the supplied automation policy.",
            Status = AgentLifecycleStatus.Active,
            Model = "gpt-test",
            Workload = AgentWorkloadKind.Programming,
            ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            Temperature = 0.1,
            ConfigurationJson = "{}",
            TemplateKey = "partner-policy-reviewer",
            Permissions = AgentPermissionsPolicy.Default,
            SelectedCapabilityIds = [],
            Tags = ["alpha", "beta"]
        };
    }

    private sealed class InMemoryWorkspaceStore(SandboxWorkspaceDocument initialDocument)
        : ISandboxWorkspaceStore
    {
        public SandboxWorkspaceDocument Document { get; private set; } = initialDocument;

        public Task<SandboxWorkspaceDocument> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Document);

        public Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SandboxWorkspaceDocumentSnapshot(Document, 0));

        public Task<SandboxWorkspaceDocument> SaveAsync(
            SandboxWorkspaceDocument document,
            CancellationToken cancellationToken = default)
        {
            Document = document;
            return Task.FromResult(Document);
        }

        public Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
            Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
            CancellationToken cancellationToken = default)
        {
            Document = update(Document);
            return Task.FromResult(Document);
        }

        public Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
            Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
            => UpdateWorkspaceAsync(update, cancellationToken);

        public Task<SandboxWorkspaceCatalog> LoadCatalogAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Document.ToCatalog());

        public async Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            var catalog = await LoadCatalogAsync(cancellationToken);
            return new SandboxWorkspaceCatalogSnapshot(
                catalog,
                catalog.CatalogDataRevision);
        }

        public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(
            SandboxWorkspaceCatalog catalog,
            CancellationToken cancellationToken = default)
        {
            Document = SandboxWorkspaceDocument.Combine(catalog, Document.ToExecutionState());
            return Task.FromResult(catalog);
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            CancellationToken cancellationToken = default)
        {
            var catalog = update(Document.ToCatalog());
            Document = SandboxWorkspaceDocument.Combine(catalog, Document.ToExecutionState());
            return Task.FromResult(catalog);
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
            => UpdateCatalogAsync(update, cancellationToken);

        public Task<SandboxWorkspaceExecutionState> LoadExecutionAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Document.ToExecutionState());

        public Task<SandboxWorkspaceExecutionSummary> LoadExecutionSummaryAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AgentUsageProjection> LoadUsageProjectionAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveExecutionAsync(
            SandboxWorkspaceExecutionState executionState,
            CancellationToken cancellationToken = default)
        {
            Document = SandboxWorkspaceDocument.Combine(Document.ToCatalog(), executionState);
            return Task.CompletedTask;
        }

        public Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
            Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
            CancellationToken cancellationToken = default)
        {
            var executionState = update(Document.ToExecutionState());
            Document = SandboxWorkspaceDocument.Combine(Document.ToCatalog(), executionState);
            return Task.FromResult(executionState);
        }

        public Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
            Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
            => UpdateExecutionAsync(update, cancellationToken);
    }
}
