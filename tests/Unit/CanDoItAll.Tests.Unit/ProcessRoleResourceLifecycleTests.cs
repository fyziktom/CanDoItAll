using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRoleResourceLifecycleTests {
    private const string DefinitionKey = "dotnet-runtime-command-writeback";
    private const string RoleKey = "owned-role";
    private const string SurvivorKey = "surviving-role";

    [Fact]
    public async Task Delete_removes_only_owned_head_resources_and_placements_and_replays_the_original_receipt() {
        var content = Content();
        content.Definition.RoleUsages.Add(new() { Key = SurvivorKey, DisplayName = "Surviving role" });
        content = content with { References = [
            new("owned-placement", ProcessDefinitionCanvasNodeKind.Role, RoleKey, null, 10, 20),
            new("owned-clone", ProcessDefinitionCanvasNodeKind.Role, RoleKey, null, 30, 40, true),
            new("surviving-placement", ProcessDefinitionCanvasNodeKind.Role, SurvivorKey, null, 50, 60),
            new("unrelated-step", ProcessDefinitionCanvasNodeKind.Step, RoleKey, RoleKey, 70, 80)
        ] };
        var store = new Store(content);
        var historical = ProcessAuthoringCodec.Write(store.Head.Content);
        var adapter = store.Open();
        var before = await adapter.ReadAsync(ProcessWorkspaceShellScope.Global, new(DefinitionKey), default);
        ProcessDefinitionRoleEditorCommand command = new(ProcessWorkspaceShellScope.Global, new(DefinitionKey),
            ProcessDefinitionRoleCommandKind.DeleteRole, before.VersionToken,
            before.Roles.Single(role => role.RoleKey.Value == RoleKey).Draft, null);

        var deleted = await adapter.ExecuteAsync(command, default);

        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, deleted.Receipt.Status);
        Assert.Equal(2, store.Head.Revision);
        Assert.False(store.Head.Content.RoleResources.ContainsKey(RoleKey));
        Assert.Equal("Unrelated resource", store.Head.Content.RoleResources[SurvivorKey].Summary);
        Assert.Equal(new[] { "surviving-placement", "unrelated-step" }, store.Head.Content.References.Select(item => item.Key));
        Assert.Equal(SurvivorKey, Assert.Single((await store.Open().ReadAsync(ProcessWorkspaceShellScope.Global, new(DefinitionKey), default)).Roles).RoleKey.Value);
        Assert.Equal(historical, store.PublicationJson);
        var receiptJson = ProcessAuthoringCodec.WriteReceipt(Assert.Single(store.Receipts.Values).Receipt);
        var replay = await store.Open().ExecuteAsync(command, default);
        Assert.Equal(deleted.Receipt, replay.Receipt);
        Assert.Equal(2, store.Head.Revision);
        Assert.Equal(receiptJson, ProcessAuthoringCodec.WriteReceipt(Assert.Single(store.Receipts.Values).Receipt));
        var stale = await store.Open().ExecuteAsync(command with { OperationId = Guid.NewGuid() }, default);
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, stale.Receipt.Status);
        Assert.Equal(2, store.Head.Revision);
    }

    [Fact]
    public async Task Add_role_avoids_retained_resource_identity_and_does_not_inherit_its_defaults() {
        var store = new Store(Content());
        var initial = await store.Open().ReadAsync(ProcessWorkspaceShellScope.Global, new(DefinitionKey), default);
        var action = initial.TemplateActions.First();
        var occupied = action.KeyPrefix + "-1";
        var resources = store.Head.Content.RoleResources.ToDictionary();
        resources.Add(occupied, new() { Key = "older-source", DisplayName = "Obsolete role", Summary = "Must not leak", Purpose = "Old purpose" });
        store.Head = store.Head with { Content = store.Head.Content with { RoleResources = resources } };
        var adapter = store.Open();
        var before = await adapter.ReadAsync(ProcessWorkspaceShellScope.Global, new(DefinitionKey), default);
        ProcessDefinitionRoleEditorCommand command = new(ProcessWorkspaceShellScope.Global, new(DefinitionKey),
            ProcessDefinitionRoleCommandKind.AddRole, before.VersionToken, before.SelectedRole!.Draft, action.ActionKey);

        var added = await adapter.ExecuteAsync(command, default);

        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, added.Receipt.Status);
        Assert.NotEqual(occupied, added.Projection.SelectedRoleKey!.Value.Value);
        Assert.Equal(action.Summary, added.Projection.SelectedRole!.Draft.Purpose);
        Assert.Equal("Must not leak", store.Head.Content.RoleResources[occupied].Summary);
        Assert.DoesNotContain(store.Head.Content.RoleResources.Keys, key => key == added.Projection.SelectedRoleKey.Value.Value);
        Assert.Equal(added.Receipt, (await store.Open().ExecuteAsync(command, default)).Receipt);
        Assert.Equal(2, store.Head.Revision);
    }

    [Theory]
    [InlineData(ProcessTemplateImportCommandKind.ImportRole)]
    [InlineData(ProcessTemplateImportCommandKind.ImportProcess)]
    public void Import_remaps_an_occupied_resource_identity_with_its_local_references(ProcessTemplateImportCommandKind kind) {
        var target = Content();
        target.Definition.RoleUsages.Clear();
        var originalResource = target.RoleResources[RoleKey];
        var source = Content();
        source.Definition.Key = "import-source";
        source.RoleResources[RoleKey].Summary = "Imported resource";
        source.Definition.Steps = [new() { Key = "dispatch", DecisionRoleKey = RoleKey,
            RoleAssignments = [new() { RoleKey = RoleKey }],
            ArtifactExpectations = [new() { Key = "result" }],
            SubprocessContract = new() { DefinitionKey = "child", ParentProducedArtifactExpectationKey = "result",
                AcceptedChildOutputs = [new() { StepKey = "dispatch", ArtifactExpectationKey = "result" }] }
        }];
        source = source with { References = [new("source-role-clone", ProcessDefinitionCanvasNodeKind.Role, RoleKey, null, 11, 22, true)] };
        ProcessTemplateImportedComponentProjection imported = new(new("role:import-source:" + RoleKey),
            kind == ProcessTemplateImportCommandKind.ImportRole ? ProcessTemplateCatalogItemKind.Role : ProcessTemplateCatalogItemKind.Process,
            "Imported role", source.Definition.Key, RoleKey, "source-hash", null, DateTimeOffset.UnixEpoch);
        ProcessTemplateImportCommand command = new(ProcessWorkspaceShellScope.Global, new(DefinitionKey), kind, imported.ItemKey,
            null, new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 50), null);

        var merged = ProcessAuthoringImportPatch.Apply(target, source, command, imported);

        var role = Assert.Single(merged.Definition.RoleUsages);
        Assert.NotEqual(RoleKey, role.Key);
        Assert.Same(originalResource, merged.RoleResources[RoleKey]);
        Assert.Equal("Imported resource", merged.RoleResources[role.Key].Summary);
        Assert.Equal(role.Key, Assert.Single(merged.Imports).Remap["role:" + RoleKey]);
        var placement = Assert.Single(merged.References);
        Assert.Equal(role.Key, placement.SemanticKey);
        Assert.True(placement.IsClone);
        Assert.Equal((11d, 22d), (placement.X, placement.Y));
        if (kind == ProcessTemplateImportCommandKind.ImportProcess) {
            var step = Assert.Single(merged.Definition.Steps);
            Assert.Equal(role.Key, step.DecisionRoleKey);
            Assert.Equal(role.Key, Assert.Single(step.RoleAssignments).RoleKey);
            var childOutput = Assert.Single(step.SubprocessContract!.AcceptedChildOutputs);
            Assert.Equal("dispatch", childOutput.StepKey);
            Assert.Equal("result", childOutput.ArtifactExpectationKey);
        }
    }

    private static ProcessAuthoringContent Content() => new(ProcessAuthoringContent.CurrentSchemaVersion,
        new() { Key = DefinitionKey, DisplayName = "Role resource lifecycle", RoleUsages = [new() {
            Key = RoleKey, RoleResourceKey = "source-role", DisplayName = "Owned role", PreferredExecutorKind = "person" }] },
        new Dictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>>(),
        new Dictionary<string, ProcessTemplateRoleResourceDocument> {
            [RoleKey] = new() { Key = "source-role", DisplayName = "Owned role", Summary = "Owned resource", PreferredExecutorKind = "person" },
            [SurvivorKey] = new() { Key = "another-source", Summary = "Unrelated resource" }
        }, new("unit-fixture", "1", "fixture-content"), [], []);

    private sealed class Store : IProcessAuthoringStore, IProcessAuthoringContext, IProcessProjectionClock {
        public Store(ProcessAuthoringContent content) {
            Head = new(new(DatabaseProfileId, Guid.Empty, Guid.Empty, DefinitionKey), 1, ProcessAuthoringLifecycle.Published,
                ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(content)), Guid.NewGuid(), GetUtcNow());
            PublicationJson = ProcessAuthoringCodec.Write(Head.Content);
        }
        public Guid DatabaseProfileId { get; } = Guid.NewGuid();
        public string CallerId => "role-resource-fixture";
        public ProcessAuthoringSnapshot Head { get; set; }
        public string PublicationJson { get; }
        public Dictionary<Guid, (string Fingerprint, ProcessAuthoringReceipt Receipt)> Receipts { get; } = [];
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
        public ProcessRoleAuthoringAdapter Open() {
            var templates = new ProcessTemplatePackLoader();
            return new(new(this, this, templates), templates, this);
        }
        public Task<ProcessAuthoringAddress> CaptureReadAddressAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken)
            => Task.FromResult(Head.Address with { DefinitionKey = key.Value });
        public Task<ProcessAuthoringSnapshot?> ReadAsync(ProcessAuthoringAddress address, CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessAuthoringSnapshot?>(address == Head.Address ? Head with { Content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(Head.Content)) } : null);
        public Task<ProcessAuthoringReceipt?> RecoverAsync(ProcessAuthoringAddress address, string callerId, Guid operationId, string requestFingerprint, CancellationToken cancellationToken = default) {
            if (!Receipts.TryGetValue(operationId, out var saved)) {
                return Task.FromResult<ProcessAuthoringReceipt?>(null);
            }
            Assert.Equal(saved.Fingerprint, requestFingerprint);
            return Task.FromResult<ProcessAuthoringReceipt?>(ProcessAuthoringCodec.ReadReceipt(ProcessAuthoringCodec.WriteReceipt(saved.Receipt)));
        }
        public Task<ProcessAuthoringReceipt> CommitAsync(ProcessAuthoringCommit command, CancellationToken cancellationToken = default) {
            Assert.Equal(Head.Address, command.Address);
            Assert.Equal(Head.Revision, command.ExpectedRevision);
            Head = Head with { Revision = Head.Revision + 1, Content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(command.Content)), Lifecycle = command.Lifecycle };
            ProcessAuthoringReceipt receipt = new(command.OperationId, ProcessAuthoringOutcome.Accepted, Head) { Selection = command.Selection };
            Receipts.Add(command.OperationId, (command.RequestFingerprint, receipt));
            return Task.FromResult(receipt);
        }
        public Task RequireLaunchableAsync(ProcessExecutableDefinitionClosure definitions, bool underMutationGate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessAuthoringReceipt?> GetOperationAsync(ProcessAuthoringAddress address, string callerId, Guid operationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessAuthoringCatalogEntry>> ListAsync(Guid databaseProfileId, Guid projectId, Guid projectLifetimeId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessAuthoringPublication?> ReadPublicationAsync(Guid publicationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
