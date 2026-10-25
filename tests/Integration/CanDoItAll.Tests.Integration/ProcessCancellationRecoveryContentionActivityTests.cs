using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessCancellationRecoveryContentionActivityTests(ITestOutputHelper output) {
    [Fact]
    public async Task Recorded_real_recovery_steps_preserve_the_contended_completion_contract() {
        var current = new AsyncLocal<StepMeasurement?>();
        var steps = new ConcurrentQueue<StepMeasurement>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => current.Value is null ? ActivitySamplingResult.None : ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => current.Value is null ? ActivitySamplingResult.None : ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (current.Value is { } step && activity.OperationName.StartsWith("workspace.", StringComparison.Ordinal)) {
                    step.Activities.AddOrUpdate(activity.OperationName, new Measurement(1, activity.Duration.TotalMilliseconds),
                        (_, prior) => new(prior.Count + 1, prior.Milliseconds + activity.Duration.TotalMilliseconds));
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        try {
            await new ProcessCancellationRecoveryContentionTests(output).RunAsync(ReadBasisMode.RunSnapshot, 32, services => {
                services.RemoveAll<IProcessCancellationRecoveryStep>();
                services.AddScoped<IProcessCancellationRecoveryStep>(provider => new RecordedStep(
                    ActivatorUtilities.CreateInstance<ProcessCancellationRecoveryDispatcher>(provider), current, steps));
            });
        } finally {
            output.WriteLine(JsonSerializer.Serialize(new {
                recoverySteps = steps.Select(step => new {
                    step.Root, step.Stage, step.Revision, step.ElapsedMilliseconds, step.Cancelled, step.Failure,
                    activities = step.Activities.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new { operation = pair.Key, count = pair.Value.Count, inclusiveMilliseconds = pair.Value.Milliseconds })
                }),
                scope = "Diagnostic listener wraps the real registered dispatcher; nested inclusive durations are not additive; no capacity claim."
            }));
        }
    }

    private sealed record Measurement(int Count, double Milliseconds);

    private sealed class StepMeasurement(ProcessCancellationRecoveryClaim claim) {
        internal ProcessRunId Root { get; } = claim.RootRunId;
        internal ProcessCancellationRecoveryStage Stage { get; } = claim.Position.Stage;
        internal long Revision { get; } = claim.ProgressRevision;
        internal double ElapsedMilliseconds { get; set; }
        internal bool Cancelled { get; set; }
        internal string? Failure { get; set; }
        internal ConcurrentDictionary<string, Measurement> Activities { get; } = new(StringComparer.Ordinal);
    }

    private sealed class RecordedStep(IProcessCancellationRecoveryStep inner, AsyncLocal<StepMeasurement?> current,
        ConcurrentQueue<StepMeasurement> steps) : IProcessCancellationRecoveryStep {
        public async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
            var step = new StepMeasurement(claim);
            var started = Stopwatch.GetTimestamp();
            current.Value = step;
            try {
                return await inner.AdvanceAsync(claim, cancellationToken);
            } catch (Exception failure) {
                step.Failure = failure.GetType().FullName;
                throw;
            } finally {
                step.ElapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                step.Cancelled = cancellationToken.IsCancellationRequested;
                current.Value = null;
                steps.Enqueue(step);
            }
        }
    }
}
