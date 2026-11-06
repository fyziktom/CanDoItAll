using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Editing;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowLegacySnapshotMigrationTests {
    [Fact]
    public async Task Legacy_backfill_appends_a_new_head_and_preserves_exact_versions_and_persisted_run_references() {
        await using var database = PostgresTestDatabaseLease.Create("workflowlegacysnapshot");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var component = Component();
        var original = Definition(component.Id);
        var historical = original with { VersionId = WorkflowVersionId.New(), Name = "Historical exact version" };
        var run = new WorkflowRunRecordEntity {
            RunId = Guid.NewGuid(), WorkflowId = original.Id.Value, VersionId = original.VersionId.Value,
            State = WorkflowRunState.Running, Backend = WorkflowRuntimeBackendKind.InProcess,
            BackendRunId = "retained-run", Summary = "Retained runtime reference", OriginJson = "{}",
            CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };
        var oldHead = WorkflowDefinitionRecord.FromDefinition(original, 2);
        var oldHistory = WorkflowDefinitionRecord.FromDefinition(historical, 1);
        oldHead.InstructionSnapshotSchemaVersion = 0;
        oldHistory.InstructionSnapshotSchemaVersion = 0;
        await using (var arrange = factory.CreateDbContext()) {
            arrange.AddRange(oldHead, oldHistory, run, new WorkflowDefinitionHeadRecord {
                WorkflowId = original.Id.Value, VersionId = original.VersionId.Value,
                ExternalNamespace = original.ExternalNamespace, ExternalKey = original.ExternalKey
            });
            await arrange.SaveChangesAsync();
        }
        string runBytes;
        await using (var before = factory.CreateDbContext()) {
            runBytes = await RunBytesAsync(before);
        }
        var changes = WorkflowObservationTestSupport.Create();
        using var subscription = changes.Subscribe(original.Id);
        var migration = new WorkflowPromptGalleryMigrationService(new FixedComponentLibrary(component), WorkflowOwnerPersistenceTestFactory.FromCanonical(factory), changes,
            NullLogger<WorkflowPromptGalleryMigrationService>.Instance);

        await migration.EnsureMigratedAsync();
        using var hintDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var hint = Assert.Single((await subscription.ReadAsync(hintDeadline.Token)).Changes);
        Assert.Equal(original.Id.Value, hint.Resource.ResourceId);
        Assert.Equal(EditingChangeKind.EditableContentChanged, hint.Kind);

        Guid acceptedVersion;
        await using (var verify = factory.CreateDbContext()) {
            var versions = await verify.Set<WorkflowDefinitionRecord>().AsNoTracking().ToDictionaryAsync(row => row.VersionId);
            Assert.Equal(oldHead.DefinitionJson, versions[oldHead.VersionId].DefinitionJson);
            Assert.Equal(oldHistory.DefinitionJson, versions[oldHistory.VersionId].DefinitionJson);
            Assert.Equal(oldHead.UpdatedAtUtc, versions[oldHead.VersionId].UpdatedAtUtc);
            Assert.Equal(runBytes, await RunBytesAsync(verify));
            Assert.Equal(3, versions.Count);
            acceptedVersion = (await verify.Set<WorkflowDefinitionHeadRecord>().SingleAsync()).VersionId;
            Assert.NotEqual(original.VersionId.Value, acceptedVersion);
            var appended = versions[acceptedVersion];
            Assert.Equal(3, appended.Revision);
            Assert.All(versions.Values, row => Assert.Equal(3, row.InstructionSnapshotSchemaVersion));
            var definition = JsonSerializer.Deserialize<WorkflowDefinition>(appended.DefinitionJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var call = Assert.Single(definition.Graph.Nodes, node => node.Kind == WorkflowNodeKind.LlmCall);
            Assert.Equal(component.Instructions, call.Settings.Instructions);
            Assert.Equal(component.ProviderProfileId, call.Settings.ProviderProfileId);
            Assert.Equal(component.Model, call.Settings.Model);
            Assert.Equal(original.TemplateKey, definition.TemplateKey);
            Assert.Equal(original.ExternalKey, definition.ExternalKey);
            Assert.Equal(original.Graph.Nodes[1].Settings.ExecutorSettingsJson, call.Settings.ExecutorSettingsJson);
        }
        await migration.EnsureMigratedAsync();
        await using var reopened = factory.CreateDbContext();
        Assert.Equal(3, await reopened.Set<WorkflowDefinitionRecord>().CountAsync());
        Assert.Equal(acceptedVersion, (await reopened.Set<WorkflowDefinitionHeadRecord>().SingleAsync()).VersionId);
        Assert.Equal(runBytes, await RunBytesAsync(reopened));
    }

    [Fact]
    public async Task A_peer_head_change_refuses_the_whole_legacy_snapshot_batch_without_rewriting_old_payloads() {
        await using var database = PostgresTestDatabaseLease.Create("workflowlegacysnapshotrace");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var component = Component();
        var original = Definition(component.Id);
        var record = WorkflowDefinitionRecord.FromDefinition(original, 1);
        record.InstructionSnapshotSchemaVersion = 0;
        await using (var arrange = factory.CreateDbContext()) {
            arrange.AddRange(record, new WorkflowDefinitionHeadRecord {
                WorkflowId = original.Id.Value, VersionId = original.VersionId.Value,
                ExternalNamespace = original.ExternalNamespace, ExternalKey = original.ExternalKey
            });
            await arrange.SaveChangesAsync();
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var library = new FixedComponentLibrary(component) {
            BeforeRead = async () => {
                entered.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(20));
            }
        };
        var changes = WorkflowObservationTestSupport.Create();
        using var subscription = changes.Subscribe(original.Id);
        var migration = new WorkflowPromptGalleryMigrationService(library, WorkflowOwnerPersistenceTestFactory.FromCanonical(factory), changes,
            NullLogger<WorkflowPromptGalleryMigrationService>.Instance);
        var pending = migration.EnsureMigratedAsync();
        var peer = original with { VersionId = WorkflowVersionId.New(), Name = "Peer current head" };
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await using var writer = factory.CreateDbContext();
            var head = await writer.Set<WorkflowDefinitionHeadRecord>().SingleAsync();
            writer.Add(WorkflowDefinitionRecord.FromDefinition(peer, 2));
            head.VersionId = peer.VersionId.Value;
            await writer.SaveChangesAsync();
        } finally {
            release.TrySetResult();
        }

        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() => pending);

        await using var verify = factory.CreateDbContext();
        Assert.Equal(peer.VersionId.Value, (await verify.Set<WorkflowDefinitionHeadRecord>().SingleAsync()).VersionId);
        var stored = await verify.Set<WorkflowDefinitionRecord>().SingleAsync(row => row.VersionId == original.VersionId.Value);
        Assert.Equal(record.DefinitionJson, stored.DefinitionJson);
        Assert.Equal(0, stored.InstructionSnapshotSchemaVersion);
        Assert.Equal(2, await verify.Set<WorkflowDefinitionRecord>().CountAsync());
        using var noHint = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => subscription.ReadAsync(noHint.Token).AsTask());
        await migration.EnsureMigratedAsync();
        await using var reentered = factory.CreateDbContext();
        Assert.Equal(2, await reentered.Set<WorkflowDefinitionRecord>().CountAsync());
        Assert.Equal(peer.VersionId.Value, (await reentered.Set<WorkflowDefinitionHeadRecord>().SingleAsync()).VersionId);
        var retained = await reentered.Set<WorkflowDefinitionRecord>().SingleAsync(row => row.VersionId == original.VersionId.Value);
        Assert.Equal(record.DefinitionJson, retained.DefinitionJson);
        Assert.Equal(3, retained.InstructionSnapshotSchemaVersion);
    }

    private static Task<string> RunBytesAsync(DbContext context)
        => context.Database.SqlQueryRaw<string>("""
            SELECT to_jsonb(r)::text AS "Value" FROM "AgentFramework_WorkflowRuns" AS r
            """).SingleAsync();

    private static LlmCallComponent Component() => new(WorkflowComponentId.New(), "Current component", Guid.NewGuid(),
        "fixture-model", WorkflowModality.Text, new(.2, 800, false, string.Empty), "Current component instructions",
        WorkflowValueShape.Text, WorkflowValueShape.Text, AgentPermissionsPolicy.Default, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static WorkflowDefinition Definition(WorkflowComponentId componentId) {
        var start = new WorkflowNodeId("start");
        var call = new WorkflowNodeId("call");
        var end = new WorkflowNodeId("end");
        return new(WorkflowId.New(), WorkflowVersionId.New(), "Legacy head", "Retained description", WorkflowLifecycleStatus.Draft,
            new(start, [Node(start, WorkflowNodeKind.Start), Node(call, WorkflowNodeKind.LlmCall, componentId), Node(end, WorkflowNodeKind.End)],
                [new(new("start-call"), start, null, call, null, WorkflowEdgeKind.Direct, string.Empty),
                 new(new("call-end"), call, null, end, null, WorkflowEdgeKind.Direct, string.Empty)]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch) {
            TemplateKey = "retained-template", TemplatePackKey = "retained-pack", TemplatePackVersion = "1.0", SourceHash = "retained-source",
            ExternalNamespace = "fixture", ExternalKey = "retained-route"
        };
    }

    private static WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind, WorkflowComponentId? componentId = null)
        => new(id, kind, id.Value, [], new(componentId, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text) {
            ExecutorSettingsJson = "{\"nullable\":null,\"retained\":\"漢字\"}"
        });

    private sealed class FixedComponentLibrary(LlmCallComponent component) : IWorkflowComponentLibraryService {
        public Func<Task>? BeforeRead { get; init; }
        public async Task<LlmCallComponent?> GetComponentAsync(WorkflowComponentId id, CancellationToken cancellationToken = default) {
            Assert.Equal(component.Id, id);
            if (BeforeRead is { } beforeRead) {
                await beforeRead();
            }
            return component;
        }
        public Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The migration must not read a provider catalog.");
        public Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The migration must read only its referenced components.");
        public Task<LlmCallComponent> SaveComponentAsync(LlmCallComponentSaveRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("This fixture contains no outdated component binding.");
        public Task DeleteComponentAsync(WorkflowComponentDeleteRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Schema migration must not delete components.");
    }
}
