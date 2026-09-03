using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class FileSandboxWorkspaceUsageProjectionIntegrationTests
{
    [Fact]
    public async Task Usage_projection_delta_matches_canonical_rebuild_after_append()
    {
        await using var scenario = UsageProjectionScenario.Create();
        var runId = Guid.NewGuid();
        var agentId = await scenario.GetAgentIdAsync();
        var createdAtUtc = DateTimeOffset.UtcNow;
        var firstObservation = CreateUsageObservation(
            runId,
            agentId,
            createdAtUtc,
            "OpenAI default",
            ProviderKind.OpenAi,
            "gpt-5.4-mini",
            inputTokens: 120,
            outputTokens: 30);
        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                runId,
                agentId,
                createdAtUtc,
                "OpenAI default",
                "gpt-5.4-mini",
                [firstObservation]));
        var secondObservation = CreateUsageObservation(
            runId,
            agentId,
            createdAtUtc.AddSeconds(1),
            "OpenAI default",
            ProviderKind.OpenAi,
            "gpt-5.4-mini",
            inputTokens: 80,
            outputTokens: 20);

        await ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
            .UpdateExecutionRunDetailAsync(
                runId,
                detail => detail with
                {
                    Run = detail.Run with
                    {
                        Revision = detail.Run.Revision + 1,
                        UpdatedAtUtc = createdAtUtc.AddSeconds(2)
                    },
                    UsageObservations =
                    [
                        .. detail.UsageObservations,
                        secondObservation
                    ]
                });
        var incremental = await scenario.Store.LoadUsageProjectionAsync();

        File.Delete(scenario.UsageIndexPath);
        var rebuilt = await new FileSandboxWorkspaceStore(
                scenario.WorkspaceRoot,
                scenario.Scope)
            .LoadUsageProjectionAsync();

        Assert.Equal(incremental.Version, rebuilt.Version);
        Assert.Equal(incremental.Revision, rebuilt.Revision);
        Assert.Equal(incremental.UpdatedAtUtc, rebuilt.UpdatedAtUtc);
        Assert.Equal(incremental.Agents, rebuilt.Agents);
        Assert.Equal(incremental.Providers, rebuilt.Providers);
        Assert.Equal(incremental.Models, rebuilt.Models);
    }

    [Fact]
    public async Task Usage_projection_keeps_delimiter_bearing_model_keys_distinct()
    {
        await using var scenario = UsageProjectionScenario.Create();
        var agentId = await scenario.GetAgentIdAsync();
        var createdAtUtc = DateTimeOffset.UtcNow;
        var firstRunId = Guid.NewGuid();
        var secondRunId = Guid.NewGuid();

        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                firstRunId,
                agentId,
                createdAtUtc,
                "alpha:beta",
                "gamma",
                [
                    CreateUsageObservation(
                        firstRunId,
                        agentId,
                        createdAtUtc,
                        "alpha:beta",
                        ProviderKind.OpenAi,
                        "gamma",
                        inputTokens: 10,
                        outputTokens: 5)
                ]));
        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                secondRunId,
                agentId,
                createdAtUtc.AddSeconds(1),
                "alpha",
                "beta:gamma",
                [
                    CreateUsageObservation(
                        secondRunId,
                        agentId,
                        createdAtUtc.AddSeconds(1),
                        "alpha",
                        ProviderKind.OpenAi,
                        "beta:gamma",
                        inputTokens: 20,
                        outputTokens: 5)
                ]));

        var projection = await scenario.Store.LoadUsageProjectionAsync();

        Assert.Equal(2, projection.Providers.Count);
        Assert.Equal(2, projection.Models.Count);
        Assert.Contains(
            projection.Models,
            row => row.ProviderName == "alpha:beta" &&
                   row.Model == "gamma" &&
                   row.UsageObservationCount == 1);
        Assert.Contains(
            projection.Models,
            row => row.ProviderName == "alpha" &&
                   row.Model == "beta:gamma" &&
                   row.UsageObservationCount == 1);
    }

    [Fact]
    public async Task Existing_run_update_rejects_usage_observation_removal()
    {
        await using var scenario = UsageProjectionScenario.Create();
        var runId = Guid.NewGuid();
        var agentId = await scenario.GetAgentIdAsync();
        var createdAtUtc = DateTimeOffset.UtcNow;
        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                runId,
                agentId,
                createdAtUtc,
                "OpenAI default",
                "gpt-5.4-mini",
                [
                    CreateUsageObservation(
                        runId,
                        agentId,
                        createdAtUtc,
                        "OpenAI default",
                        ProviderKind.OpenAi,
                        "gpt-5.4-mini",
                        inputTokens: 10,
                        outputTokens: 5)
                ]));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    runId,
                    detail => detail with
                    {
                        Run = NextRevision(detail.Run),
                        UsageObservations = []
                    }));

        Assert.Contains("cannot remove provider usage observation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_run_update_rejects_usage_observation_identity_change()
    {
        await using var scenario = UsageProjectionScenario.Create();
        var runId = Guid.NewGuid();
        var agentId = await scenario.GetAgentIdAsync();
        var createdAtUtc = DateTimeOffset.UtcNow;
        await scenario.Store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                runId,
                agentId,
                createdAtUtc,
                "OpenAI default",
                "gpt-5.4-mini",
                [
                    CreateUsageObservation(
                        runId,
                        agentId,
                        createdAtUtc,
                        "OpenAI default",
                        ProviderKind.OpenAi,
                        "gpt-5.4-mini",
                        inputTokens: 10,
                        outputTokens: 5)
                ]));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    runId,
                    detail => detail with
                    {
                        Run = NextRevision(detail.Run),
                        UsageObservations = detail.UsageObservations
                            .Select(observation => observation with
                            {
                                Model = "gpt-5.4-mini:changed"
                            })
                            .ToArray()
                    }));

        Assert.Contains("cannot change the identity", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((int)FileHistoryCommitStage.Prepared)]
    [InlineData((int)FileHistoryCommitStage.SourceCommitted)]
    [InlineData((int)FileHistoryCommitStage.Published)]
    public async Task Actual_file_owner_recovers_first_commit_after_expiry_and_deletion(int failureStageValue) {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var scenario = UsageProjectionScenario.Create();
        var agentId = await scenario.GetAgentIdAsync();
        var runId = Guid.NewGuid();
        var start = history.Start();
        start = start with {
            Workload = HistoryWorkload.Agent,
            ContentOwner = new(history.Partition, HistorySourceKind.AgentConversation,
                new(runId.ToString("N")), new(start.RequestId.Value.ToString("N")))
        };
        var completion = history.Completion();
        await history.Capture.BeginAsync(start, null, default);
        await history.Capture.CompleteAsync(start, completion, null, default);
        history.Clock.Now += TimeSpan.FromDays(40);
        Assert.Equal(1, await new HistoryRetentionStore(history.Factory, history.Clock)
            .PurgeExpiredMetadataAsync(history.Partition, 10, default));
        var observation = CreateUsageObservation(runId, agentId, history.Clock.Now,
            "Fixture", ProviderKind.OpenAi, "exact-model", 700, 300) with {
            HistoryEvidence = new(start.RequestId, true, [HistoryAttemptEvidence.Create(start, completion)]),
            RawUsageJson = "{\"private\":\"raw-private-do-not-copy\"}",
            DiagnosticsJson = "{\"private\":\"diagnostics-private-do-not-copy\"}"
        };
        var failureStage = (FileHistoryCommitStage)failureStageValue;
        var injected = false;
        var failing = new FileSandboxWorkspaceStore(scenario.WorkspaceRoot, scenario.Scope,
            chatBackedRunCommitBoundary: null, existingRunDetailCommitBoundary: null,
            genericNewRunCommitBoundary: null, jsonReadDiagnostics: null, historyCommitBoundary: stage => {
                if (!injected && stage == failureStage) {
                    injected = true;
                    throw new IOException("Injected history file handoff failure.");
                }
            });
        await Assert.ThrowsAsync<IOException>(() => failing.SaveExecutionRunDetailAsync(
            CreateRunDetail(runId, agentId, history.Clock.Now, "Fixture", "exact-model", [observation])));
        Assert.True(injected);
        var journal = new FileProviderHistoryJournal(scenario.WorkspaceRoot, scenario.Scope);
        var beforeOwnerRecovery = await journal.ReadBatchAsync(history.Partition, 10);
        Assert.Equal(failureStage == FileHistoryCommitStage.Prepared ? 0 : 1, beforeOwnerRecovery.Count);

        var recoveredStore = new FileSandboxWorkspaceStore(scenario.WorkspaceRoot, scenario.Scope);
        var detail = await recoveredStore.GetExecutionRunDetailAsync(runId);
        Assert.Equal(observation.Id, Assert.Single(detail!.UsageObservations).Id);
        var published = Assert.Single(await journal.ReadBatchAsync(history.Partition, 10));
        await history.Projection.ApplyAsync(published.Mutation, default);
        var afterDatabaseCrash = Assert.Single(await new FileProviderHistoryJournal(
            scenario.WorkspaceRoot, scenario.Scope).ReadBatchAsync(history.Partition, 10));
        Assert.Equal(published.Version, afterDatabaseCrash.Version);
        await history.Projection.ApplyAsync(afterDatabaseCrash.Mutation, default);
        await journal.AcknowledgeAsync(afterDatabaseCrash);
        Assert.Empty(await journal.ReadBatchAsync(history.Partition, 10));
        await using (var db = history.Factory.CreateDbContext()) {
            var entry = Assert.Single(await db.Set<HistoryEntryRow>().AsNoTracking().ToListAsync());
            Assert.Equal(start.EntryId.Value, entry.Id);
            Assert.Equal(10, entry.InputTokens);
            Assert.Equal(0.01m, entry.Amount);
            Assert.Equal(HistoryRetentionAuthority.CanonicalOwner, entry.RetentionAuthority);
            Assert.Empty(await db.Set<HistoryDetailRow>().ToListAsync());
        }
        var journalRoot = Path.Combine(scenario.WorkspaceRoot, ".provider-history", scenario.Scope.PartitionRelativePath);
        foreach (var path in Directory.EnumerateFiles(journalRoot, "*.json", SearchOption.AllDirectories)) {
            var text = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("raw-private-do-not-copy", text, StringComparison.Ordinal);
            Assert.DoesNotContain("diagnostics-private-do-not-copy", text, StringComparison.Ordinal);
        }

        var document = await recoveredStore.LoadAsync();
        await recoveredStore.SaveAsync(document with { ExecutionRuns = [], ProviderUsageObservations = [] });
        var deleted = Assert.Single(await journal.ReadBatchAsync(history.Partition, 10));
        Assert.Equal(HistorySourceMutationKind.Delete, deleted.Mutation.Kind);
        Assert.True(deleted.Version > published.Version);
        await history.Projection.ApplyAsync(deleted.Mutation, default);
        await history.Projection.ApplyAsync(published.Mutation, default);
        await journal.AcknowledgeAsync(deleted);
        await using var finalDb = history.Factory.CreateDbContext();
        var hidden = Assert.Single(await finalDb.Set<HistoryEntryRow>().AsNoTracking().ToListAsync());
        Assert.False(hidden.IsVisible);
        Assert.Equal(HistoryDetailState.Deleted, hidden.DetailState);
        Assert.Empty(await journal.ReadBatchAsync(history.Partition, 10));
    }

    [Theory]
    [InlineData((int)FileHistoryCommitStage.LegacyBindingPersisted)]
    [InlineData((int)FileHistoryCommitStage.LegacyHeadBound)]
    [InlineData((int)FileHistoryCommitStage.AcknowledgmentPersisted)]
    public async Task Legacy_file_binding_and_acknowledgment_recover_without_rebinding_or_duplicates(int failureStageValue) {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var scenario = UsageProjectionScenario.Create();
        var agent = await scenario.GetAgentIdAsync();
        var run = Guid.NewGuid();
        var old = history.Clock.Now.AddDays(-60);
        var observation = CreateUsageObservation(run, agent, old, "Legacy provider", ProviderKind.OpenAi, "legacy-model", 15, 4);
        await scenario.Store.SaveExecutionRunDetailAsync(CreateRunDetail(run, agent, old,
            "Legacy provider", "legacy-model", [observation]));
        var stage = (FileHistoryCommitStage)failureStageValue;
        var failing = new FileProviderHistoryJournal(new(scenario.WorkspaceRoot, scenario.Scope), current => {
            if (current == stage) {
                throw new IOException("Injected legacy journal handoff failure.");
            }
        });
        var journal = new FileProviderHistoryJournal(scenario.WorkspaceRoot, scenario.Scope);
        FileHistoryPublication publication;
        if (stage == FileHistoryCommitStage.AcknowledgmentPersisted) {
            publication = Assert.Single(await journal.ReadBatchAsync(history.Partition, 1));
            await history.Projection.ApplyAsync(publication.Mutation, default);
            await Assert.ThrowsAsync<IOException>(() => failing.AcknowledgeAsync(publication));
            Assert.Empty(await journal.ReadBatchAsync(history.Partition, 10));
        } else {
            await Assert.ThrowsAsync<IOException>(() => failing.ReadBatchAsync(history.Partition, 10));
            publication = Assert.Single(await journal.ReadBatchAsync(history.Partition, 10));
            await history.Projection.ApplyAsync(publication.Mutation, default);
            await journal.AcknowledgeAsync(publication);
            Assert.Empty(await journal.ReadBatchAsync(history.Partition, 10));
        }
        var source = publication.Mutation.Source;
        var read = await journal.ReadAsync(source);
        Assert.Equal(publication.Version, read!.Version.Value);
        Assert.Null(read.Entry!.StartedAtUtc);
        Assert.Null(read.Entry.AttemptId);
        Assert.Equal(HistoryGranularity.LegacyAggregate, read.Entry.Granularity);
        Assert.Equal(old, read.Entry.SortAtUtc);
        var foreign = new HistoryPartition(Guid.NewGuid(), Guid.NewGuid(), "other");
        Assert.Null(await journal.ReadAsync(source with { Partition = foreign }));
        Assert.Empty(await journal.ReadBatchAsync(foreign, 10));
        var path = Path.Combine(scenario.Scope.DataRootRelativePath, "execution", "runs", run.ToString("N"),
            "usage", observation.Id.ToString("N") + ".json");
        Assert.False(await journal.StageExistingAsync(path, foreign));
        await using var db = history.Factory.CreateDbContext();
        Assert.Equal(observation.Id, Assert.Single(await db.Set<HistoryEntryRow>().ToListAsync()).Id);
    }

    [Fact]
    public async Task File_backfill_resumes_bounded_metadata_chunks_without_recopying_canonical_bodies() {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var scenario = UsageProjectionScenario.Create();
        var agent = await scenario.GetAgentIdAsync();
        var run = Guid.NewGuid();
        var old = history.Clock.Now.AddDays(-60);
        await scenario.Store.SaveExecutionRunDetailAsync(CreateRunDetail(run, agent, old, "Old", "model", []));
        var layout = new FileSandboxWorkspaceStorageLayout(scenario.WorkspaceRoot, scenario.Scope);
        var historicalWriter = new FileSandboxWorkspaceJsonStore();
        var ids = new HashSet<Guid>();
        for (var index = 0; index < 5; index++) {
            var observation = CreateUsageObservation(run, agent, old.AddSeconds(index),
                "Old", ProviderKind.OpenAi, "model", index + 1, 1) with {
                RawUsageJson = "{\"private\":\"legacy-body-must-stay-at-owner\"}"
            };
            ids.Add(observation.Id);
            await historicalWriter.WriteJsonAtomicallyAsync(Path.Combine(layout.RunUsageRoot(run),
                observation.Id.ToString("N") + ".json"), observation, default);
        }
        var journal = new FileProviderHistoryJournal(layout, null, new SingleRecordBudgetClock());
        var backfill = new FileHistoryBackfill(scenario.WorkspaceRoot, scenario.Scope, history.Partition, new SingleRecordBudgetClock());
        var staged = 0;
        var completed = false;
        try {
            for (var iteration = 0; iteration < 20 && !completed; iteration++) {
                if (iteration == 2) {
                    backfill.Dispose();
                    backfill = new(scenario.WorkspaceRoot, scenario.Scope, history.Partition, new SingleRecordBudgetClock());
                }
                var progress = await backfill.ProcessAsync(2);
                Assert.InRange(progress.DiscoverySteps, 0, 2);
                Assert.InRange(progress.VisitedFiles, 0, 1);
                staged += progress.StagedRecords;
                var publications = await journal.ReadBatchAsync(history.Partition, 2);
                Assert.InRange(publications.Count, 0, 1);
                foreach (var publication in publications) {
                    await history.Projection.ApplyAsync(publication.Mutation, default);
                    await journal.AcknowledgeAsync(publication);
                }
                completed = progress.AllSourceIntentsStaged;
            }
            Assert.True(completed);
            Assert.Equal(5, staged);
            Assert.Equal(0, (await backfill.ProcessAsync(2)).VisitedFiles);
            await using var db = history.Factory.CreateDbContext();
            var entries = await db.Set<HistoryEntryRow>().AsNoTracking().ToListAsync();
            Assert.Equal(ids.Order(), entries.Select(entry => entry.Id).Order());
            Assert.All(entries, entry => {
                Assert.Equal(HistoryTimeBasis.CanonicalRecorded, entry.TimeBasis);
                Assert.Null(entry.StartedAtUtc);
                Assert.Equal(1, entry.Version);
            });
            Assert.Empty(await db.Set<HistoryDetailRow>().ToListAsync());
            foreach (var path in Directory.EnumerateFiles(Path.Combine(scenario.WorkspaceRoot, ".provider-history"),
                "*.json", SearchOption.AllDirectories)) {
                Assert.DoesNotContain("legacy-body-must-stay-at-owner", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
            }
        } finally {
            backfill.Dispose();
        }
    }

    [Fact]
    public async Task File_source_checkpoints_large_manifest_in_bounded_passes_and_resumes_after_restart() {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        var profile = Guid.NewGuid();
        await using var scenario = UsageProjectionScenario.Create(WorkspaceScopeDescriptor.Organization(profile.ToString("N")));
        var agent = await scenario.GetAgentIdAsync();
        var run = Guid.NewGuid();
        await scenario.Store.SaveExecutionRunDetailAsync(CreateRunDetail(run, agent, history.Clock.Now, "Legacy", "model", []));
        var layout = new FileSandboxWorkspaceStorageLayout(scenario.WorkspaceRoot, scenario.Scope);
        var writer = new FileSandboxWorkspaceJsonStore();
        var ids = new HashSet<Guid>();
        for (var index = 0; index < 100; index++) {
            var observation = CreateUsageObservation(run, agent, history.Clock.Now.AddSeconds(index),
                "Legacy", ProviderKind.OpenAi, "model", index + 1, 1);
            ids.Add(observation.Id);
            await writer.WriteJsonAtomicallyAsync(Path.Combine(layout.RunUsageRoot(run),
                observation.Id.ToString("N") + ".json"), observation, default);
        }
        var runtime = new DatabaseRuntimeState(new DatabaseSwitchNotificationService());
        var context = new HistoryMaintenanceContext(history.Partition, runtime.GetSnapshot(), runtime);
        var runner = new HistorySourceMaintenanceRunner(history.Factory, TimeProvider.System);
        AgentFileHistorySource CreateSource() => new(history.Factory, new FixedHistoryProfile(profile),
            new HistoryWorkspacePaths(scenario.WorkspaceRoot), new(history.Factory),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentFileHistorySource>.Instance,
            new SingleRecordBudgetClock());
        var source = CreateSource();
        var complete = false;
        var projectedCount = 0;
        try {
            var maximumPasses = ids.Count + 4;
            for (var pass = 0; pass < maximumPasses && !complete; pass++) {
                if (pass == 3) {
                    Assert.InRange(projectedCount, 1, ids.Count - 1);
                    source.Dispose();
                    source = CreateSource();
                }
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                Assert.True(await runner.ProcessAsync(source, context, 100, timeout.Token));
                await using var db = history.Factory.CreateDbContext();
                var checkpoint = await db.Set<HistoryCheckpointRow>().SingleAsync(row =>
                    row.PartitionId == history.Partition.StorageLineageId && row.SourceKind == source.Kind);
                Assert.Null(checkpoint.FailureCode);
                var currentCount = await db.Set<HistoryEntryRow>().CountAsync();
                Assert.InRange(currentCount - projectedCount, 0, 1);
                projectedCount = currentCount;
                complete = checkpoint.Coverage == HistoryCoverageState.Current;
            }
            Assert.True(complete, "A retained manifest must converge while each pass exhausts its work budget after one record.");
            await using var final = history.Factory.CreateDbContext();
            Assert.Equal(ids.Order(), (await final.Set<HistoryEntryRow>().Select(row => row.Id).ToListAsync()).Order());
            Assert.Equal(100, await final.Set<AgentHistoryLocator>().CountAsync());
            Assert.Empty(await final.Set<HistoryDetailRow>().ToListAsync());
            Assert.Empty(await new FileProviderHistoryJournal(scenario.WorkspaceRoot, scenario.Scope).ReadBatchAsync(history.Partition, 100));
        } finally {
            source.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Application_file_source_commits_locator_and_index_before_acknowledging(bool failAfterFlush) {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        var profile = Guid.NewGuid();
        await using var scenario = UsageProjectionScenario.Create(WorkspaceScopeDescriptor.Organization(profile.ToString("N")));
        var agent = await scenario.GetAgentIdAsync();
        var run = Guid.NewGuid();
        var start = history.Start();
        var usage = CreateUsageObservation(run, agent, history.Clock.Now, "Local", ProviderKind.OpenAi, "model", 900, 300) with {
            HistoryEvidence = new(start.RequestId, true, [HistoryAttemptEvidence.Create(start, history.Completion())]),
            RawUsageJson = "{\"prompt\":\"must-not-be-copied\"}"
        };
        var session = new ChatSessionRecord(Guid.NewGuid(), agent, "Retained conversation", history.Clock.Now, history.Clock.Now, [
            new(Guid.NewGuid(), ChatMessageRole.User, "Full user prompt", history.Clock.Now, 4),
            new(Guid.NewGuid(), ChatMessageRole.Assistant, new string('x', 200) + " full retained answer password=secret", history.Clock.Now.AddSeconds(1), 60)
        ]);
        var runDetail = CreateRunDetail(run, agent, history.Clock.Now, "Local", "model", [usage with { ChatSessionId = session.Id }]);
        await scenario.Store.SaveExecutionRunDetailAsync(runDetail with {
            Run = runDetail.Run with { ChatSessionId = session.Id }, ChatSession = session
        });
        using var queue = new FileHistoryReadyQueue(scenario.WorkspaceRoot);
        Assert.Equal(scenario.Scope, await queue.NextAsync(history.Partition, default));
        var journal = new FileProviderHistoryJournal(scenario.WorkspaceRoot, scenario.Scope);
        var publication = Assert.Single(await journal.ReadBatchAsync(history.Partition, 10));
        if (failAfterFlush) {
            var failingStore = new AgentHistoryPublicationStore(history.Factory.WithInterceptor(new FailAfterLocatorFlush()));
            using var failingSource = new AgentFileHistorySource(history.Factory, new FixedHistoryProfile(profile),
                new HistoryWorkspacePaths(scenario.WorkspaceRoot), failingStore, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentFileHistorySource>.Instance);
            await Assert.ThrowsAsync<InvalidOperationException>(() => failingSource.ProcessAsync(history.Maintenance, null, 10, default));
            await using var db = history.Factory.CreateDbContext();
            Assert.Empty(await db.Set<HistoryEntryRow>().ToArrayAsync());
            Assert.Empty(await db.Set<AgentHistoryLocator>().ToArrayAsync());
            Assert.Single(await journal.ReadBatchAsync(history.Partition, 10));
        }
        using var source = new AgentFileHistorySource(history.Factory, new FixedHistoryProfile(profile),
            new HistoryWorkspacePaths(scenario.WorkspaceRoot), new(history.Factory), Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentFileHistorySource>.Instance);
        var progress = await source.ProcessAsync(history.Maintenance, null, 10, default);
        progress = await source.ProcessAsync(history.Maintenance, progress.Cursor, 10, default);
        Assert.True(progress.BackfillComplete);
        Assert.Empty(await journal.ReadBatchAsync(history.Partition, 10));
        Assert.False(queue.HasPending(history.Partition));
        var exact = await source.ReadAsync(publication.Mutation.Source, default);
        Assert.Equal(start.EntryId, Assert.Single(exact!.Attempts).Id);
        Assert.Null(await source.ReadAsync(publication.Mutation.Source with { Owner = new(Guid.NewGuid().ToString("N")) }, default));
        await using (var db = history.Factory.CreateDbContext()) {
            var row = await db.Set<HistoryEntryRow>().SingleAsync();
            Assert.Equal(10, row.InputTokens);
            Assert.Equal(0.01m, row.Amount);
            var locator = await db.Set<AgentHistoryLocator>().SingleAsync();
            Assert.Equal(run, locator.OwnerId);
            Assert.Equal(scenario.Scope.Key, locator.ScopeKey);
            Assert.Empty(await db.Set<HistoryDetailRow>().ToArrayAsync());
        }
        var content = await source.ReadDetailAsync(publication.Mutation.Source, start.EntryId, default);
        Assert.Equal(HistoryDetailState.Canonical, content.State);
        Assert.Null(content.Input);
        Assert.Equal("summary", content.Sections.Single(section => section.Title == "Run input summary").Content.Text);
        Assert.Equal("completed", content.Sections.Single(section => section.Title == "Run result summary").Content.Text);
        var transcript = content.Sections.Single(section => section.Title.StartsWith("Linked conversation", StringComparison.Ordinal)).Content;
        Assert.Contains("Full user prompt", transcript.Text);
        Assert.Contains("full retained answer", transcript.Text);
        Assert.DoesNotContain("password=secret", transcript.Text);
        Assert.True(transcript.Flags.HasFlag(HistoryDetailFlags.Redacted));
        Assert.Equal(HistoryDetailState.Unavailable,
            (await source.ReadDetailAsync(publication.Mutation.Source, HistoryEntryId.New(), default)).State);
        Assert.Equal(HistoryDetailState.Unavailable,
            (await source.ReadDetailAsync(publication.Mutation.Source with { Owner = new(Guid.NewGuid().ToString("N")) }, start.EntryId, default)).State);
    }

    [Fact]
    public async Task Deleted_project_reconciliation_and_late_file_publication_cannot_restore_history() {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        var project = Guid.NewGuid();
        await using var scenario = UsageProjectionScenario.Create(WorkspaceScopeDescriptor.Project(project.ToString("D")));
        await using (var db = history.Factory.CreateDbContext()) {
            db.Add(new Project { Id = project, Name = "History owner", Slug = project.ToString("N") });
            await db.SaveChangesAsync();
        }
        var agent = await scenario.GetAgentIdAsync();
        var run = Guid.NewGuid();
        var usage = CreateUsageObservation(run, agent, history.Clock.Now, "Local", ProviderKind.OpenAi, "model", 10, 3);
        await scenario.Store.SaveExecutionRunDetailAsync(CreateRunDetail(run, agent, history.Clock.Now, "Local", "model", [usage]));
        var journal = new FileProviderHistoryJournal(scenario.WorkspaceRoot, scenario.Scope);
        var publication = Assert.Single(await journal.ReadBatchAsync(history.Partition, 10));
        var store = new AgentHistoryPublicationStore(history.Factory);
        await store.PublishAsync(history.Partition, scenario.Scope, [publication], default);
        await journal.AcknowledgeAsync(publication);
        await using (var db = history.Factory.CreateDbContext()) {
            await db.Set<Project>().Where(row => row.Id == project).ExecuteDeleteAsync();
        }
        Assert.Equal(1, await store.ReconcileDeletedProjectsAsync(history.Partition, 10, default));
        await store.PublishAsync(history.Partition, scenario.Scope, [publication], default);
        var changed = usage with { OutputTokens = 4 };
        var detail = await scenario.Store.GetExecutionRunDetailAsync(run);
        Assert.NotNull(detail);
        await scenario.Store.SaveExecutionRunDetailAsync(detail! with {
            Run = NextRevision(detail.Run), UsageObservations = [changed]
        });
        var late = Assert.Single(await journal.ReadBatchAsync(history.Partition, 10));
        await store.PublishAsync(history.Partition, scenario.Scope, [late], default);
        await journal.AcknowledgeAsync(late);
        await using var result = history.Factory.CreateDbContext();
        Assert.False((await result.Set<HistoryEntryRow>().SingleAsync()).IsVisible);
        var tombstone = await result.Set<HistorySourceRow>().SingleAsync();
        Assert.True(tombstone.IsDeleted);
        Assert.True(tombstone.Version > late.Version);
        Assert.True((await result.Set<AgentHistoryLocator>().SingleAsync()).IsDeleted);
    }

    private sealed class FailAfterLocatorFlush : SaveChangesInterceptor {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (eventData.Context is AppDbContext db && db.ChangeTracker.Entries<AgentHistoryLocator>().Any()) {
                throw new InvalidOperationException("Crash after locator and index flush, before commit.");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedHistoryProfile(Guid id) : IDatabaseProfileRuntimeAccessor {
        public ResolvedDatabaseProfile ResolveCurrentProfile()
            => new(new() { Id = id }, DatabaseProfileResolutionSource.ExplicitOverride, "");
        public ResolvedDatabaseProfile ResolveProfile(Guid profileId)
            => profileId == id ? ResolveCurrentProfile() : throw new InvalidOperationException("Wrong test profile.");
    }

    private sealed class HistoryWorkspacePaths(string root) : IWorkspacePathResolver {
        public string ResolveWorkspaceRoot() => root;
        public string ResolveManagedFilesRoot() => Path.Combine(root, "managed");
        public string ResolveExportsRoot() => Path.Combine(root, "exports");
        public string ResolveEvidenceRoot() => Path.Combine(root, "evidence");
        public string ResolveManagerArtifactsRoot() => Path.Combine(root, "manager");
    }

    private static ExecutionRunRecord NextRevision(ExecutionRunRecord run)
    {
        return run with
        {
            Revision = run.Revision + 1,
            UpdatedAtUtc = run.UpdatedAtUtc.AddSeconds(1)
        };
    }

    private static ExecutionRunDetail CreateRunDetail(
        Guid runId,
        Guid agentId,
        DateTimeOffset createdAtUtc,
        string providerName,
        string model,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        return new ExecutionRunDetail(
            new ExecutionRunRecord(
                Id: runId,
                AgentId: agentId,
                ChatSessionId: null,
                Title: "Usage projection test",
                SourceKind: "integration-test",
                SourceId: $"run-{runId:N}",
                CorrelationId: $"corr-{runId:N}",
                CausationId: string.Empty,
                RequestedBy: "test",
                RequestedByKind: "system",
                MetadataJson: "{}",
                InputSummary: "summary",
                ResultSummary: "completed",
                ProviderName: providerName,
                Model: model,
                State: ExecutionState.Completed,
                Outcome: RunOutcome.Succeeded,
                CreatedAtUtc: createdAtUtc,
                UpdatedAtUtc: createdAtUtc,
                StartedAtUtc: createdAtUtc,
                CompletedAtUtc: createdAtUtc,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: string.Empty,
                PendingApprovals: []),
            ChatSession: null,
            ExecutionLog: [],
            Metrics: [])
        {
            UsageObservations = usageObservations
        };
    }

    private static ProviderUsageObservation CreateUsageObservation(
        Guid executionRunId,
        Guid agentId,
        DateTimeOffset createdAtUtc,
        string providerName,
        ProviderKind providerKind,
        string model,
        int inputTokens,
        int outputTokens)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: createdAtUtc,
            ProviderName: providerName,
            ProviderKind: providerKind,
            Model: model,
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: inputTokens,
            CachedInputTokens: 0,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0)
        {
            ExecutionRunId = executionRunId,
            AgentId = agentId,
            CalculatedCostUsd = 0.001m
        };
    }

    private sealed class SingleRecordBudgetClock : TimeProvider {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Add(ref timestamp, TimeSpan.TicksPerSecond);
    }

    private sealed class UsageProjectionScenario(
        string workspaceRoot,
        WorkspaceScopeDescriptor scope) : IAsyncDisposable
    {
        public string WorkspaceRoot { get; } = workspaceRoot;

        public WorkspaceScopeDescriptor Scope { get; } = scope;

        public FileSandboxWorkspaceStore Store { get; } = new(
            workspaceRoot,
            scope);

        public string UsageIndexPath { get; } = Path.Combine(
            scope.ResolveDataRoot(workspaceRoot),
            "execution",
            "usage-index.json");

        public static UsageProjectionScenario Create(WorkspaceScopeDescriptor? scope = null)
        {
            var workspaceRoot = Path.Combine(
                Path.GetTempPath(),
                $"candoitall-usage-projection-{Guid.NewGuid():N}");
            return new UsageProjectionScenario(
                workspaceRoot,
                scope ?? WorkspaceScopeDescriptor.Organization(
                    "usage-projection-test"));
        }

        public async Task<Guid> GetAgentIdAsync()
        {
            var snapshot = await Store.LoadCatalogSnapshotAsync();
            return snapshot.Catalog.Agents
                .First(agent => !agent.IsTemplate)
                .Id;
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(WorkspaceRoot))
            {
                Directory.Delete(WorkspaceRoot, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
