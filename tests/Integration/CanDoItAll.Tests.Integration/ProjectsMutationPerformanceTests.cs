using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

[Trait("Category", "LongRunning")]
public sealed class ProjectsMutationPerformanceTests(ITestOutputHelper output) {
    private const int WarmupOperations = 20;
    private const int MeasuredOperations = 100;
    private const int Samples = 3;
    private const int MaximumWriters = 32;
    private const int DiagnosticProcessStripeCount = 64;

    [Fact]
    public async Task Measure_real_project_editor_reads_and_saves() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var cancellationToken = deadline.Token;
        var commands = new CommandProbe();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(commands), ServiceLifetime.Singleton);
                services.ConfigureDbContext<SearchDbContext>(options => options.AddInterceptors(commands), ServiceLifetime.Singleton);
            }
        });
        await WriteEnvironmentAsync(application, cancellationToken);

        foreach (var children in new[] { 0, 5, 100, 1000 }) {
            var projectId = await CreateProjectAsync(application, children, cancellationToken);
            await MeasureScenarioAsync(application, commands, Workload.Uncontended, [projectId], children, cancellationToken);
        }

        var disjointProjects = new Guid[MaximumWriters];
        for (var index = 0; index < disjointProjects.Length; index++) {
            disjointProjects[index] = await CreateProjectAsync(application, 5, cancellationToken);
        }
        foreach (var writers in new[] { 8, MaximumWriters }) {
            await MeasureScenarioAsync(application, commands, Workload.DisjointProjects, disjointProjects[..writers], 5, cancellationToken);
            await MeasureScenarioAsync(application, commands, Workload.SameProject,
                Enumerable.Repeat(disjointProjects[0], writers).ToArray(), 5, cancellationToken);
        }
    }

    private async Task WriteEnvironmentAsync(TestApplication application, CancellationToken cancellationToken) {
        await using var context = await application.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>()
            .CreateDbContextAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        Write(new {
            Kind = "environment",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            ServerGc = System.Runtime.GCSettings.IsServerGC,
            PostgreSql = context.Database.GetDbConnection().ServerVersion,
            ProjectsAssembly = typeof(ProjectsService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            HarnessAssembly = typeof(ProjectsMutationPerformanceTests).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            ActivityStream = application.Services.GetRequiredService<IActivityStream>().GetType().Name,
            WarmupOperations,
            MeasuredOperations,
            Samples,
            MeasurementDeadlineMinutes = 10,
            UnavailableMetrics = new[] { "rows-read-or-written", "wire-payload", "transaction-duration", "connection-wait",
                "pure-lock-wait", "lock-hold", "WAL", "server-CPU", "cross-process-contention" }
        });
    }

    private async Task MeasureScenarioAsync(TestApplication application, CommandProbe commands, Workload workload,
        Guid[] projectIds, int children, CancellationToken cancellationToken) {
        var example = await ReadAsync(application, projectIds[0], cancellationToken);
        var examplePayloadBytes = JsonSerializer.SerializeToUtf8Bytes(example.Model).Length;
        for (var sample = 1; sample <= Samples; sample++) {
            await RunWavesAsync(application, projectIds, WarmupOperations, $"warmup-{workload}-{sample}", cancellationToken);
            commands.Reset();
            var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            var collectionsBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            var started = Stopwatch.GetTimestamp();
            var measurements = await RunWavesAsync(application, projectIds, MeasuredOperations,
                $"measure-{workload}-{sample}", cancellationToken);
            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
            var collections = Enumerable.Range(0, 3).Select(generation => GC.CollectionCount(generation) - collectionsBefore[generation]).ToArray();
            var observedCommands = commands.Snapshot();
            Write(new {
                Kind = "sample", Workload = workload.ToString(), Sample = sample, Writers = projectIds.Length,
                DiagnosticProcessStripeCount,
                DiagnosticStripeOccupancy = projectIds.Distinct().GroupBy(id =>
                    (ProjectMutationScopeKeys.ForProject(id).GetHashCode(StringComparison.Ordinal) & int.MaxValue) % DiagnosticProcessStripeCount)
                    .Select(group => group.Count()).OrderDescending().ToArray(),
                StoredPhases = children, StoredOptions = children, EditorOptions = example.Model.Options.Count,
                ExampleEditorJsonBytes = examplePayloadBytes, Operations = measurements.Length,
                Accepted = measurements.Count(item => item.Accepted),
                Rejected = measurements.Count(item => !item.Accepted && item.ExceptionType is null),
                Faulted = measurements.Count(item => item.ExceptionType is not null),
                Outcomes = measurements.GroupBy(item => item.Outcome).ToDictionary(group => group.Key, group => group.Count()),
                ElapsedMilliseconds = elapsed.TotalMilliseconds,
                AttemptsPerSecond = measurements.Length / elapsed.TotalSeconds,
                AcceptedPerSecond = measurements.Count(item => item.Accepted) / elapsed.TotalSeconds,
                ReadMilliseconds = Distribution(measurements.Select(item => item.ReadMilliseconds)),
                SaveMilliseconds = Distribution(measurements.Select(item => item.SaveMilliseconds)),
                AcceptedSaveMilliseconds = Distribution(measurements.Where(item => item.Accepted).Select(item => item.SaveMilliseconds)),
                RejectedSaveMilliseconds = Distribution(measurements.Where(item => !item.Accepted && item.ExceptionType is null).Select(item => item.SaveMilliseconds)),
                FaultedSaveMilliseconds = Distribution(measurements.Where(item => item.ExceptionType is not null).Select(item => item.SaveMilliseconds)),
                AllocatedProcessBytes = allocatedBytes, GarbageCollections = collections,
                ObservedCommands = observedCommands
            });
            Assert.Contains(observedCommands, item => item.Owner == nameof(ProjectsDbContext) && item.Executions > 0);
            Assert.Contains(observedCommands, item => item.Owner == nameof(SearchDbContext) && item.Executions > 0);
            Assert.Contains(measurements, item => item.Accepted);
            if (workload == Workload.Uncontended) {
                Assert.All(measurements, item => Assert.True(item.Accepted, item.Outcome));
            }
        }
    }

    private static async Task<Measurement[]> RunWavesAsync(TestApplication application, Guid[] projectIds,
        int operations, string label, CancellationToken cancellationToken) {
        var measurements = new List<Measurement>(operations);
        for (var offset = 0; offset < operations; offset += projectIds.Length) {
            var count = Math.Min(projectIds.Length, operations - offset);
            var reads = await Task.WhenAll(projectIds.Take(count).Select(id => ReadAsync(application, id, cancellationToken)));
            var saves = reads.Select((read, writer) => SaveAsync(application, read, $"{label}-{offset + writer}", cancellationToken));
            measurements.AddRange(await Task.WhenAll(saves));
        }
        return measurements.ToArray();
    }

    private static async Task<ReadMeasurement> ReadAsync(TestApplication application, Guid projectId,
        CancellationToken cancellationToken) {
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var started = Stopwatch.GetTimestamp();
        var model = await service.GetAsync(projectId, cancellationToken);
        return new(model, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private static async Task<Measurement> SaveAsync(TestApplication application, ReadMeasurement read, string label,
        CancellationToken cancellationToken) {
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        read.Model.Objective = label;
        if (read.Model.Phases.Count > 0) {
            read.Model.Phases[0].Goal = label;
        }
        var option = read.Model.Options.FirstOrDefault(item => item.Id.HasValue);
        if (option is not null) {
            option.Notes = label;
        }
        var started = Stopwatch.GetTimestamp();
        try {
            var result = await service.SaveEditorAsync(read.Model, cancellationToken);
            return new(read.Milliseconds, Stopwatch.GetElapsedTime(started).TotalMilliseconds, result.IsSuccess,
                result.IsSuccess ? "accepted" : string.Join(",", result.Errors.Select(error => error.Code).Distinct().Order()), null);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            var cause = exception.GetBaseException();
            var outcome = cause is PostgresException postgres ? $"{cause.GetType().Name}:{postgres.SqlState}" : cause.GetType().Name;
            return new(read.Milliseconds, Stopwatch.GetElapsedTime(started).TotalMilliseconds, false, outcome, cause.GetType().Name);
        }
    }

    private static async Task<Guid> CreateProjectAsync(TestApplication application, int children, CancellationToken cancellationToken) {
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var categories = Enum.GetValues<ProjectOptionCategory>();
        var editor = new ProjectEditorModel { Name = $"Performance project with {children} children", Objective = "Baseline" };
        for (var index = 0; index < children; index++) {
            editor.Phases.Add(new() { Name = $"Phase {index}", Goal = "Baseline phase" });
            editor.Options.Add(new() { Category = categories[index % categories.Length], OptionName = $"Option {index}", Notes = "Baseline option" });
        }
        var saved = await service.SaveAsync(editor, cancellationToken);
        Assert.True(saved.IsSuccess);
        return saved.Value;
    }

    private static object? Distribution(IEnumerable<double> values) {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) {
            return null;
        }
        double At(double percentile) => sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1)];
        return new { P50 = At(0.50), P95 = At(0.95), P99 = At(0.99), Maximum = sorted[^1] };
    }

    private void Write(object value) => output.WriteLine(JsonSerializer.Serialize(value));

    private enum Workload { Uncontended, DisjointProjects, SameProject }

    private sealed record ReadMeasurement(ProjectEditorModel Model, double Milliseconds);

    private sealed record Measurement(double ReadMilliseconds, double SaveMilliseconds, bool Accepted, string Outcome, string? ExceptionType);

    private sealed record CommandMeasurement(string Owner, double Milliseconds, bool Advisory, bool Failed);

    private sealed record CommandSummary(string Owner, int Executions, int Failed, double SummedMilliseconds,
        int AdvisoryExecutions, double AdvisorySummedMilliseconds);

    private sealed class CommandProbe : DbCommandInterceptor {
        private readonly ConcurrentQueue<CommandMeasurement> measurements = new();

        public void Reset() => measurements.Clear();

        public CommandSummary[] Snapshot() => measurements.GroupBy(item => item.Owner).OrderBy(group => group.Key)
            .Select(group => new CommandSummary(group.Key, group.Count(), group.Count(item => item.Failed), group.Sum(item => item.Milliseconds),
                group.Count(item => item.Advisory), group.Where(item => item.Advisory).Sum(item => item.Milliseconds))).ToArray();

        private void Record(DbCommand command, CommandEndEventData eventData, bool failed = false) {
            measurements.Enqueue(new(eventData.Context?.GetType().Name ?? "unknown", eventData.Duration.TotalMilliseconds,
                command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal), failed));
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            Record(command, eventData);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            object? result, CancellationToken cancellationToken = default) {
            Record(command, eventData);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            Record(command, eventData);
            return ValueTask.FromResult(result);
        }

        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData,
            CancellationToken cancellationToken = default) {
            Record(command, eventData, failed: true);
            return Task.CompletedTask;
        }

        public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData,
            CancellationToken cancellationToken = default) {
            Record(command, eventData, failed: true);
            return Task.CompletedTask;
        }
    }
}
