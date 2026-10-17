using System.Data.Common;
using System.Diagnostics;
using System.Threading.Channels;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

internal enum CapacityAttemptState { Unoffered, Queued, QueueRejected, Started, Accepted, Replayed, Rejected, Unconfirmed, Fault }
internal sealed record CapacityLatency(int Count, double P50, double P95, double P99, double Maximum);
internal sealed record CapacityAttempt(int Index, CapacityAttemptState State, bool Large, double QueueMilliseconds,
    double ReadMilliseconds, double OwnerMilliseconds, double EndToEndMilliseconds, string? Detail);
internal sealed record CapacityWindow(int Offered, int Enqueued, int QueueRejected, int Started, int Accepted,
    int Replayed, int Rejected, int Unconfirmed, int Faults, int MaximumQueueDepth, double ElapsedMilliseconds,
    double ProcessCpuMilliseconds, long ProcessAllocatedBytes, int[] GarbageCollections,
    CapacityLatency? QueueLatency, CapacityLatency? EndToEndLatency, CapacityLatency? OwnerLatency,
    CapacityMinute[] Minutes, IReadOnlyDictionary<string, int> Details);
internal sealed record CapacityMinute(int Minute, int Offered, int Accepted, int QueueRejected,
    CapacityLatency? QueueLatency, CapacityLatency? EndToEndLatency);
internal sealed record CapacityTemplate(ProjectDefinitionRequest<ReplaceProjectDefinitionCommand> Request,
    ProjectDefinitionAccepted<ProjectEditorAcknowledgement> Accepted, byte[] CanonicalPayload);

internal sealed class CapacityArrivalAccounting(int count) {
    private readonly int[] states = new int[count is > 0 and <= 54_000 ? count : throw new ArgumentOutOfRangeException(nameof(count))];
    private readonly CapacityAttempt?[] results = new CapacityAttempt?[count];
    internal void Offer(int index) => Move(index, CapacityAttemptState.Unoffered, CapacityAttemptState.Queued);
    internal void QueueRejected(int index, bool large) {
        Move(index, CapacityAttemptState.Queued, CapacityAttemptState.QueueRejected);
        results[index] = new(index, CapacityAttemptState.QueueRejected, large, 0, 0, 0, 0, null);
    }
    internal void Start(int index) => Move(index, CapacityAttemptState.Queued, CapacityAttemptState.Started);
    internal void Complete(CapacityAttempt result) {
        if (result.State is not (CapacityAttemptState.Accepted or CapacityAttemptState.Replayed or CapacityAttemptState.Rejected or
            CapacityAttemptState.Unconfirmed or CapacityAttemptState.Fault)) {
            throw new ArgumentException("A completed arrival requires an explicit terminal observation.");
        }
        Move(result.Index, CapacityAttemptState.Started, result.State);
        results[result.Index] = result;
    }
    internal CapacityAttempt[] CompleteSnapshot() {
        if (results.Any(result => result is null)) {
            throw new InvalidOperationException("The offered-arrival accounting contains unsettled or missing identities.");
        }
        return results.Select(result => result!).ToArray();
    }
    private void Move(int index, CapacityAttemptState previous, CapacityAttemptState next) {
        if ((uint)index >= states.Length || Interlocked.CompareExchange(ref states[index], (int)next, (int)previous) != (int)previous) {
            throw new InvalidOperationException("An arrival was counted twice or changed its observed outcome.");
        }
    }
}

internal sealed class ProjectsMaintenanceCapacityLoad(TestApplication application, bool mixed) {
    private const int Executors = 8;
    private const int QueueCapacity = 256;
    internal static readonly ProjectDefinitionInvocation Invocation = new(ProjectMutationCallerScope.LocalWorkspace, new AllowedReceiptRead());
    private readonly Guid[,] targets = new Guid[Executors, 2];
    private readonly ProjectDefinitionWriteContext[] contexts = new ProjectDefinitionWriteContext[Executors];
    private int revisionLabel;
    internal CapacityTemplate Small { get; private set; } = null!;
    internal CapacityTemplate Large { get; private set; } = null!;
    internal Guid Namespace => contexts[0].Namespace.Value;

    internal async Task InitializeAsync(CancellationToken token) {
        for (var executor = 0; executor < Executors; executor++) {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            contexts[executor] = await services.GetRequiredService<ProjectDefinitionOperationProtocol>().GetWriteContextAsync(Invocation.Caller, token);
            for (var shape = 0; shape < 2; shape++) {
                var children = shape == 0 ? 5 : 1000;
                var operation = Operation(contexts[executor]);
                var created = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(
                    await services.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(
                        new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(operation, ProjectDefinitionRequestVersion.VersionOne, Creation(children)), Invocation, token));
                targets[executor, shape] = created.Result.Acknowledgement.Project.ProjectId;
                if (executor == 0) {
                    var template = await PrepareTemplateAsync(services, targets[executor, shape], contexts[executor], token);
                    if (shape == 0) {
                        Small = template;
                    } else {
                        Large = template;
                    }
                }
            }
        }
    }

    internal async Task<CapacityWindow> RunAsync(int seconds, CancellationToken token) {
        if (seconds is not (30 or 120 or 360)) {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        var rate = mixed ? 50 : 150;
        var count = rate * seconds;
        var accounting = new CapacityArrivalAccounting(count);
        var queue = Channel.CreateBounded<(int Index, bool Large, long Due)>(new BoundedChannelOptions(QueueCapacity) {
            SingleWriter = true, FullMode = BoundedChannelFullMode.Wait
        });
        var started = Stopwatch.GetTimestamp();
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var maximumQueue = 0;
        var consumers = Enumerable.Range(0, Executors).Select(async executor => {
            await foreach (var item in queue.Reader.ReadAllAsync(token)) {
                accounting.Start(item.Index);
                var dequeued = Stopwatch.GetTimestamp();
                accounting.Complete(await ExecuteAsync(executor, item.Index, item.Large, item.Due, dequeued, token));
            }
        }).ToArray();
        Exception? failure = null;
        try {
            for (var index = 0; index < count; index++) {
                token.ThrowIfCancellationRequested();
                var due = started + (long)((double)index * Stopwatch.Frequency / rate);
                var wait = TimeSpan.FromSeconds((double)(due - Stopwatch.GetTimestamp()) / Stopwatch.Frequency);
                if (wait > TimeSpan.Zero) {
                    await Task.Delay(wait, token);
                }
                var large = mixed && index % 20 == 19;
                accounting.Offer(index);
                if (!queue.Writer.TryWrite((index, large, due))) {
                    accounting.QueueRejected(index, large);
                }
                maximumQueue = Math.Max(maximumQueue, queue.Reader.Count);
            }
        } catch (Exception exception) {
            failure = exception;
        } finally {
            queue.Writer.TryComplete();
            try {
                await Task.WhenAll(consumers);
            } catch (Exception cleanup) {
                failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
            }
        }
        if (failure is not null) {
            throw failure;
        }
        var attempts = accounting.CompleteSnapshot();
        var byState = attempts.GroupBy(attempt => attempt.State).ToDictionary(group => group.Key, group => group.Count());
        int Count(CapacityAttemptState state) => byState.GetValueOrDefault(state);
        var queueRejected = Count(CapacityAttemptState.QueueRejected);
        var completed = attempts.Where(attempt => attempt.State != CapacityAttemptState.QueueRejected).ToArray();
        return new(count, count - queueRejected, queueRejected, completed.Length, Count(CapacityAttemptState.Accepted),
            Count(CapacityAttemptState.Replayed), Count(CapacityAttemptState.Rejected), Count(CapacityAttemptState.Unconfirmed),
            Count(CapacityAttemptState.Fault), maximumQueue, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            (process.TotalProcessorTime - cpu).TotalMilliseconds, GC.GetTotalAllocatedBytes(precise: true) - allocated,
            Enumerable.Range(0, 3).Select(index => GC.CollectionCount(index) - collections[index]).ToArray(),
            Distribution(completed.Select(item => item.QueueMilliseconds)), Distribution(completed.Select(item => item.EndToEndMilliseconds)),
            Distribution(completed.Select(item => item.OwnerMilliseconds)),
            attempts.GroupBy(attempt => attempt.Index / (rate * 60)).Select(group => new CapacityMinute(group.Key,
                group.Count(), group.Count(item => item.State == CapacityAttemptState.Accepted), group.Count(item => item.State == CapacityAttemptState.QueueRejected),
                Distribution(group.Where(item => item.State != CapacityAttemptState.QueueRejected).Select(item => item.QueueMilliseconds)),
                Distribution(group.Where(item => item.State != CapacityAttemptState.QueueRejected).Select(item => item.EndToEndMilliseconds)))).ToArray(),
            attempts.Where(item => item.Detail is not null).GroupBy(item => item.Detail!).ToDictionary(group => group.Key, group => group.Count()));
    }

    private async Task<CapacityAttempt> ExecuteAsync(int executor, int index, bool large, long due, long dequeued, CancellationToken token) {
        var readMilliseconds = 0d;
        var ownerMilliseconds = 0d;
        CapacityAttemptState state;
        string? detail = null;
        try {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var read = Stopwatch.GetTimestamp();
            var original = await services.GetRequiredService<ProjectDefinitionReader>().ReadAsync(targets[executor, large ? 1 : 0], token);
            if (!original.IsSuccess) {
                throw new InvalidOperationException("The owned foreground target could not be read.");
            }
            readMilliseconds = Stopwatch.GetElapsedTime(read).TotalMilliseconds;
            var label = Interlocked.Increment(ref revisionLabel);
            var request = new ProjectDefinitionRequest<ReplaceProjectDefinitionCommand>(Operation(contexts[executor]),
                ProjectDefinitionRequestVersion.VersionOne, Replacement(original.Value!, label));
            var save = Stopwatch.GetTimestamp();
            var result = await services.GetRequiredService<ProjectDefinitionWriter>().ReplaceAsync(request, Invocation, token);
            ownerMilliseconds = Stopwatch.GetElapsedTime(save).TotalMilliseconds;
            state = result switch {
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted { Replayed: true } => CapacityAttemptState.Replayed,
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted => CapacityAttemptState.Accepted,
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected => CapacityAttemptState.Rejected,
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Unconfirmed => CapacityAttemptState.Unconfirmed,
                _ => throw new InvalidOperationException("The owner returned an unsupported outcome.")
            };
            detail = result switch {
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected rejected => string.Join(",", rejected.Errors.Select(error => error.Code).Distinct().Order()),
                ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Unconfirmed unconfirmed => unconfirmed.Reason.ToString(),
                _ => null
            };
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            state = CapacityAttemptState.Fault;
            detail = exception.GetBaseException() is PostgresException postgres ? postgres.SqlState : exception.GetBaseException().GetType().Name;
        }
        return new(index, state, large, TimeSpan.FromSeconds((double)(dequeued - due) / Stopwatch.Frequency).TotalMilliseconds,
            readMilliseconds, ownerMilliseconds, Stopwatch.GetElapsedTime(due).TotalMilliseconds, detail);
    }

    internal async Task VerifyOriginalReplayAsync(CancellationToken token) {
        foreach (var template in new[] { Small, Large }) {
            await using var scope = application.Services.CreateAsyncScope();
            var replay = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(
                await scope.ServiceProvider.GetRequiredService<ProjectDefinitionWriter>().ReplaceAsync(template.Request, Invocation, token));
            Assert.True(replay.Replayed);
            Assert.Equal(template.CanonicalPayload, ProjectDefinitionOperationCodec.EncodeAccepted(replay.Result, ProjectDefinitionLimits.VersionOneMaximum).ToArray());
        }
    }

    private static async Task<CapacityTemplate> PrepareTemplateAsync(IServiceProvider services, Guid target,
        ProjectDefinitionWriteContext context, CancellationToken token) {
        var original = await services.GetRequiredService<ProjectDefinitionReader>().ReadAsync(target, token);
        Assert.True(original.IsSuccess);
        var request = new ProjectDefinitionRequest<ReplaceProjectDefinitionCommand>(Operation(context),
            ProjectDefinitionRequestVersion.VersionOne, Replacement(original.Value!, 0));
        var accepted = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(
            await services.GetRequiredService<ProjectDefinitionWriter>().ReplaceAsync(request, Invocation, token));
        return new(request, accepted.Result, ProjectDefinitionOperationCodec.EncodeAccepted(accepted.Result, ProjectDefinitionLimits.VersionOneMaximum).ToArray());
    }

    private static ProjectDefinitionOperation Operation(ProjectDefinitionWriteContext context)
        => new(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding);

    private static CreateProjectDefinitionCommand Creation(int children) {
        var categories = Enum.GetValues<ProjectOptionCategory>();
        return new(new($"Capacity project {children}", "", "capacity-0000000000", default, "", null),
            Enumerable.Range(0, children).Select(index => new ProjectDefinitionPhaseInput(null, $"Phase {index}", "Baseline phase", default, null, null)).ToArray(),
            Enumerable.Range(0, children).Select(index => new ProjectDefinitionOptionInput(null, categories[index % categories.Length], $"Option {index}", "Baseline option")).ToArray(), null);
    }

    private static ReplaceProjectDefinitionCommand Replacement(ProjectDefinitionSnapshot original, int index)
        => new(original.Expectation, original.Scalars with { Objective = $"capacity-{index:D10}" },
            original.Phases.Select(row => new ProjectDefinitionPhaseInput(row.Id, row.Name, row.Goal, row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(),
            original.Options.Select(row => new ProjectDefinitionOptionInput(row.Id, row.Category, row.OptionName, row.Notes)).ToArray(), null);

    internal static CapacityLatency? Distribution(IEnumerable<double> values) {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) {
            return null;
        }
        double At(double percentile) => sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1)];
        return new(sorted.Length, At(.5), At(.95), At(.99), sorted[^1]);
    }

    private sealed class AllowedReceiptRead : IProjectDefinitionReceiptAccess {
        public Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}

internal enum CapacityCommandFamily { Advisory, Search, Receipt, Epoch, Children, Root, Other }
internal sealed record CapacityCommandSummary(CapacityCommandFamily Family, long Count, long Failures, double Milliseconds);
internal sealed class CapacityCommandProbe : DbCommandInterceptor {
    private readonly object gate = new();
    private readonly long[] count = new long[7];
    private readonly long[] failures = new long[7];
    private readonly double[] milliseconds = new double[7];

    internal CapacityCommandSummary[] Drain() {
        lock (gate) {
            var result = Enum.GetValues<CapacityCommandFamily>().Select(family => new CapacityCommandSummary(family,
                count[(int)family], failures[(int)family], milliseconds[(int)family])).ToArray();
            Array.Clear(count);
            Array.Clear(failures);
            Array.Clear(milliseconds);
            return result;
        }
    }

    private void Record(DbCommand command, CommandEndEventData data, bool failure = false) {
        var sql = command.CommandText;
        var family = sql.Contains("pg_advisory", StringComparison.Ordinal) ? CapacityCommandFamily.Advisory
            : sql.Contains("Infrastructure_SearchDocuments", StringComparison.Ordinal) ? CapacityCommandFamily.Search
            : sql.Contains("Projects_DefinitionReceipts", StringComparison.Ordinal) ? CapacityCommandFamily.Receipt
            : sql.Contains("Infrastructure_FiniteOperation", StringComparison.Ordinal) ? CapacityCommandFamily.Epoch
            : sql.Contains("Projects_ProjectPhases", StringComparison.Ordinal) || sql.Contains("Projects_ProjectOptionSelections", StringComparison.Ordinal) ? CapacityCommandFamily.Children
            : sql.Contains("Projects_Projects", StringComparison.Ordinal) ? CapacityCommandFamily.Root : CapacityCommandFamily.Other;
        lock (gate) {
            count[(int)family]++;
            failures[(int)family] += failure ? 1 : 0;
            milliseconds[(int)family] += data.Duration.TotalMilliseconds;
        }
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
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default) {
        Record(command, eventData, true);
        return Task.CompletedTask;
    }
    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default) {
        Record(command, eventData, true);
        return Task.CompletedTask;
    }
}
