using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.ProcessRecovery;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try {
    if (args.Length != 3 || !Enum.TryParse<RecoveryHostMode>(args[0], false, out var mode) || !Enum.IsDefined(mode) ||
            args[0] != mode.ToString() || !Guid.TryParseExact(args[1], "D", out var profileId) || profileId == Guid.Empty ||
            !Guid.TryParseExact(args[2], "D", out var rootId) || rootId == Guid.Empty) {
        throw new ArgumentException("The recovery child requires a supported mode, profile and root.");
    }
    var environmentRoot = Environment.GetEnvironmentVariable(RecoveryHostProtocol.EnvironmentRoot);
    if (string.IsNullOrWhiteSpace(environmentRoot) || !Path.IsPathFullyQualified(environmentRoot) || !Directory.Exists(environmentRoot)) {
        throw new InvalidOperationException("The recovery child requires its existing owned environment root.");
    }
    for (DirectoryInfo? directory = new(environmentRoot); directory is not null; directory = directory.Parent) {
        if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
            throw new InvalidOperationException("The recovery child root must not traverse a reparse point.");
        }
    }
    var section = ProcessCancellationRecoveryOptions.ConfigurationSectionName;
    var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?> {
        [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = string.Empty,
        ["LaneKind"] = string.Empty,
        [$"{section}:{nameof(ProcessCancellationRecoveryOptions.Enabled)}"] = "true",
        [$"{section}:{nameof(ProcessCancellationRecoveryOptions.IdleDelay)}"] = "00:00:00.025",
        [$"{section}:{nameof(ProcessCancellationRecoveryOptions.Limits)}:MaximumDuration"] = "00:00:01",
        [$"{section}:{nameof(ProcessCancellationRecoveryOptions.Limits)}:LeaseDuration"] = "00:00:03",
        ["AgentFramework:Providers:SeedDefaults"] = "false",
        ["Workflows:ExampleSeed:Enabled"] = "false"
    }).Build();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    using var self = Process.GetCurrentProcess();
    var probe = new RecoveryProbe(mode, new(rootId), new(self.StartTime.ToUniversalTime()), deadline.Token);
    var services = new ServiceCollection();
    TestApplicationBootstrap.ConfigureDefaultServices(services, configuration,
        new TestHostEnvironment(environmentRoot, "CanDoItAll.ProcessRecovery.TestHost"));
    services.AddRouting();
    services.AddCanDoItAllApi(configuration);
    services.AddLogging(logging => logging.ClearProviders());
    foreach (var registration in services.Where(item => item.ServiceType == typeof(IHostedService) &&
            item.ImplementationType != typeof(ProcessCancellationRecoveryWorker)).ToArray()) {
        services.Remove(registration);
    }
    services.AddScoped<ProcessCancellationRecoveryDispatcher>();
    services.AddScoped<EfProcessCancellationRecoveryQueue>();
    services.RemoveAll<IProcessCancellationRecoveryStep>();
    services.AddScoped<IProcessCancellationRecoveryStep>(scope => new HeldRecoveryStep(
        scope.GetRequiredService<ProcessCancellationRecoveryDispatcher>(), probe));
    services.RemoveAll<IProcessCancellationRecoveryQueue>();
    services.AddScoped<IProcessCancellationRecoveryQueue>(scope => new ObservedRecoveryQueue(
        scope.GetRequiredService<EfProcessCancellationRecoveryQueue>(), probe));
    await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    await using (var scope = provider.CreateAsyncScope()) {
        var canonical = scope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        if (canonical.Profile.Id != profileId || canonical.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw new InvalidOperationException("The recovery child resolved a different owned database profile.");
        }
    }
    var worker = provider.GetServices<IHostedService>().OfType<ProcessCancellationRecoveryWorker>().Single();
    await worker.StartAsync(deadline.Token);
    try {
        await probe.Completed.Task.WaitAsync(deadline.Token);
    } finally {
        await worker.StopAsync(default);
    }
    return 0;
} catch (Exception failure) {
    await Console.Error.WriteLineAsync(JsonSerializer.Serialize(new {
        FailureType = failure.GetType().FullName, CauseType = failure.GetBaseException().GetType().FullName,
        Frames = new StackTrace(failure.GetBaseException(), true).GetFrames().Take(8)
            .Select(frame => new { Type = frame.GetMethod()?.DeclaringType?.FullName, Method = frame.GetMethod()?.Name, Line = frame.GetFileLineNumber() })
    }));
    return 2;
}

internal sealed class RecoveryProbe(RecoveryHostMode mode, ProcessRunId root, DateTimeOffset startedUtc, CancellationToken lifetime) {
    internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal RecoveryHostMode Mode => mode;
    internal bool IsSelected(ProcessCancellationRecoveryClaim claim) => claim.RootRunId == root && claim.Intent is ProcessCancellationAccountChild;

    internal async Task WriteAsync(RecoveryHostFrameKind kind, ProcessCancellationRecoveryClaim claim) {
        var frame = new RecoveryHostFrame(kind, claim.RootRunId.Value, claim.Intent?.Context.CommandId.Value,
            claim.LeaseGeneration, claim.LeaseToken, claim.ProgressRevision, Environment.ProcessId, startedUtc);
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(frame));
        await Console.Out.FlushAsync(lifetime);
    }

    internal async Task HoldAsync(ProcessCancellationRecoveryClaim claim) {
        await WriteAsync(RecoveryHostFrameKind.Held, claim);
        await Task.Delay(Timeout.InfiniteTimeSpan, lifetime);
    }
}

internal sealed class HeldRecoveryStep(IProcessCancellationRecoveryStep inner, RecoveryProbe probe) : IProcessCancellationRecoveryStep {
    public async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
        if (probe.IsSelected(claim) && probe.Mode == RecoveryHostMode.BeforeAccounting) {
            await probe.HoldAsync(claim);
        }
        var result = await inner.AdvanceAsync(claim, cancellationToken);
        if (probe.IsSelected(claim)) {
            if (result.Resolution?.Outcome != ProcessCancellationIntentOutcome.Committed) {
                throw new InvalidDataException("The selected original accounting action did not commit or replay.");
            }
            if (probe.Mode == RecoveryHostMode.AfterAccounting) {
                await probe.HoldAsync(claim);
            } else if (probe.Mode == RecoveryHostMode.Resume) {
                await probe.WriteAsync(RecoveryHostFrameKind.Replayed, claim);
            }
        }
        return result;
    }
}

internal sealed class ObservedRecoveryQueue(IProcessCancellationRecoveryQueue inner, RecoveryProbe probe) : IProcessCancellationRecoveryQueue {
    public Task<ProcessCancellationRecoveryClaim?> ClaimAsync(ProcessCancellationRecoveryOrigin origin, TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) => inner.ClaimAsync(origin, leaseDuration, cancellationToken);

    public async Task<ProcessCancellationCheckpointResult> CheckpointAsync(ProcessCancellationRecoveryClaim claim,
        ProcessCancellationRecoveryCheckpoint checkpoint, CancellationToken cancellationToken = default) {
        var result = await inner.CheckpointAsync(claim, checkpoint, cancellationToken);
        if (checkpoint.Position.Stage == ProcessCancellationRecoveryStage.Completed && result.Status == ProcessCancellationCheckpointStatus.Released) {
            await probe.WriteAsync(RecoveryHostFrameKind.Completed, claim);
            probe.Completed.TrySetResult();
        }
        return result;
    }

    public Task<ProcessCancellationSeedResult> SeedLegacyAsync(int take = 16, CancellationToken cancellationToken = default)
        => inner.SeedLegacyAsync(take, cancellationToken);
}
