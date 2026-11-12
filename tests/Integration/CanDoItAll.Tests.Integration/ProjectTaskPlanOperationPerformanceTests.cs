using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "LongRunning")]
public sealed class ProjectTaskPlanOperationPerformanceTests(ITestOutputHelper output) {
    [Fact]
    public async Task Measure_bounded_dormant_task_plan_publication_and_exact_replay() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var token = deadline.Token;
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        Write(new { Kind = "environment", Workload = "workbench-task-plan-owner-v1",
            Runtime = RuntimeInformation.FrameworkDescription, OperatingSystem = RuntimeInformation.OSDescription,
            SourceModule = typeof(ProjectTaskPlanPublisher).Assembly.ManifestModule.ModuleVersionId,
            BackgroundMaintenance = "disabled only for this owned test application",
            AllocationScope = "process-wide batch delta; concurrent per-operation attribution unavailable",
            SqlScope = "Workbench EF commands only; enlisted Projects and neutral contexts may be absent",
            PhaseScope = "capture plus write-context, save, replay; internal lock/staging/encode/commit phases uninstrumented",
            Sampling = "three finite samples; no discarded warmup; one capture and write-context read per worker per batch",
            Capacity = "finite synthetic workload; sustained arrival, two-family fairness, vacuum and production distributions not measured" });
        foreach (var existing in new[] { 0, 10, 100, 900 }) {
            foreach (var appended in new[] { 1, 10, 100 }) {
                for (var sample = 1; sample <= 3; sample++) {
                    var project = await NewProjectAsync(fixture, token);
                    await SeedAsync(fixture, project, existing, token);
                    await MeasureAsync(fixture, "uncontended", sample, existing, appended, [project], token);
                }
            }
        }
        foreach (var writers in new[] { 1, 8, 32 }) {
            foreach (var shared in new[] { false, true }) {
                for (var sample = 1; sample <= 3; sample++) {
                    var projects = new ProjectWriteAdmission[writers];
                    for (var index = 0; index < writers; index++) {
                        projects[index] = shared && index > 0 ? projects[0] : await NewProjectAsync(fixture, token);
                    }
                    await MeasureAsync(fixture, shared ? "same-project" : "disjoint-projects", sample, 0, 1, projects, token);
                }
            }
        }
        var full = await NewProjectAsync(fixture, token);
        await SeedAsync(fixture, full, 1000, token);
        var original = await fixture.CommandAsync(1, token, full);
        var refused = Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(original, fixture.Invocation, token));
        Assert.Equal(ProjectTaskPlanRefusal.InvalidInput, refused.Reason);
        Write(new { Kind = "candidate-boundary", Existing = 1000, Attempted = 1, Outcome = refused.Reason.ToString() });
    }

    private async Task MeasureAsync(ProjectTaskPlanOperationFixture fixture, string scenario, int sample, int existing,
        int appended, ProjectWriteAdmission[] projects, CancellationToken token) {
        var before = await StorageAsync(fixture, token);
        fixture.Probe.Reset();
        var allocations = GC.GetTotalAllocatedBytes(precise: true);
        var batchStarted = Stopwatch.GetTimestamp();
        var openingStarted = Stopwatch.GetTimestamp();
        var commands = await Task.WhenAll(projects.Select(project => fixture.CommandAsync(appended, token, project)));
        var openingMilliseconds = Stopwatch.GetElapsedTime(openingStarted).TotalMilliseconds;
        var attempts = await Task.WhenAll(commands.Select(async command => {
            using var authority = new TaskPlanAuthority();
            var started = Stopwatch.GetTimestamp();
            var result = await fixture.Publisher().PublishAsync(command, new(fixture.Caller, authority, authority), token);
            return (Command: command, Result: result, SaveMilliseconds: Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }));
        var elapsed = Stopwatch.GetElapsedTime(batchStarted).TotalMilliseconds;
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocations;
        var observedCommands = fixture.Probe.Commands.ToArray();
        var accepted = attempts.Where(attempt => attempt.Result is ProjectTaskPlanOutcome.Accepted).ToArray();
        var replay = new List<double>();
        foreach (var attempt in accepted) {
            var started = Stopwatch.GetTimestamp();
            var repeated = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher()
                .PublishAsync(attempt.Command, fixture.Invocation, token));
            replay.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.True(repeated.Replayed);
            Assert.Equal(((ProjectTaskPlanOutcome.Accepted)attempt.Result).Acknowledgement.Tasks, repeated.Acknowledgement.Tasks);
        }
        var after = await StorageAsync(fixture, token);
        Write(new { Kind = "sample", Scenario = scenario, Sample = sample, ExistingCandidates = existing, AppendedTasks = appended,
            Writers = projects.Length, Attempts = attempts.Length, Accepted = accepted.Length,
            Outcomes = attempts.GroupBy(attempt => Outcome(attempt.Result)).ToDictionary(group => group.Key, group => group.Count()),
            OpeningMilliseconds = openingMilliseconds, EndToEndBatchMilliseconds = elapsed,
            ProcessAllocatedBytes = allocated, ProcessAllocatedBytesPerAttempt = allocated / attempts.Length,
            Save = Percentiles(attempts.Select(attempt => attempt.SaveMilliseconds)), Replay = Percentiles(replay),
            ObservedEfCommands = observedCommands.Length,
            ObservedReads = observedCommands.Count(sql => sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal)),
            ObservedWriteCommands = observedCommands.Count(sql => sql.Contains("INSERT INTO", StringComparison.Ordinal) ||
                sql.Contains("UPDATE ", StringComparison.Ordinal) || sql.Contains("DELETE FROM", StringComparison.Ordinal)),
            ReceiptPayloadBytes = after.PayloadBytes - before.PayloadBytes,
            ClusterWalBytes = after.WalBytes - before.WalBytes, Storage = after });
        var uncertain = attempts.Count(attempt => attempt.Result is ProjectTaskPlanOutcome.Unconfirmed);
        Assert.InRange(after.ReceiptRows - before.ReceiptRows, accepted.Length, accepted.Length + uncertain);
        Assert.Equal((after.ReceiptRows - before.ReceiptRows) * appended, after.TaskRows - before.TaskRows);
        if (scenario == "same-project") {
            Assert.InRange(after.ReceiptRows - before.ReceiptRows, 0, 1);
        }
    }

    private static string Outcome(ProjectTaskPlanOutcome result) => result switch {
        ProjectTaskPlanOutcome.Accepted => "accepted",
        ProjectTaskPlanOutcome.Rejected rejected => "rejected:" + rejected.Reason,
        ProjectTaskPlanOutcome.Unconfirmed unknown => "unconfirmed:" + unknown.Reason,
        _ => throw new InvalidOperationException()
    };

    private static object Percentiles(IEnumerable<double> values) {
        var sorted = values.Order().ToArray();
        return new { Count = sorted.Length, P50 = Value(0.5), P95 = Value(0.95), P99 = Value(0.99) };
        double Value(double percentile) => sorted.Length == 0 ? 0 : sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
    }

    private static async Task<ProjectWriteAdmission> NewProjectAsync(ProjectTaskPlanOperationFixture fixture, CancellationToken token) {
        await using var owner = await fixture.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(token);
        var project = new Project { Name = "Synthetic task-plan measurement", Slug = Guid.NewGuid().ToString("N") };
        owner.Add(project);
        await owner.SaveChangesAsync(token);
        return (await fixture.Services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(project.Id, token))!;
    }

    private static async Task SeedAsync(ProjectTaskPlanOperationFixture fixture, ProjectWriteAdmission project, int count, CancellationToken token) {
        while (count > 0) {
            var batch = Math.Min(count, 100);
            var command = await fixture.CommandAsync(batch, token, project);
            Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation, token));
            count -= batch;
        }
    }

    private static async Task<StorageObservation> StorageAsync(ProjectTaskPlanOperationFixture fixture, CancellationToken token) {
        await using var context = await fixture.ContextAsync(token);
        return await context.Database.SqlQuery<StorageObservation>($"""
            SELECT (SELECT count(*) FROM "Workbench_TaskPlanReceipts") AS "ReceiptRows",
                (SELECT count(*) FROM "Workbench_ProjectObjects") AS "TaskRows",
                (SELECT coalesce(sum(octet_length("AcceptedPayload")), 0)::bigint FROM "Workbench_TaskPlanReceipts") AS "PayloadBytes",
                pg_total_relation_size('"Workbench_TaskPlanReceipts"') AS "ReceiptTotalBytes",
                pg_table_size('"Workbench_TaskPlanReceipts"') AS "ReceiptTableAndToastBytes",
                pg_indexes_size('"Workbench_TaskPlanReceipts"') AS "ReceiptIndexBytes",
                pg_wal_lsn_diff(pg_current_wal_insert_lsn(), '0/0')::bigint AS "WalBytes"
            """).SingleAsync(token);
    }

    private void Write<T>(T value) => output.WriteLine(JsonSerializer.Serialize(value));
    private sealed class StorageObservation {
        public long ReceiptRows { get; set; }
        public long TaskRows { get; set; }
        public long PayloadBytes { get; set; }
        public long ReceiptTotalBytes { get; set; }
        public long ReceiptTableAndToastBytes { get; set; }
        public long ReceiptIndexBytes { get; set; }
        public long WalBytes { get; set; }
    }
}
