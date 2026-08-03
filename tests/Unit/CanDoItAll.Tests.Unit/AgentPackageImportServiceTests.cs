using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentPackageImportServiceTests
{
    private static readonly DateTimeOffset FixedVersion =
        new(2026, 7, 25, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ImportAsync_IdenticalIdempotencyReplay_ReturnsOriginalReceiptWithoutDuplicateAgent()
    {
        var importedAgent = CreateAgent();
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var packageService = new StubAgentPackageService(CreateImportResult(importedAgent));
        var sut = CreateService(store, packageService);
        var command = new AgentPackageImportCommand(
            AgentPackageImportMode.Create,
            "partner-import-001",
            "partner-agents-reviewer");

        var first = await sut.ImportAsync(Stream.Null, command);
        var replayed = await sut.ImportAsync(Stream.Null, command);

        Assert.False(first.Replayed);
        Assert.Equal(first with { Replayed = true }, replayed);
        Assert.Equal(2, packageService.ImportCallCount);
        var persistedAgent = Assert.Single(store.Document.Agents);
        Assert.Equal(importedAgent.Id, persistedAgent.Id);
        Assert.Equal(importedAgent.Name, persistedAgent.Name);
        var persistedOperation = Assert.Single(store.Document.AgentPackageImportOperations);
        Assert.Equal(command.IdempotencyKey, persistedOperation.IdempotencyKey);
        Assert.Equal(first, persistedOperation.Receipt);
        var binding = Assert.Single(store.Document.AgentExternalBindings);
        Assert.Equal(AgentExternalIdentityNormalizer.PackageImportNamespace, binding.Namespace);
        Assert.Equal(command.ExternalKey, binding.Key);
        Assert.Equal(importedAgent.Id, binding.AgentId);
    }

    [Fact]
    public async Task ImportAsync_IdempotencyKeyReusedWithChangedFingerprint_RejectsWithoutCatalogMutation()
    {
        var importedAgent = CreateAgent();
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var packageService = new StubAgentPackageService(CreateImportResult(importedAgent));
        var sut = CreateService(store, packageService);
        var originalCommand = new AgentPackageImportCommand(
            AgentPackageImportMode.Create,
            "partner-import-002",
            "partner-agents-reviewer");
        await sut.ImportAsync(Stream.Null, originalCommand);
        var documentBeforeConflict = store.Document;

        var exception = await Assert.ThrowsAsync<AgentPackageImportException>(
            () => sut.ImportAsync(
                Stream.Null,
                originalCommand with { ExternalKey = "partner-agents-changed-reviewer" }));

        Assert.Equal(AgentPackageImportFailureKind.Conflict, exception.Kind);
        Assert.Equal("agent-package.idempotency-conflict", exception.Code);
        Assert.Same(documentBeforeConflict, store.Document);
        Assert.Single(store.Document.Agents);
        Assert.Single(store.Document.AgentPackageImportOperations);
    }

    [Fact]
    public async Task ImportAsync_ReplaceWithStaleExpectedVersion_RejectsWithoutCatalogMutation()
    {
        var existingAgent = CreateAgent();
        var initialDocument = SandboxWorkspaceDocument.Empty with
        {
            Agents = [existingAgent]
        };
        var store = new InMemoryWorkspaceStore(initialDocument);
        var packageService = new StubAgentPackageService(
            CreateImportResult(existingAgent with { UpdatedAtUtc = FixedVersion.AddHours(1) }));
        var sut = CreateService(store, packageService);
        var command = new AgentPackageImportCommand(
            AgentPackageImportMode.ReplaceExactVersion,
            "partner-replace-001",
            "partner-agents-reviewer",
            ExpectedAgentVersion: FixedVersion.AddMinutes(-1));

        var exception = await Assert.ThrowsAsync<AgentPackageImportException>(
            () => sut.ImportAsync(Stream.Null, command));

        Assert.Equal(AgentPackageImportFailureKind.PreconditionFailed, exception.Kind);
        Assert.Equal("agent-package.version-conflict", exception.Code);
        Assert.Same(initialDocument, store.Document);
        var unchangedAgent = Assert.Single(store.Document.Agents);
        Assert.Equal(FixedVersion, unchangedAgent.UpdatedAtUtc);
        Assert.Empty(store.Document.AgentPackageImportOperations);
    }

    private static AgentPackageImportService CreateService(
        ISandboxWorkspaceStore store,
        IAgentPackageService packageService)
    {
        return new AgentPackageImportService(
            store,
            packageService,
            new ProviderProfileService());
    }

    private static AgentImportResult CreateImportResult(AgentDefinition agent)
    {
        return new AgentImportResult(
            agent,
            Sessions: [],
            ExecutionLog: [],
            Metrics: [],
            Memory: [],
            Providers: [],
            Capabilities: [])
        {
            PackageSha256 = new string('A', 64),
            PackageSchemaVersion = "1.0"
        };
    }

    private static AgentDefinition CreateAgent()
    {
        return new AgentDefinition(
            Guid.Parse("53c996ea-8023-4190-a0f5-b19fe775546f"),
            "Imported policy reviewer",
            "Review specialist",
            "Reviews partner automation policy.",
            "Review the supplied automation policy.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-test",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.1,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: """{"responseMode":"concise"}""",
            IsTemplate: false,
            TemplateKey: "imported-policy-reviewer",
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["package-import"],
            CreatedAtUtc: FixedVersion,
            UpdatedAtUtc: FixedVersion);
    }

    private sealed class StubAgentPackageService(AgentImportResult result) : IAgentPackageService
    {
        public int ImportCallCount { get; private set; }

        public Task<AgentExportResult> ExportAsync(
            SandboxWorkspaceDocument document,
            AgentDefinition agent,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgentImportResult> ImportAsync(
            string packagePath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgentImportResult> ImportAsync(
            Stream package,
            AgentPackageReadOptions options,
            CancellationToken cancellationToken = default)
        {
            ImportCallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class InMemoryWorkspaceStore(SandboxWorkspaceDocument initialDocument)
        : ISandboxWorkspaceStore
    {
        public SandboxWorkspaceDocument Document { get; private set; } = initialDocument;

        public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
            AgentExecutionReportQuery query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AgentWorkspaceDeletionResult> DeleteAgentWorkspaceDataAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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
