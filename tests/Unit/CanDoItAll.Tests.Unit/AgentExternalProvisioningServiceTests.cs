using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentExternalProvisioningServiceTests
{
    [Fact]
    public async Task UpsertAsync_EquivalentNormalizedRequest_ReplaysStableFingerprint()
    {
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var sut = CreateService(store);
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
        var sut = CreateService(store);
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

    [Fact]
    public async Task Prepared_upsert_and_archive_receipts_do_not_escape_refusal_or_uncertain_cleanup() {
        Exception[] failures = [
            new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged),
            new SandboxWorkspaceConcurrencyException(4, 5),
            new AggregateException(new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged),
                new IOException("Fixture key release failed."))
        ];
        foreach (var failure in failures) {
            var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
            var service = CreateService(store);
            var created = await service.UpsertAsync(new("partner-system", "publication-proof", "seed", null, CreateEditor()));
            var original = store.Document;
            store.PublicationFailure = failure;
            var editor = CreateEditor();
            editor.Summary = "Prepared but unpublished";
            await RequireRefusalAsync(() => service.UpsertAsync(new("partner-system", "publication-proof", "changed",
                created.ConfigurationVersion, editor)));
            Assert.False(Assert.Single(store.PreparedDocument!.AgentExternalProvisioningOperations,
                operation => operation.IdempotencyKey == "changed").Receipt.Archived);
            await RequireRefusalAsync(() => service.ArchiveAsync(new("partner-system", "publication-proof", "archived",
                created.ConfigurationVersion)));
            Assert.True(Assert.Single(store.PreparedDocument!.AgentExternalProvisioningOperations,
                operation => operation.IdempotencyKey == "archived").Receipt.Archived);
            Assert.Equal(3, store.Preparations);
            Assert.Same(original, store.Document);
            Assert.Single(store.Document.AgentExternalProvisioningOperations);
            Assert.Equal(AgentLifecycleStatus.Active, Assert.Single(store.Document.Agents).Status);

            async Task RequireRefusalAsync(Func<Task<AgentExternalProvisioningReceipt>> invoke) {
                AgentExternalProvisioningReceipt? accepted = null;
                var observed = await Record.ExceptionAsync(async () => accepted = await invoke());
                Assert.Null(accepted);
                if (failure is AggregateException) {
                    Assert.Same(failure, observed);
                } else {
                    var conflict = Assert.IsType<AgentExternalProvisioningException>(observed);
                    Assert.Equal(AgentExternalProvisioningFailureKind.Conflict, conflict.Kind);
                    Assert.Equal("agents.external-key-publication-conflict", conflict.Code);
                    Assert.Same(failure, conflict.InnerException);
                }
            }
        }
    }


    [Fact]
    public async Task Ordinary_change_and_ABA_invalidate_cached_binding_versions_but_preserve_historical_replay() {
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var service = CreateService(store);
        var command = new AgentExternalProvisioningCommand("partner-system", "review-agent", "original", null, CreateEditor());
        var created = await service.UpsertAsync(command);
        var original = Assert.Single(store.Document.Agents);
        await store.UpdateCatalogAsync(catalog => catalog with {
            Agents = [original with { Summary = "Other actor", UpdatedAtUtc = original.UpdatedAtUtc.AddTicks(1) }]
        });
        var changed = await service.GetAsync(command.Namespace, command.Key);
        Assert.NotEqual(created.ConfigurationVersion, changed.ConfigurationVersion);
        Assert.Equal(created.ConfigurationVersion, Assert.Single(store.Document.AgentExternalBindings).ConfigurationVersion);
        await RequireStaleAsync(created.ConfigurationVersion);
        await store.UpdateCatalogAsync(catalog => catalog with {
            Agents = [original with { UpdatedAtUtc = original.UpdatedAtUtc.AddTicks(2) }]
        });
        await RequireStaleAsync(created.ConfigurationVersion);
        await RequireStaleAsync(changed.ConfigurationVersion);
        var current = await service.GetAsync(command.Namespace, command.Key);
        var revision = Assert.Single(store.Document.Agents).UpdatedAtUtc;
        var noOp = await service.UpsertAsync(command with { IdempotencyKey = "explicit-current", ExpectedConfigurationVersion = current.ConfigurationVersion });
        Assert.Equal(current.ConfigurationVersion, noOp.ConfigurationVersion);
        Assert.Single(noOp.Warnings);
        Assert.Equal(revision, Assert.Single(store.Document.Agents).UpdatedAtUtc);
        Assert.Equal(created with { Replayed = true }, await service.UpsertAsync(command));

        async Task RequireStaleAsync(string version) {
            var before = store.Document;
            var update = await Assert.ThrowsAsync<AgentExternalProvisioningException>(() => service.UpsertAsync(
                command with { IdempotencyKey = "stale-update", ExpectedConfigurationVersion = version }));
            var archive = await Assert.ThrowsAsync<AgentExternalProvisioningException>(() => service.ArchiveAsync(
                new(command.Namespace, command.Key, "stale-archive", version)));
            Assert.Equal(AgentExternalProvisioningFailureKind.PreconditionFailed, update.Kind);
            Assert.Equal(AgentExternalProvisioningFailureKind.PreconditionFailed, archive.Kind);
            Assert.Same(before, store.Document);
        }
    }

    [Fact]
    public async Task Target_identity_and_workspace_are_part_of_the_precondition() {
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var workspace = Workspace();
        var service = CreateService(store, workspace);
        var created = await service.UpsertAsync(new("partner-system", "review-agent", "original", null, CreateEditor()));
        var changedId = Guid.NewGuid();
        await store.UpdateCatalogAsync(catalog => catalog with {
            Agents = [Assert.Single(catalog.Agents) with { Id = changedId }],
            AgentExternalBindings = [Assert.Single(catalog.AgentExternalBindings) with { AgentId = changedId }]
        });
        var current = await service.GetAsync("partner-system", "review-agent");
        Assert.Equal(changedId, current.AgentId);
        Assert.NotEqual(created.ConfigurationVersion, current.ConfigurationVersion);
        AgentExecutionActivityWorkspaceIdentity[] otherContexts = [
            new(Guid.NewGuid(), workspace.WorkspaceScope, workspace.DatabaseProfileGeneration),
            new(workspace.DatabaseProfileId, WorkspaceScopeDescriptor.Organization("other"), workspace.DatabaseProfileGeneration),
            new(workspace.DatabaseProfileId, workspace.WorkspaceScope, new(2))
        ];
        foreach (var other in otherContexts) {
            var owner = CreateService(store, other, new MutableGeneration { Value = other.DatabaseProfileGeneration.Value });
            var observed = await owner.GetAsync("partner-system", "review-agent");
            Assert.NotEqual(current.ConfigurationVersion, observed.ConfigurationVersion);
            var before = store.Document;
            var failure = await Assert.ThrowsAsync<AgentExternalProvisioningException>(() => owner.ArchiveAsync(
                new("partner-system", "review-agent", "wrong-context", current.ConfigurationVersion)));
            Assert.Equal(AgentExternalProvisioningFailureKind.PreconditionFailed, failure.Kind);
            Assert.Same(before, store.Document);
        }
        var targetFailure = await Assert.ThrowsAsync<AgentExternalProvisioningException>(() => service.ArchiveAsync(
            new("partner-system", "review-agent", "wrong-target", created.ConfigurationVersion)));
        Assert.Equal(AgentExternalProvisioningFailureKind.PreconditionFailed, targetFailure.Kind);
    }

    [Fact]
    public async Task Mutable_editor_and_nested_access_are_captured_before_storage_yields() {
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var service = CreateService(store);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.BeforeUpdateAsync = async () => {
            entered.TrySetResult();
            await resume.Task.WaitAsync(timeout.Token);
        };
        var editor = CreateEditor();
        editor.ProjectStructureAccess.CanRead = true;
        var command = new AgentExternalProvisioningCommand("partner-system", "review-agent", "captured", null, editor);
        var pending = service.UpsertAsync(command, timeout.Token);
        try {
            await entered.Task.WaitAsync(timeout.Token);
            editor.Name = "Late changed name";
            editor.Tags.Clear();
            editor.ProjectStructureAccess.CanWrite = true;
            editor.ProjectStructureAccess.AllowedProjectIds.Add(Guid.NewGuid());
        } finally {
            resume.TrySetResult();
        }
        var receipt = await pending;
        var persisted = Assert.Single(store.Document.Agents);
        Assert.Equal("Partner policy reviewer", persisted.Name);
        Assert.Equal(["alpha", "beta"], persisted.Tags);
        var access = AgentProjectStructureAccessMetadata.Read(persisted.ConfigurationJson);
        Assert.True(access.CanRead);
        Assert.False(access.CanWrite);
        Assert.Empty(access.AllowedProjectIds);
        var original = CreateEditor();
        original.ProjectStructureAccess.CanRead = true;
        Assert.Equal(receipt with { Replayed = true }, await service.UpsertAsync(command with { Agent = original }));
        var conflicting = await Assert.ThrowsAsync<AgentExternalProvisioningException>(() => service.UpsertAsync(command));
        Assert.Equal(AgentExternalProvisioningFailureKind.Conflict, conflicting.Kind);
        Assert.Single(store.Document.AgentExternalProvisioningOperations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_rotation_before_callback_or_final_publication_refuses_every_write(bool afterPreparation) {
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var generation = new MutableGeneration();
        var service = CreateService(store, generation: generation);
        var created = await service.UpsertAsync(new("partner-system", "review-agent", "seed", null, CreateEditor()));
        var before = store.Document;
        if (afterPreparation) {
            store.BeforePublication = () => generation.Value++;
        } else {
            store.BeforeUpdateAsync = () => {
                generation.Value++;
                return Task.CompletedTask;
            };
        }
        foreach (var archive in new[] { false, true }) {
            generation.Value = 1;
            var failure = await Assert.ThrowsAsync<AgentExternalProvisioningException>(() => archive
                ? service.ArchiveAsync(new("partner-system", "review-agent", "archive", created.ConfigurationVersion))
                : service.UpsertAsync(new("partner-system", "review-agent", "update", created.ConfigurationVersion, CreateEditor())));
            Assert.Equal("agents.external-key-context-changed", failure.Code);
            Assert.Same(before, store.Document);
        }
    }

    [Fact]
    public async Task Read_rejects_a_profile_rotation_and_reports_current_lifecycle_instead_of_binding_metadata() {
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var generation = new MutableGeneration();
        var service = CreateService(store, generation: generation);
        await service.UpsertAsync(new("partner-system", "review-agent", "seed", null, CreateEditor()));
        var original = Assert.Single(store.Document.Agents);
        await store.UpdateCatalogAsync(catalog => catalog with {
            Agents = [original with { Status = AgentLifecycleStatus.Archived, UpdatedAtUtc = original.UpdatedAtUtc.AddTicks(1) }]
        });
        var resource = await service.GetAsync("partner-system", "review-agent");
        Assert.True(resource.IsArchived);
        Assert.Equal(original.UpdatedAtUtc.AddTicks(1), resource.UpdatedAtUtc);
        Assert.False(Assert.Single(store.Document.AgentExternalBindings).IsArchived);
        store.BeforeReadCompletion = () => generation.Value++;
        var failure = await Assert.ThrowsAsync<AgentExternalProvisioningException>(() => service.GetAsync("partner-system", "review-agent"));
        Assert.Equal("agents.external-key-context-changed", failure.Code);
    }

    [Fact]
    public async Task Legacy_fingerprint_and_receipt_remain_exact_and_are_not_fresh_write_authority() {
        const string legacyFingerprint = "C62A2372A1CECA3AA45235745A8CC705C415E8AB6010AAB8549B5FCB4814A845";
        var store = new InMemoryWorkspaceStore(SandboxWorkspaceDocument.Empty);
        var service = CreateService(store);
        var command = new AgentExternalProvisioningCommand("partner-system", "review-agent", "legacy", null, CreateEditor());
        var created = await service.UpsertAsync(command);
        Assert.Equal(legacyFingerprint, Assert.Single(store.Document.AgentExternalProvisioningOperations).RequestFingerprint);
        var legacy = created with { ConfigurationVersion = AgentConfigurationVersion.Create(Assert.Single(store.Document.Agents)) };
        await store.UpdateCatalogAsync(catalog => catalog with {
            AgentExternalProvisioningOperations = [Assert.Single(catalog.AgentExternalProvisioningOperations) with { Receipt = legacy }]
        });
        Assert.Equal(legacy with { Replayed = true }, await service.UpsertAsync(command));
        var current = await service.GetAsync(command.Namespace, command.Key);
        Assert.NotEqual(legacy.ConfigurationVersion, current.ConfigurationVersion);
        var failure = await Assert.ThrowsAsync<AgentExternalProvisioningException>(() => service.ArchiveAsync(
            new(command.Namespace, command.Key, "new-intent", legacy.ConfigurationVersion)));
        Assert.Equal(AgentExternalProvisioningFailureKind.PreconditionFailed, failure.Kind);
    }

    private static AgentExecutionActivityWorkspaceIdentity Workspace()
        => new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1));

    private static AgentExternalProvisioningService CreateService(InMemoryWorkspaceStore store,
        AgentExecutionActivityWorkspaceIdentity? workspace = null, MutableGeneration? generation = null)
        => new(store, new ProviderProfileService(), workspace ?? Workspace(), generation ?? new());

    private sealed class MutableGeneration : IAgentExecutionProfileGenerationSource {
        public long Value { get; set; } = 1;
        public DatabaseProfileGeneration GetGeneration() => new(Value);
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

    private sealed class InMemoryWorkspaceStore(SandboxWorkspaceDocument initialDocument) : ISandboxWorkspaceCatalogStore {
        public SandboxWorkspaceCatalog Document { get; private set; } = initialDocument.ToCatalog();
        public SandboxWorkspaceCatalog? PreparedDocument { get; private set; }
        public Exception? PublicationFailure { get; set; }
        public Func<Task>? BeforeUpdateAsync { get; set; }
        public Action? BeforePublication { get; set; }
        public Action? BeforeReadCompletion { get; set; }
        public int Preparations { get; private set; }

        public Task<SandboxWorkspaceCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) {
            var catalog = Document;
            BeforeReadCompletion?.Invoke();
            return Task.FromResult(catalog);
        }

        public Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(SandboxWorkspaceCatalog catalog,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            CancellationToken cancellationToken = default) => UpdateCatalogAsync(update, _ => { }, cancellationToken);

        public async Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            Action<SandboxWorkspaceCatalog> validatePublication, CancellationToken cancellationToken = default) {
            if (BeforeUpdateAsync is { } beforeUpdate) {
                await beforeUpdate();
            }
            Preparations++;
            PreparedDocument = update(Document);
            BeforePublication?.Invoke();
            validatePublication(PreparedDocument);
            if (PublicationFailure is { } failure) {
                throw failure;
            }
            Document = PreparedDocument;
            return Document;
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            long expectedRevision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
