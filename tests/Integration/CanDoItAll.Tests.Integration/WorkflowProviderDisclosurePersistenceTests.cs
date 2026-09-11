using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed partial class WorkflowProviderDisclosurePersistenceTests {
    private static readonly JsonSerializerOptions JsonOptions = WorkflowProviderDisclosureContent.JsonOptions;
    private static readonly DateTimeOffset Timestamp = new DateTimeOffset(2026, 9, 11, 10, 11, 12, TimeSpan.Zero).AddTicks(9);
    private static readonly WorkflowNodeId ReadNode = new("read-projects");

    [Fact]
    public async Task Restart_retains_large_ordered_evidence_and_simulations_while_all_public_readers_exclude_private_rows() {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-disclosure-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var run = Run();
        var declaration = Declaration(run) with {
            Simulations = Enumerable.Range(0, 130).Select(index => Simulation(
                new($"simulated-node-{index:D4}"), Json(new { preview = index }))).ToImmutableArray()
        };
        var completion = Completion(run, declaration, 5, 50_000);
        await using (var application = await TestApplication.CreateAsync(harness)) {
            var store = Store(application);
            await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
            await store.SaveEventAsync(completion);
            await store.SaveEventAsync(completion with {
                Id = Guid.NewGuid(), Message = "Second observer redacted display", PayloadJson = "{}",
                CompletionProof = Clone(completion.CompletionProof!),
                ProviderReadEvidence = completion.ProviderReadEvidence.Select(Clone).ToArray()
            });
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        var restoredStore = Store(restarted);
        var retained = await restoredStore.ReadProviderDisclosureAsync(run.RunId);
        Assert.Equal(Json(declaration), Json(retained.Declaration));
        var completed = Assert.Single(retained.Completions);
        Assert.Equal(Json(completion.CompletionProof), Json(completed.Proof));
        Assert.Equal(completion.ProviderReadEvidence.Select(Json), completed.Evidence.Select(Json));
        Assert.Equal(WorkflowProviderDisclosureContent.Manifest(completion.ProviderReadEvidence), completed.Manifest);
        Assert.Empty(retained.UnprovenCompletedNodeIds);
        var visible = await restoredStore.ListEventsAsync(run.RunId);
        Assert.Equal(3, visible.Count);
        Assert.All(visible, value => {
            Assert.NotEqual(WorkflowEventKind.ProviderReadEvidence, value.Kind);
            Assert.Null(value.DisclosureDeclaration);
            Assert.Null(value.CompletionProof);
            Assert.Empty(value.ProviderReadEvidence);
        });
        var page = await restoredStore.ListEventPageAsync(new(run.RunId, PageIndex: 1, PageSize: 2));
        Assert.Equal(3, page.TotalCount);
        Assert.Single(page.Items);
        var source = new WorkflowRuntimeEvidenceSourceProvider(Factory(restarted));
        var memory = await source.ReadSnapshotAsync(new(run.RunId.Value, Take: 100));
        Assert.Equal(3, memory.Items.Count(item => item.EntityKind == MemorySourceEntityKind.WorkflowEvent));
        Assert.DoesNotContain("private-owner-value", Json(memory), StringComparison.Ordinal);
        await using var rows = await Factory(restarted).CreateDbContextAsync();
        Assert.True(await rows.Set<WorkflowEventRecordEntity>().CountAsync(row => row.RunId == run.RunId.Value) > 130);
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        var isolated = await Store(other).ReadProviderDisclosureAsync(run.RunId);
        Assert.Null(isolated.Declaration);
        Assert.Empty(isolated.Completions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Flush_failure_rolls_back_visible_event_and_all_private_parts_before_retry(bool failStarted) {
        await using var application = await TestApplication.CreateAsync();
        var run = Run();
        var declaration = Declaration(run);
        var started = Started(run, declaration);
        var completion = Completion(run, declaration, 3);
        var normal = Store(application);
        if (!failStarted) {
            await normal.CreateRunWithStartedEventAsync(run, started);
        }
        await using var database = await Factory(application).CreateDbContextAsync();
        var fault = new FailAfterFlush();
        var options = new DbContextOptionsBuilder<WorkflowDbContext>().UseNpgsql(database.Database.GetConnectionString())
            .AddInterceptors(fault).Options;
        var failing = new PersistentWorkflowRunStore(new OwnerFactory(options));
        var failure = await Assert.ThrowsAsync<IOException>(() => failStarted
            ? failing.CreateRunWithStartedEventAsync(run, started)
            : failing.SaveEventAsync(completion));
        Assert.Same(fault.Failure, failure);
        await using var verify = await Factory(application).CreateDbContextAsync();
        var persisted = await verify.Set<WorkflowEventRecordEntity>().AsNoTracking().Where(row => row.RunId == run.RunId.Value).ToArrayAsync();
        Assert.Equal(failStarted ? 0 : 3, persisted.Length);
        Assert.Equal(!failStarted, await verify.Set<WorkflowRunRecordEntity>().AnyAsync(row => row.RunId == run.RunId.Value));
        if (failStarted) {
            await normal.CreateRunWithStartedEventAsync(run, started);
        }
        await normal.SaveEventAsync(completion);
        Assert.Single((await normal.ReadProviderDisclosureAsync(run.RunId)).Completions);
    }

    [Fact]
    public async Task Duplicate_observers_require_full_equal_proof_and_stale_updates_cannot_remove_or_replace_it() {
        await using var application = await TestApplication.CreateAsync();
        var store = Store(application);
        var run = Run();
        var declaration = Declaration(run);
        var completion = Completion(run, declaration, 2);
        await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
        await store.SaveEventAsync(completion);
        var before = await Rows(application, run.RunId);
        await store.SaveEventAsync(completion with { CompletionProof = Clone(completion.CompletionProof!) });
        Assert.Equal(before, await Rows(application, run.RunId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(WorkflowProviderDisclosureJournal.PublicEvent(completion)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(completion with { Message = "Changed later display" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(completion with { Id = Guid.NewGuid(), ProviderReadEvidence = [] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(completion with {
            Id = Guid.NewGuid(), CompletionProof = completion.CompletionProof! with { ResultHash = Hash("different result") }
        }));
        await using var database = await Factory(application).CreateDbContextAsync();
        var privateRow = await database.Set<WorkflowEventRecordEntity>().AsNoTracking()
            .FirstAsync(row => row.RunId == run.RunId.Value && row.Kind == WorkflowEventKind.ProviderReadEvidence);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(completion with { Id = privateRow.Id }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(completion with {
            Id = Guid.NewGuid(), Kind = WorkflowEventKind.ProviderReadEvidence
        }));
        Assert.Equal(before, await Rows(application, run.RunId));
    }

    [Fact]
    public async Task Raw_SDK_completion_is_diagnostic_but_unproven_owner_progress_and_legacy_runs_remain_distinct() {
        await using var application = await TestApplication.CreateAsync();
        var store = Store(application);
        var run = Run();
        var declaration = Declaration(run);
        await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
        var raw = new WorkflowEventRecord(Guid.NewGuid(), run.RunId, WorkflowEventKind.ExecutorCompleted, new("sdk-diagnostic"),
            "SDK completed", WorkflowEventPayloads.Serialize(WorkflowEventPayloadSource.MafNative, "ExecutorCompleted"), Timestamp.AddSeconds(1));
        await store.SaveEventAsync(raw);
        var fake = Completion(run, declaration) with { CompletionProof = null, ProviderReadEvidence = [] };
        fake = fake with { PayloadJson = WorkflowEventPayloads.Serialize(WorkflowEventPayloadSource.CanDoItAllProgress,
            "ExecutorCompleted", ReadNode, inlineJson: Json(new { completionProof = Completion(run, declaration).CompletionProof })) };
        await store.SaveEventAsync(fake);
        var history = await store.ReadProviderDisclosureAsync(run.RunId);
        Assert.Empty(history.Completions);
        Assert.Equal(new[] { ReadNode }, history.UnprovenCompletedNodeIds);
        var legacy = Run();
        var legacyStart = Started(legacy, null);
        await store.CreateRunWithStartedEventAsync(legacy, legacyStart);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(legacyStart with { DisclosureDeclaration = Declaration(legacy) }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(Completion(legacy, Declaration(legacy))));
        Assert.Null((await store.ReadProviderDisclosureAsync(legacy.RunId)).Declaration);
        Assert.Single(await store.ListEventsAsync(legacy.RunId));
        var missingStart = Run();
        await store.SaveRunAsync(missingStart);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(Started(missingStart, Declaration(missingStart))));
        Assert.Empty(await store.ListEventsAsync(missingStart.RunId));
    }

    [Fact]
    public async Task Simulated_completion_requires_exact_saved_simulation_and_cannot_replace_it_with_real_owner_evidence() {
        await using var application = await TestApplication.CreateAsync();
        var store = Store(application);
        var run = Run();
        var simulation = Simulation(ReadNode, "{\"preview\":\"exact original step\"}");
        var declaration = Declaration(run) with { Simulations = [simulation] };
        await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
        var real = Completion(run, declaration, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(real));
        var simulated = real with { CompletionProof = real.CompletionProof! with { SimulationHash = simulation.Hash }, ProviderReadEvidence = [] };
        await store.SaveEventAsync(simulated);
        var restarted = new PersistentWorkflowRunStore(Factory(application));
        var retained = Assert.Single((await restarted.ReadProviderDisclosureAsync(run.RunId)).Completions);
        Assert.Equal(simulation.Hash, retained.Proof.SimulationHash);
        Assert.Empty(retained.Evidence);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.SaveEventAsync(simulated with {
            Id = Guid.NewGuid(), ProviderReadEvidence = real.ProviderReadEvidence
        }));
        var invalidRun = Run();
        var duplicate = Declaration(invalidRun) with { Simulations = [simulation, simulation] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateRunWithStartedEventAsync(invalidRun, Started(invalidRun, duplicate)));
        Assert.Null(await store.GetRunAsync(invalidRun.RunId));
    }

    [Fact]
    public async Task Concurrent_conflicting_completion_claims_commit_one_complete_winner_without_partial_loser_rows() {
        await using var application = await TestApplication.CreateAsync();
        var store = Store(application);
        var run = Run();
        var declaration = Declaration(run);
        await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
        var first = Completion(run, declaration, 2);
        var second = first with { Id = Guid.NewGuid(), CompletionProof = first.CompletionProof! with { ResultHash = Hash("other result") } };
        async Task<Exception?> Attempt(WorkflowEventRecord value) {
            try {
                await new PersistentWorkflowRunStore(Factory(application)).SaveEventAsync(value);
                return null;
            } catch (InvalidOperationException failure) {
                return failure;
            }
        }
        var outcomes = await Task.WhenAll(Attempt(first), Attempt(second));
        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
        var winner = outcomes[0] is null ? first : second;
        var history = await store.ReadProviderDisclosureAsync(run.RunId);
        Assert.Equal(Json(winner.CompletionProof), Json(Assert.Single(history.Completions).Proof));
        Assert.Equal(winner.ProviderReadEvidence.Select(Json), history.Completions[0].Evidence.Select(Json));
        var visible = await store.ListEventsAsync(run.RunId);
        Assert.Equal(2, visible.Count);
        Assert.Equal(winner.Id, Assert.Single(visible, value => value.Kind == WorkflowEventKind.ExecutorCompleted).Id);
    }

    private static IDbContextFactory<WorkflowDbContext> Factory(TestApplication application) =>
        application.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
    private static PersistentWorkflowRunStore Store(TestApplication application) => new(Factory(application));
    private static WorkflowExecutionContentHash Hash(string value) => WorkflowExecutionContentHash.Compute(value);
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(Json(value), JsonOptions)!;
    private static WorkflowRunSnapshot Run() => new(WorkflowRunId.New(), WorkflowId.New(), WorkflowVersionId.New(),
        WorkflowRunState.Running, WorkflowRuntimeBackendKind.InProcess, "disclosure-fixture", "Running", Timestamp, Timestamp);
    private static WorkflowRunDisclosureDeclaration Declaration(WorkflowRunSnapshot run) => new(run.RunId, run.WorkflowId,
        run.VersionId, Hash("exact admitted definition"), WorkflowProviderDisclosureContent.Source(run.Origin), WorkflowProviderDisclosureProtocol.Current);
    private static WorkflowNodeSimulationAdmission Simulation(WorkflowNodeId nodeId, string outputJson) {
        var step = new WorkflowPreviewSimulationStep(nodeId, new("fixture.simulation"), "Exact original preview", outputJson);
        return new(nodeId, WorkflowProviderDisclosureContent.Simulation(step)) { Step = step };
    }
    private static WorkflowEventRecord Started(WorkflowRunSnapshot run, WorkflowRunDisclosureDeclaration? declaration) =>
        new(Guid.NewGuid(), run.RunId, WorkflowEventKind.Started, null, "Started", "{}", Timestamp) { DisclosureDeclaration = declaration };
    private static WorkflowEventRecord Completion(WorkflowRunSnapshot run, WorkflowRunDisclosureDeclaration declaration, int parts = 1, int size = 10) {
        var occurrence = WorkflowExecutionOccurrence.Start(run.RunId).Advance(run.VersionId, ReadNode);
        var proof = new WorkflowNodeCompletionProof(Guid.NewGuid(), occurrence, run.WorkflowId, run.VersionId, ReadNode,
            new("fixture.read"), declaration.DefinitionHash, Hash("settings"), Hash("input"), Hash("result"),
            declaration.SourceHash, declaration.CompilerVersion);
        var evidence = Enumerable.Range(0, parts).Select(index => new WorkflowProviderReadEvidence(new("fixture.owner"), 1,
            occurrence, run.VersionId, ReadNode, Json(new { privateValue = "private-owner-value", index, content = new string('x', size) }))).ToArray();
        return new(Guid.NewGuid(), run.RunId, WorkflowEventKind.ExecutorCompleted, ReadNode, "Completed",
            WorkflowEventPayloads.Serialize(WorkflowEventPayloadSource.CanDoItAllProgress, "ExecutorCompleted", ReadNode), Timestamp.AddSeconds(1)) {
            CompletionProof = proof, ProviderReadEvidence = evidence
        };
    }

    private static async Task<string[]> Rows(TestApplication application, WorkflowRunId runId) {
        await using var database = await Factory(application).CreateDbContextAsync();
        var rows = await database.Set<WorkflowEventRecordEntity>().AsNoTracking().Where(row => row.RunId == runId.Value).OrderBy(row => row.Id).ToArrayAsync();
        return rows.Select(Json).ToArray();
    }

    private sealed class OwnerFactory(DbContextOptions<WorkflowDbContext> options) : IDbContextFactory<WorkflowDbContext> {
        public WorkflowDbContext CreateDbContext() => new(options);
        public Task<WorkflowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class FailAfterFlush : SaveChangesInterceptor {
        public IOException Failure { get; } = new("Injected owner flush failure");
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => throw Failure;
    }
}
