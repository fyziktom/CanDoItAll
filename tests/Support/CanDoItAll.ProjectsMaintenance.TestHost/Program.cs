using CanDoItAll.Composition;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Data.Common;
using System.Threading.Channels;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Maintenance;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

return await MaintenanceChild.RunAsync(args);

internal static class MaintenanceChild {
    internal static async Task<int> RunAsync(string[] arguments) {
        try {
            var values = MaintenanceHostProtocol.ParseArguments(arguments);
            var profileId = Guid.ParseExact(values["--profile-id"], "D");
            var runId = Guid.ParseExact(values["--run-id"], "D");
            var ordinal = MaintenanceHostProtocol.Integer(values, "--ordinal");
            var cadence = MaintenanceHostProtocol.Integer(values, "--interval-seconds");
            var warning = MaintenanceHostProtocol.Integer(values, "--warning-seconds");
            var lifetime = MaintenanceHostProtocol.Integer(values, "--lifetime-seconds");
            if (profileId == Guid.Empty || runId == Guid.Empty || ordinal is < 1 or > 2 ||
                cadence is not (5 or 60) || warning != 120 || lifetime is < 1 or > 1800) {
                throw new ArgumentException("The maintenance child arguments exceed the approved workload.");
            }
            var root = Environment.GetEnvironmentVariable(MaintenanceHostProtocol.RootEnvironment)
                ?? throw new InvalidOperationException("The owned maintenance root was not supplied.");
            if (!Path.IsPathFullyQualified(root) || !Directory.Exists(root)) {
                throw new InvalidOperationException("The owned maintenance root is unavailable.");
            }
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(lifetime));
            await using var transport = new MaintenanceOutput(runId, ordinal, deadline);
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            TestApplicationBootstrap.ConfigureDefaultServices(services, configuration,
                new TestHostEnvironment(root, "CanDoItAll.ProjectsMaintenance.TestHost"));
            var backends = new MaintenanceBackendObserver(transport);
            services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(backends), ServiceLifetime.Singleton);
            services.Configure<ProjectDefinitionOperationOptions>(options => {
                options.MaintenanceInterval = TimeSpan.FromSeconds(cadence);
                options.MaintenanceWarningAfter = TimeSpan.FromSeconds(warning);
            });
            services.AddLogging(logging => logging.ClearProviders().AddProvider(new MaintenanceLoggerProvider(transport)));
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            var profile = provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile;
            if (profile.Id != profileId || profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
                throw new InvalidOperationException("The maintenance child resolved a different profile.");
            }
            _ = provider.GetRequiredService<IOptions<ProjectDefinitionOperationOptions>>().Value.GetLimits();
            var binding = await provider.GetRequiredService<DatasetActivationService>().ReadActiveBindingAsync(profileId, deadline.Token);
            var worker = provider.GetServices<IHostedService>().OfType<FiniteOperationMaintenance>().Single();
            using var observer = new MaintenanceMetricObserver(transport);
            transport.Emit(MaintenanceFrameKind.Ready, profile: profileId, @namespace: binding.Namespace.Value, maintenance: binding.MaintenanceId);
            var start = await MaintenanceHostProtocol.ReadAsync(Console.In, deadline.Token);
            RequireControl(start, MaintenanceFrameKind.Start, 1, runId, ordinal);
            transport.Emit(MaintenanceFrameKind.Start);
            await worker.StartAsync(deadline.Token);
            Exception? failure = null;
            using var control = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var readStop = MaintenanceHostProtocol.ReadAsync(Console.In, control.Token);
            try {
                var execution = worker.ExecuteTask ?? throw new InvalidOperationException("The maintenance worker did not start.");
                await Task.WhenAny(readStop, execution);
                if (!readStop.IsCompleted) {
                    await execution;
                    throw new InvalidOperationException("The maintenance worker ended before its explicit stop.");
                }
                var stop = await readStop;
                if (stop is not null) {
                    RequireControl(stop, MaintenanceFrameKind.Stop, 2, runId, ordinal);
                }
            } catch (Exception exception) {
                failure = exception;
            } finally {
                control.Cancel();
                try {
                    await readStop;
                } catch (OperationCanceledException) when (control.IsCancellationRequested) {
                } catch (Exception controlFailure) {
                    failure ??= controlFailure;
                }
                using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try {
                    await worker.StopAsync(stopping.Token);
                    if (worker.ExecuteTask is { IsFaulted: true } execution) {
                        throw new InvalidOperationException("The maintenance worker faulted.", execution.Exception);
                    }
                    observer.Flush();
                } catch (Exception cleanup) {
                    failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
                }
            }
            if (failure is not null) {
                throw failure;
            }
            transport.ThrowIfFailed();
            transport.Emit(MaintenanceFrameKind.Stopped, backendProcesses: backends.Snapshot());
            return 0;
        } catch (Exception exception) {
            await Console.Error.WriteLineAsync(exception.GetType().Name);
            return 2;
        }
    }

    private static void RequireControl(MaintenanceHostFrame? frame, MaintenanceFrameKind kind, long sequence,
        Guid runId, int worker) {
        if (frame is not null) {
            MaintenanceHostProtocol.ValidateControl(frame);
        }
        if (frame is null || frame.Kind != kind || frame.Sequence != sequence || frame.RunId != runId || frame.Worker != worker) {
            throw new InvalidDataException("The maintenance control frame does not match its owned process.");
        }
    }
}

internal sealed class MaintenanceOutput(Guid run, int worker, CancellationTokenSource deadline) : IAsyncDisposable {
    private readonly Channel<string> output = Channel.CreateBounded<string>(new BoundedChannelOptions(MaintenanceHostProtocol.MaximumQueuedFrames) {
        SingleReader = true, FullMode = BoundedChannelFullMode.Wait
    });
    private readonly object gate = new();
    private Task? pump;
    private Exception? failure;
    private long sequence;

    internal void Emit(MaintenanceFrameKind kind, MaintenanceCycleObservation? cycle = null,
        MaintenanceHealth? health = null, string? failureType = null, string? sqlState = null,
        Guid? profile = null, Guid? @namespace = null, Guid? maintenance = null, int[]? backendProcesses = null) {
        lock (gate) {
            pump ??= PumpAsync();
            if (failure is not null) {
                return;
            }
            try {
                using var process = Process.GetCurrentProcess();
                var frame = new MaintenanceHostFrame(MaintenanceHostProtocol.Version, run, worker, ++sequence,
                    kind, DateTimeOffset.UtcNow, profile, @namespace, maintenance, cycle, health, failureType,
                    sqlState, process.TotalProcessorTime.TotalMilliseconds, backendProcesses);
                MaintenanceHostProtocol.ValidateObservation(frame);
                if (!output.Writer.TryWrite(MaintenanceHostProtocol.Encode(frame))) {
                    throw new InvalidDataException("The maintenance transport queue is full.");
                }
            } catch (Exception exception) {
                Fail(exception);
            }
        }
    }

    internal void Fail(Exception exception) {
        Interlocked.CompareExchange(ref failure, exception, null);
        deadline.Cancel();
    }

    internal void ThrowIfFailed() {
        if (failure is { } exception) {
            throw new InvalidDataException("The maintenance observation stream is incomplete.", exception);
        }
    }

    private async Task PumpAsync() {
        try {
            await foreach (var line in output.Reader.ReadAllAsync(deadline.Token)) {
                await Console.Out.WriteLineAsync(line.AsMemory(), deadline.Token);
                await Console.Out.FlushAsync(deadline.Token);
            }
        } catch (Exception exception) {
            Fail(exception);
        }
    }

    public async ValueTask DisposeAsync() {
        output.Writer.TryComplete();
        if (pump is not null) {
            await pump;
        }
        ThrowIfFailed();
    }
}

internal sealed class MaintenanceBackendObserver(MaintenanceOutput output) : DbConnectionInterceptor {
    private readonly object gate = new();
    private readonly HashSet<int> processes = [];

    internal int[] Snapshot() {
        lock (gate) {
            return processes.Order().ToArray();
        }
    }

    private void Observe(DbConnection connection) {
        try {
            if (connection is not NpgsqlConnection postgres) {
                throw new InvalidOperationException("The maintenance worker opened a non-PostgreSQL connection.");
            }
            lock (gate) {
                if (!processes.Contains(postgres.ProcessID) && processes.Count >= MaintenanceHostProtocol.MaximumBackendProcesses) {
                    throw new InvalidDataException("The maintenance child exceeded its backend-evidence bound.");
                }
                processes.Add(postgres.ProcessID);
            }
        } catch (Exception exception) {
            output.Fail(exception);
        }
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Observe(connection);
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) {
        Observe(connection);
        return Task.CompletedTask;
    }
}

internal sealed class MaintenanceMetricObserver : IDisposable {
    private readonly MeterListener listener = new();
    private readonly MaintenanceMetricSequence sequence = new();
    private readonly MaintenanceOutput output;

    internal MaintenanceMetricObserver(MaintenanceOutput output) {
        this.output = output;
        listener.InstrumentPublished = (instrument, active) => {
            if (instrument.Meter.Name == MaintenanceHostProtocol.Meter) {
                if (instrument.Name is not (MaintenanceHostProtocol.Receipts or MaintenanceHostProtocol.Bytes or
                    MaintenanceHostProtocol.Cycles or MaintenanceHostProtocol.Duration or MaintenanceHostProtocol.Eligible or
                    MaintenanceHostProtocol.Age or MaintenanceHostProtocol.SinceProgress or MaintenanceHostProtocol.Lateness)) {
                    output.Fail(new InvalidDataException("An unknown maintenance instrument was published."));
                    return;
                }
                active.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => {
            try {
                RequireIdentity(tags, instrument.Name is MaintenanceHostProtocol.Receipts or MaintenanceHostProtocol.Bytes);
                switch (instrument.Name) {
                    case MaintenanceHostProtocol.Receipts:
                        Emit(sequence.Begin(value));
                        break;
                    case MaintenanceHostProtocol.Bytes:
                        sequence.SetBytes(value);
                        break;
                    default:
                        var (stop, eligibility) = ReadTags(tags);
                        sequence.SetCycle(value, stop, eligibility);
                        break;
                }
            } catch (Exception exception) {
                output.Fail(exception);
            }
        });
        listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => {
            try {
                RequireIdentity(tags, family: true);
                sequence.SetOptional(instrument.Name, value);
            } catch (Exception exception) {
                output.Fail(exception);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => {
            try {
                RequireIdentity(tags, instrument.Name == MaintenanceHostProtocol.Age);
            } catch (Exception exception) {
                output.Fail(exception);
                return;
            }
            if (instrument.Name == MaintenanceHostProtocol.Lateness) {
                if (!double.IsFinite(value) || value < 0) {
                    output.Fail(new InvalidDataException("Scheduled maintenance lateness is invalid."));
                }
                return;
            }
            if (instrument.Name == MaintenanceHostProtocol.Duration) {
                try {
                    var (stop, eligibility) = ReadTags(tags);
                    sequence.SetDuration(value, stop, eligibility);
                } catch (Exception exception) {
                    output.Fail(exception);
                }
            } else {
                Guard(() => sequence.SetOptional(instrument.Name, value));
            }
        });
        listener.Start();
    }

    private static (MaintenanceStop, MaintenanceEligibility) ReadTags(ReadOnlySpan<KeyValuePair<string, object?>> tags) {
        MaintenanceStop? stop = null;
        MaintenanceEligibility? eligibility = null;
        foreach (var tag in tags) {
            if (tag.Key == "pool") {
                continue;
            }
            if (tag.Key == "stop" && stop is null && tag.Value is string text && Enum.TryParse<MaintenanceStop>(text, out var parsed) && Enum.IsDefined(parsed)) {
                stop = parsed;
            } else if (tag.Key == "eligibility" && eligibility is null && tag.Value is string evidence &&
                Enum.TryParse<MaintenanceEligibility>(evidence, out var parsedEvidence) && Enum.IsDefined(parsedEvidence)) {
                eligibility = parsedEvidence;
            } else {
                throw new InvalidDataException("Maintenance metric tags are missing, duplicated or unsupported.");
            }
        }
        return (stop ?? throw new InvalidDataException("The stop metric tag is missing."),
            eligibility ?? throw new InvalidDataException("The eligibility metric tag is missing."));
    }

    private static void RequireIdentity(ReadOnlySpan<KeyValuePair<string, object?>> tags, bool family) {
        var pools = 0;
        var families = 0;
        foreach (var tag in tags) {
            if (tag.Key == "pool" && tag.Value is Guid pool && pool == ProjectFiniteOperationPool.Pool.Value) {
                pools++;
            } else if (tag.Key == "family" && tag.Value is Guid receiptFamily && receiptFamily == ProjectFiniteOperationPool.DefinitionFamily.Value) {
                families++;
            } else if (family || tag.Key is not ("stop" or "eligibility")) {
                throw new InvalidDataException("The capacity child observed a different pool or family.");
            }
        }
        if (pools != 1 || families != (family ? 1 : 0)) {
            throw new InvalidDataException("The capacity child requires exactly its installed Projects pool and definition family.");
        }
    }

    private void Guard(Action action) {
        try {
            action();
        } catch (Exception exception) {
            output.Fail(exception);
        }
    }

    private void Emit(MaintenanceCycleObservation? cycle) {
        if (cycle is not null) {
            output.Emit(MaintenanceFrameKind.CycleObservation, cycle);
        }
    }

    internal void Flush() => Emit(sequence.Complete());
    public void Dispose() => listener.Dispose();
}

internal sealed class MaintenanceLoggerProvider(MaintenanceOutput output) : ILoggerProvider {
    public ILogger CreateLogger(string categoryName) => new MaintenanceLogger(output,
        categoryName == typeof(FiniteOperationMaintenance).FullName);
    public void Dispose() {
    }

    private sealed class MaintenanceLogger(MaintenanceOutput output, bool selected) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => selected && logLevel >= LogLevel.Information;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) {
            if (!IsEnabled(logLevel) || state is not IEnumerable<KeyValuePair<string, object?>> values) {
                return;
            }
            var fields = values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            if (fields.TryGetValue("FailureType", out var failure)) {
                output.Emit(MaintenanceFrameKind.Failure, failureType: Safe(failure), sqlState: Safe(fields.GetValueOrDefault("SqlState")));
            } else if (fields.TryGetValue("MaintenanceHealth", out var health) &&
                Enum.TryParse<MaintenanceHealth>(health?.ToString(), out var parsed) && parsed != MaintenanceHealth.Recovery) {
                output.Emit(MaintenanceFrameKind.HealthTransition, health: parsed);
            } else if (logLevel == LogLevel.Information && fields.TryGetValue("{OriginalFormat}", out var format) &&
                format is string text && text.StartsWith("Finite operation maintenance pressure cleared", StringComparison.Ordinal)) {
                output.Emit(MaintenanceFrameKind.HealthTransition, health: MaintenanceHealth.Recovery);
            } else {
                output.Fail(new InvalidDataException("The maintenance logger emitted an unsupported observation."));
            }
        }

        private static string? Safe(object? value) {
            if (value is null) {
                return null;
            }
            var text = value.ToString();
            if (text is null || text.Length > 128 || text.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '.' or '+'))) {
                throw new InvalidDataException("The maintenance diagnostic is outside its allowlist.");
            }
            return text;
        }
    }
}
