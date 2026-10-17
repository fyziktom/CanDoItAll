using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

[Trait("Category", "LongRunning")]
public sealed class ProjectsProtectedMutationPerformanceTests(ITestOutputHelper output) {
    private const int Warmup = 20;
    private const int Attempts = 100;
    private const int Samples = 3;
    private const string WorkloadVersion = "protected-projects-v1";
    private static readonly ProjectDefinitionInvocation Invocation = new(ProjectMutationCallerScope.LocalWorkspace, new LocalReceiptAccess());

    [Fact]
    public async Task Measure_protected_replacements_with_matched_shapes_and_tail_diagnostics() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var token = deadline.Token;
        var probe = new ProjectsOperationPerformanceProbe();
        await using var app = await CreateApplicationAsync(probe);
        await EnvironmentAsync(app, "replace-matched-shapes", token);
        foreach (var children in new[] { 0, 5, 100, 1000 }) {
            var created = await CreateAsync(app, children, token);
            await ReplacementsAsync(app, probe, "Uncontended", [created.Result.Acknowledgement.Project.ProjectId], children, token);
            if (children == 1000) {
                await using var database = await CanonicalAsync(app, token);
                Write(new { Kind = "tail-plan", Children = children,
                    Plan = await ProjectsOperationStorageMeasurement.ChildReadPlanAsync(database, created.Result.Acknowledgement.Project.ProjectId, token) });
            }
        }
        var projects = new Guid[32];
        for (var index = 0; index < projects.Length; index++) {
            projects[index] = (await CreateAsync(app, 5, token)).Result.Acknowledgement.Project.ProjectId;
        }
        foreach (var writers in new[] { 8, 32 }) {
            await ReplacementsAsync(app, probe, "DisjointProjects", projects[..writers], 5, token);
            await ReplacementsAsync(app, probe, "SameProject", Enumerable.Repeat(projects[0], writers).ToArray(), 5, token);
        }
    }

    [Fact]
    public async Task Measure_protected_create_replay_lookup_and_bounded_receipt_cleanup_capacity() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var token = deadline.Token;
        var probe = new ProjectsOperationPerformanceProbe();
        await using var app = await CreateApplicationAsync(probe);
        await EnvironmentAsync(app, "create-replay-capacity", token);
        var accepted = new List<ProjectDefinitionRequest<CreateProjectDefinitionCommand>>();
        for (var sample = 1; sample <= Samples; sample++) {
            for (var index = 0; index < Warmup; index++) {
                _ = await CreateAsync(app, 5, token);
            }
            await MeasureAsync(app, probe, "Create", sample, 1, 5, async () => {
                var contextStarted = Stopwatch.GetTimestamp();
                ProjectDefinitionWriteContext metadata;
                using (probe.Enter(-1)) {
                    metadata = await ContextAsync(app, token);
                }
                var opening = Stopwatch.GetElapsedTime(contextStarted).TotalMilliseconds;
                var results = new List<Attempt>();
                for (var index = 0; index < Attempts; index++) {
                    var started = Stopwatch.GetTimestamp();
                    var request = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(Operation(metadata),
                        ProjectDefinitionRequestVersion.VersionOne, Creation(5));
                    var attempt = await ExecuteAsync(app, probe, index, 0, started,
                        (writer, ct) => writer.CreateAsync(request, Invocation, ct), token);
                    results.Add(attempt);
                    if (attempt.Outcome == "accepted") {
                        accepted.Add(request);
                    }
                }
                return new(results.ToArray(), opening, 1);
            }, token);
        }
        Assert.Equal(Samples * Attempts, accepted.Count);
        await using var canonical = await CanonicalAsync(app, token);
        var beforeReplay = await ProjectsOperationStorageMeasurement.ReadAsync(canonical, token);
        for (var sample = 1; sample <= Samples; sample++) {
            var requests = accepted.Skip((sample - 1) * Attempts).Take(Attempts).ToArray();
            await MeasureAsync(app, probe, "ReplayCreate", sample, 1, 5, async () => {
                var results = new List<Attempt>();
                for (var index = 0; index < requests.Length; index++) {
                    results.Add(await ExecuteAsync(app, probe, index, 0, Stopwatch.GetTimestamp(),
                        (writer, ct) => writer.CreateAsync(requests[index], Invocation, ct), token));
                }
                return new(results.ToArray(), 0, 0);
            }, token);
            await MeasureAsync(app, probe, "Lookup", sample, 1, 5, async () => {
                var results = new List<Attempt>();
                for (var index = 0; index < requests.Length; index++) {
                    var start = Stopwatch.GetTimestamp();
                    var collections = Collections();
                    using var observed = probe.Enter(index);
                    await using var scope = app.Services.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<ProjectDefinitionReceiptReader>()
                        .LookupAsync(requests[index].Operation, Invocation, token);
                    results.Add(new(index, 0, Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                        Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                        result is ProjectDefinitionLookup.FoundFull ? "found" : result.GetType().Name,
                        CollectionDelta(collections)));
                }
                return new(results.ToArray(), 0, 0);
            }, token);
        }
        var afterReplay = await ProjectsOperationStorageMeasurement.ReadAsync(canonical, token);
        Assert.Equal(beforeReplay.Receipts, afterReplay.Receipts);
        Assert.Equal(beforeReplay.Projects, afterReplay.Projects);
        Assert.Equal(beforeReplay.SearchDocuments, afterReplay.SearchDocuments);
        await MaintenanceAsync(app, probe, 5, 8193, token);
        await MaintenanceAsync(app, probe, 1000, 192, token);
    }

    private async Task ReplacementsAsync(TestApplication app, ProjectsOperationPerformanceProbe probe, string scenario,
        Guid[] projects, int children, CancellationToken token) {
        var warmContext = await ContextAsync(app, token);
        for (var sample = 1; sample <= Samples; sample++) {
            await WavesAsync(app, probe, projects, Enumerable.Repeat(warmContext, projects.Length).ToArray(), Warmup, token);
            await MeasureAsync(app, probe, scenario, sample, projects.Length, children, async () => {
                var started = Stopwatch.GetTimestamp();
                ProjectDefinitionWriteContext[] contexts;
                using (probe.Enter(-1)) {
                    contexts = await Task.WhenAll(projects.Select(_ => ContextAsync(app, token)));
                }
                var opening = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                return new(await WavesAsync(app, probe, projects, contexts, Attempts, token), opening, contexts.Length);
            }, token);
        }
    }

    private async Task MeasureAsync(TestApplication app, ProjectsOperationPerformanceProbe probe, string scenario,
        int sample, int writers, int children, Func<Task<Batch>> execute, CancellationToken token) {
        await using var database = await CanonicalAsync(app, token);
        var before = await ProjectsOperationStorageMeasurement.ReadAsync(database, token);
        var wal = await ProjectsOperationStorageMeasurement.ReadWalAsync(database, token);
        probe.Reset();
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var collections = Collections();
        var started = Stopwatch.GetTimestamp();
        var batch = await execute();
        var elapsed = Stopwatch.GetElapsedTime(started);
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;
        var collectionDelta = CollectionDelta(collections);
        var observed = probe.Snapshot();
        var walBytes = await ProjectsOperationStorageMeasurement.WalDeltaAsync(database, wal, token);
        var after = await ProjectsOperationStorageMeasurement.ReadAsync(database, token);
        var accepted = batch.Attempts.Count(item => item.Outcome == "accepted");
        var byAttempt = observed.Where(item => item.Operation >= 0).GroupBy(item => item.Operation)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var tails = batch.Attempts.OrderByDescending(item => item.SaveMilliseconds).Take(5).Select(item => {
            var commands = byAttempt.GetValueOrDefault(item.Index) ?? [];
            return new { item.Index, item.Outcome, item.ReadMilliseconds, item.SaveMilliseconds, item.EndToEndMilliseconds,
                item.CollectionsOverlappingTimedCall, ObservedCommandCount = commands.Length,
                ObservedCommandMilliseconds = commands.Sum(command => command.Milliseconds),
                ObservedCommands = ProjectsOperationPerformanceProbe.Summarize(commands) };
        }).ToArray();
        Write(new {
            Kind = "sample", Scenario = scenario, Sample = sample, Writers = writers, ChildrenPerCollection = children,
            Attempts = batch.Attempts.Length, Accepted = accepted,
            Outcomes = batch.Attempts.GroupBy(item => item.Outcome).ToDictionary(group => group.Key, group => group.Count()),
            EndToEndBatchMilliseconds = elapsed.TotalMilliseconds,
            batch.OpeningContextMilliseconds, batch.ContextReads,
            AttemptsPerSecond = batch.Attempts.Length / elapsed.TotalSeconds, NewAcceptedPerSecond = accepted / elapsed.TotalSeconds,
            ReadMilliseconds = ProjectsOperationPerformanceProbe.Distribution(batch.Attempts.Select(item => item.ReadMilliseconds)),
            SaveMilliseconds = ProjectsOperationPerformanceProbe.Distribution(batch.Attempts.Select(item => item.SaveMilliseconds)),
            AcceptedSaveMilliseconds = ProjectsOperationPerformanceProbe.Distribution(batch.Attempts.Where(item => item.Outcome == "accepted").Select(item => item.SaveMilliseconds)),
            EndToEndMilliseconds = ProjectsOperationPerformanceProbe.Distribution(batch.Attempts.Select(item => item.EndToEndMilliseconds)),
            AllocatedProcessBytes = allocatedBytes, AllocatedProcessBytesPerAttempt = (double)allocatedBytes / batch.Attempts.Length,
            GarbageCollections = collectionDelta, ClusterWalBytes = walBytes,
            ClusterWalBytesPerAttempt = (double)walBytes / batch.Attempts.Length,
            ClusterWalBytesPerNewAccepted = accepted == 0 ? (double?)null : (double)walBytes / accepted,
            ObservedCommandCount = observed.Length, ObservedCommandsPerAttempt = (double)observed.Length / batch.Attempts.Length,
            ObservedCommands = ProjectsOperationPerformanceProbe.Summarize(observed),
            ReceiptRowsAdded = after.Receipts - before.Receipts, ReceiptPayloadBytesAdded = after.PayloadBytes - before.PayloadBytes,
            PayloadBytesPerNewAccepted = accepted == 0 ? (double?)null : (double)(after.PayloadBytes - before.PayloadBytes) / accepted,
            ReceiptHeapGrowthBytes = after.HeapBytes - before.HeapBytes,
            ReceiptIndexGrowthBytes = after.IndexBytes - before.IndexBytes,
            ReceiptRelationGrowthBytes = after.TotalBytes - before.TotalBytes, Storage = after, Tails = tails
        });
        Assert.Equal(Attempts, batch.Attempts.Length);
        Assert.Equal(accepted, after.Receipts - before.Receipts);
        Assert.Contains(observed, command => command.Owner == nameof(ProjectsDbContext));
        if (scenario is "Uncontended" or "DisjointProjects" or "Create") {
            Assert.All(batch.Attempts, attempt => Assert.Equal("accepted", attempt.Outcome));
        } else if (scenario == "SameProject") {
            Assert.Equal((int)Math.Ceiling((double)Attempts / writers), accepted);
            Assert.All(batch.Attempts, attempt => Assert.Contains(attempt.Outcome, new[] { "accepted", ProjectErrorCodes.DefinitionConflict }));
        } else {
            Assert.All(batch.Attempts, attempt => Assert.Equal(scenario == "Lookup" ? "found" : "replayed", attempt.Outcome));
        }
        if (accepted > 0) {
            Assert.Contains(observed, command => command.Owner == nameof(SearchDbContext));
        }
    }

    private static async Task<Attempt[]> WavesAsync(TestApplication app, ProjectsOperationPerformanceProbe probe,
        Guid[] projects, ProjectDefinitionWriteContext[] contexts, int attempts, CancellationToken token) {
        var results = new List<Attempt>(attempts);
        for (var offset = 0; offset < attempts; offset += projects.Length) {
            var waveOffset = offset;
            var count = Math.Min(projects.Length, attempts - offset);
            var reads = await Task.WhenAll(projects.Take(count).Select(async (id, index) => {
                var start = Stopwatch.GetTimestamp();
                using var observed = probe.Enter(waveOffset + index);
                await using var scope = app.Services.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ProjectDefinitionReader>().ReadAsync(id, token);
                Assert.True(result.IsSuccess);
                return new Read(result.Value!, start, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }));
            results.AddRange(await Task.WhenAll(reads.Select((read, index) => {
                var command = Replacement(read.Snapshot, $"change-{waveOffset + index}");
                var request = new ProjectDefinitionRequest<ReplaceProjectDefinitionCommand>(Operation(contexts[index]),
                    ProjectDefinitionRequestVersion.VersionOne, command);
                return ExecuteAsync(app, probe, waveOffset + index, read.Milliseconds, read.Started,
                    (writer, ct) => writer.ReplaceAsync(request, Invocation, ct), token);
            })));
        }
        return results.ToArray();
    }

    private static async Task<Attempt> ExecuteAsync(TestApplication app, ProjectsOperationPerformanceProbe probe, int index,
        double readMilliseconds, long endToEndStarted,
        Func<ProjectDefinitionWriter, CancellationToken, Task<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>>> execute,
        CancellationToken token) {
        using var observed = probe.Enter(index);
        await using var scope = app.Services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<ProjectDefinitionWriter>();
        var collections = Collections();
        var started = Stopwatch.GetTimestamp();
        string outcome;
        try {
            var result = await execute(writer, token);
            outcome = result switch {
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted { Replayed: true } => "replayed",
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted => "accepted",
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected rejected => string.Join(",", rejected.Errors.Select(error => error.Code).Distinct().Order()),
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Unconfirmed unconfirmed => $"unconfirmed:{unconfirmed.Reason}",
                _ => "unsupported-outcome"
            };
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            var cause = exception.GetBaseException();
            outcome = cause is PostgresException postgres ? $"fault:{postgres.SqlState}" : $"fault:{cause.GetType().Name}";
        }
        return new(index, readMilliseconds, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            Stopwatch.GetElapsedTime(endToEndStarted).TotalMilliseconds, outcome, CollectionDelta(collections));
    }

    private async Task MaintenanceAsync(TestApplication app, ProjectsOperationPerformanceProbe probe, int children,
        int requestedRows, CancellationToken token) {
        var template = await CreateAsync(app, children, token);
        await using var scope = app.Services.CreateAsyncScope();
        var canonicalProfile = scope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var binding = await scope.ServiceProvider.GetRequiredService<DatasetActivationService>()
            .ReadActiveBindingAsync(canonicalProfile.Profile.Id, token);
        var epochs = scope.ServiceProvider.GetRequiredService<FiniteOperationEpochStore>();
        var retention = scope.ServiceProvider.GetRequiredService<FiniteOperationReceiptMaintenance>();
        var pool = scope.ServiceProvider.GetServices<FiniteOperationPoolRegistration>().Single(registration => registration.Pool == ProjectFiniteOperationPool.Pool);
        var schedule = new FiniteOperationMaintenanceState([pool]);
        await using var database = await CanonicalAsync(app, token);
        var operation = template.Result.Operation.Id;
        var payloadBytes = await database.Set<ProjectDefinitionReceiptRecord>().Where(row => row.OperationId == operation)
            .Select(row => row.AcceptedPayload.Length).SingleAsync(token);
        const int MaximumSyntheticPayloadBytes = 64 * 1024 * 1024;
        var rows = Math.Min(requestedRows, MaximumSyntheticPayloadBytes / payloadBytes);
        var inactiveNamespace = Guid.NewGuid();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Infrastructure_FiniteOperationState" ("NamespaceId", "PoolId", "RetentionProtocolVersion", "CurrentEpoch", "RetiredThroughEpoch")
            VALUES ({inactiveNamespace}, {pool.Pool.Value}, {pool.ProtocolVersion}, 1, 0);
            INSERT INTO "Infrastructure_FiniteOperationEpochs" ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc",
                "ClosedAtUtc", "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
            VALUES ({inactiveNamespace}, {pool.Pool.Value}, 1, 2, clock_timestamp() - interval '3 days', clock_timestamp() - interval '2 days',
                clock_timestamp() - interval '1 day', 1000, 1);
            INSERT INTO "Projects_DefinitionReceipts" ("NamespaceId", "Epoch", "CallerKind", "CallerKey", "OperationId",
                "SchemaVersion", "NormalizationVersion", "ResultVersion", "CommandKind", "SemanticDigest", "ProjectId",
                "LifetimeId", "AcceptedRevision", "AcceptedAtUtc", "AcceptedPayload", "CreationFingerprint")
            SELECT {inactiveNamespace}, 1, r."CallerKind", r."CallerKey", gen_random_uuid(), r."SchemaVersion",
                r."NormalizationVersion", r."ResultVersion", r."CommandKind", r."SemanticDigest", r."ProjectId",
                r."LifetimeId", r."AcceptedRevision", r."AcceptedAtUtc", r."AcceptedPayload", r."CreationFingerprint"
            FROM "Projects_DefinitionReceipts" AS r CROSS JOIN generate_series(1, {rows})
            WHERE r."OperationId" = {operation}
            """, token);
        var retainedActive = await database.Set<ProjectDefinitionReceiptRecord>().LongCountAsync(row => row.NamespaceId == binding.Namespace.Value, token);
        Write(new { Kind = "maintenance-backlog", ChildrenPerReceipt = children, SeedRows = rows,
            PayloadBytesPerRow = payloadBytes, SyntheticPayloadBytes = (long)rows * payloadBytes,
            Storage = await ProjectsOperationStorageMeasurement.ReadAsync(database, token),
            Note = "Cleanup-only inactive fixture namespace; cloned payloads are not replayable addresses and are never submitted to the writer." });
        for (var cycle = 1; cycle <= 3; cycle++) {
            var before = await database.Set<ProjectDefinitionReceiptRecord>().LongCountAsync(row => row.NamespaceId == inactiveNamespace, token);
            if (before == 0) {
                break;
            }
            var wal = await ProjectsOperationStorageMeasurement.ReadWalAsync(database, token);
            var allocated = GC.GetTotalAllocatedBytes(precise: true);
            var started = Stopwatch.GetTimestamp();
            var configured = app.Services.GetRequiredService<IOptions<ProjectDefinitionOperationOptions>>().Value;
            FiniteOperationMaintenanceResult result;
            probe.Reset();
            using (var budget = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                budget.CancelAfter(ProjectDefinitionOperationOptions.MaintenanceBudget);
                using var observed = probe.Enter(cycle);
                result = await new FiniteOperationMaintenanceCycle(pool, schedule).RunAsync(epochs, retention, binding, budget.Token, token);
            }
            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocation = GC.GetTotalAllocatedBytes(precise: true) - allocated;
            var after = await database.Set<ProjectDefinitionReceiptRecord>().LongCountAsync(row => row.NamespaceId == inactiveNamespace, token);
            var deletedRows = before - after;
            var walBytes = await ProjectsOperationStorageMeasurement.WalDeltaAsync(database, wal, token);
            Write(new {
                Kind = "maintenance-cycle", ChildrenPerReceipt = children, RequestedSeedRows = requestedRows, SeedRows = rows,
                SyntheticPayloadBytes = (long)rows * payloadBytes, PayloadBytesPerRow = payloadBytes, Cycle = cycle,
                MaintenanceVersion = "neutral-pool-separated-family-retention-v3",
                Before = before, After = after, DeletedRows = deletedRows, ConfirmedBatchRows = result.DeletedReceipts,
                ConfirmedPayloadBytes = result.DeletedBytes, Stop = result.Stop.ToString(), result.CompletedBatches,
                EligibleWork = result.EligibleWork.ToString(), result.EligibleAtUtc, result.Failure,
                ElapsedMilliseconds = elapsed.TotalMilliseconds, AllocatedProcessBytes = allocation,
                ClusterWalBytes = walBytes, ObservedCommands = ProjectsOperationPerformanceProbe.Summarize(probe.Snapshot()),
                ConfiguredCycleBudgetMilliseconds = ProjectDefinitionOperationOptions.MaintenanceBudget.TotalMilliseconds,
                ConfiguredCadenceSeconds = configured.MaintenanceInterval.TotalSeconds,
                StructuralRowsPerCycleCeiling = ProjectDefinitionOperationOptions.MaximumMaintenanceBatches *
                    Math.Min(ProjectDefinitionOperationOptions.PurgeBatchCount, ProjectDefinitionOperationOptions.PurgeBatchBytes / payloadBytes),
                ObservedRowsPerConfiguredMinute = deletedRows * 60 / configured.MaintenanceInterval.TotalSeconds,
                BacklogExhausted = after == 0,
                ArrivalScenarios = new[] { 1d, 10d, 100d }.Select(perSecond => new {
                    AssumedAcceptedPerSecond = perSecond, ArrivalsPerMinute = perSecond * 60,
                    AssumedArrivalsPerCycle = perSecond * configured.MaintenanceInterval.TotalSeconds,
                    ObservedCycleNetRows = perSecond * configured.MaintenanceInterval.TotalSeconds - deletedRows,
                    SevenDayPayloadLowerBoundBytes = perSecond * 7 * 86400 * payloadBytes
                }).ToArray()
            });
            Assert.Null(result.Failure);
            Assert.InRange(result.CompletedBatches, 0, ProjectDefinitionOperationOptions.MaximumMaintenanceBatches);
            Assert.InRange(result.DeletedBytes, 0, (long)result.CompletedBatches * ProjectDefinitionOperationOptions.PurgeBatchBytes);
            Assert.InRange(deletedRows, result.DeletedReceipts,
                (long)ProjectDefinitionOperationOptions.MaximumMaintenanceBatches * ProjectDefinitionOperationOptions.PurgeBatchCount);
            Assert.Equal(retainedActive, await database.Set<ProjectDefinitionReceiptRecord>()
                .LongCountAsync(row => row.NamespaceId == binding.Namespace.Value, token));
        }
        Write(new { Kind = "maintenance-storage", ChildrenPerReceipt = children,
            Storage = await ProjectsOperationStorageMeasurement.ReadAsync(database, token),
            Note = "DELETE does not return allocated heap/index/TOAST space to the operating system; vacuum behavior is not measured." });
        await database.Set<ProjectDefinitionReceiptRecord>().Where(row => row.NamespaceId == inactiveNamespace).ExecuteDeleteAsync(token);
        await database.Set<FiniteOperationEpochRecord>().Where(row => row.NamespaceId == inactiveNamespace).ExecuteDeleteAsync(token);
        await database.Set<FiniteOperationStateRecord>().Where(row => row.NamespaceId == inactiveNamespace).ExecuteDeleteAsync(token);
    }

    private async Task EnvironmentAsync(TestApplication app, string lane, CancellationToken token) {
        await using var database = await CanonicalAsync(app, token);
        await database.Database.OpenConnectionAsync(token);
        Write(new {
            Kind = "environment", Lane = lane, CapturedAtUtc = DateTimeOffset.UtcNow,
            Runtime = RuntimeInformation.FrameworkDescription, OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Environment.ProcessorCount,
            ServerGc = System.Runtime.GCSettings.IsServerGC, PostgreSql = database.Database.GetDbConnection().ServerVersion,
            ProjectsAssembly = typeof(ProjectDefinitionWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            HarnessAssembly = GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            ActivityStream = app.Services.GetRequiredService<IActivityStream>().GetType().Name,
            Warmup, Attempts, Samples, DeadlineMinutes = 15,
            BackgroundReceiptMaintenance = "removed from owned measuring host; explicit bounded cycles only",
            Caller = "LocalWorkspace with always-allowed synthetic receipt access; HTTP authentication excluded",
            AllocationScope = "process-wide divided by attempts; includes preparation, scopes, context reads, observation; not per-request attribution",
            WalScope = "whole PostgreSQL cluster LSN delta; require isolated serialized lane, includes background WAL",
            ObservationLimits = "EF asynchronous commands only; raw lifecycle guard bypasses interceptor; reader timing excludes full materialization",
            ContextPolicy = "one write-context query per worker per sample opening; reused for 100 attempts; included in batch time/allocation",
            ProductionRetainedDataPreflight = "pending; no ordinary database inspected"
        });
    }

    private static Task<TestApplication> CreateApplicationAsync(ProjectsOperationPerformanceProbe probe)
        => TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
                services.ConfigureDbContext<SearchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
                foreach (var descriptor in services.Where(item => item.ServiceType == typeof(IHostedService) &&
                    item.ImplementationType == typeof(FiniteOperationMaintenance)).ToArray()) {
                    services.Remove(descriptor);
                }
            }
        });

    private static async Task<ProjectDefinitionWriteContext> ContextAsync(TestApplication app, CancellationToken token) {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProjectDefinitionOperationProtocol>().GetWriteContextAsync(Invocation.Caller, token);
    }

    private static Task<AppDbContext> CanonicalAsync(TestApplication app, CancellationToken token)
        => app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync(token);

    private static ProjectDefinitionOperation Operation(ProjectDefinitionWriteContext context)
        => new(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding);

    private static async Task<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted> CreateAsync(
        TestApplication app, int children, CancellationToken token) {
        var metadata = await ContextAsync(app, token);
        await using var scope = app.Services.CreateAsyncScope();
        return Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(
            await scope.ServiceProvider.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(
                new(Operation(metadata), ProjectDefinitionRequestVersion.VersionOne, Creation(children)), Invocation, token));
    }

    private static CreateProjectDefinitionCommand Creation(int children) {
        var categories = Enum.GetValues<ProjectOptionCategory>();
        return new(new($"Performance project {children}", "", "Baseline", default, "", null),
            Enumerable.Range(0, children).Select(index => new ProjectDefinitionPhaseInput(null, $"Phase {index}", "Baseline phase", default, null, null)).ToArray(),
            Enumerable.Range(0, children).Select(index => new ProjectDefinitionOptionInput(null, categories[index % categories.Length], $"Option {index}", "Baseline option")).ToArray(), null);
    }

    private static ReplaceProjectDefinitionCommand Replacement(ProjectDefinitionSnapshot original, string label)
        => new(original.Expectation, original.Scalars with { Objective = label },
            original.Phases.Select((row, index) => new ProjectDefinitionPhaseInput(row.Id, row.Name, index == 0 ? label : row.Goal,
                row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(),
            original.Options.Select((row, index) => new ProjectDefinitionOptionInput(row.Id, row.Category, row.OptionName,
                index == 0 ? label : row.Notes)).ToArray(), null);

    private static int[] Collections() => Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
    private static int[] CollectionDelta(int[] before) => Enumerable.Range(0, 3).Select(generation => GC.CollectionCount(generation) - before[generation]).ToArray();
    private void Write(object record) => output.WriteLine(JsonSerializer.Serialize(new { WorkloadVersion, Record = record }));
    private sealed record Read(ProjectDefinitionSnapshot Snapshot, long Started, double Milliseconds);
    private sealed record Attempt(int Index, double ReadMilliseconds, double SaveMilliseconds, double EndToEndMilliseconds,
        string Outcome, int[] CollectionsOverlappingTimedCall);
    private sealed record Batch(Attempt[] Attempts, double OpeningContextMilliseconds, int ContextReads);
    private sealed class LocalReceiptAccess : IProjectDefinitionReceiptAccess {
        public Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
