using System.Collections.Concurrent;
using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Integration;

internal sealed class AgentPlanningPublicationTrace : IDisposable {
    internal const string OperationKey = "planning-cost-operation";
    private const int MaximumSamples = 4096;
    private readonly Activity parent = new Activity("planning-cost-cohort").Start();
    private readonly ConcurrentQueue<Sample> samples = new();
    private readonly ActivityListener listener;
    private int count;

    internal AgentPlanningPublicationTrace() {
        listener = new ActivityListener {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.TraceId == parent.TraceId && activity.GetBaggageItem(OperationKey) is { } operation &&
                    Interlocked.Increment(ref count) <= MaximumSamples) {
                    samples.Enqueue(new(operation, activity.OperationName, activity.StartTimeUtc, activity.Duration.TotalMilliseconds));
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
    }

    internal object Snapshot() => new {
        Samples = samples.OrderBy(sample => sample.StartedAtUtc).ToArray(),
        DroppedSamples = Math.Max(0, Volatile.Read(ref count) - MaximumSamples),
        Scope = "Nested existing owner activities; durations overlap and must not be added as total elapsed time"
    };

    public void Dispose() {
        listener.Dispose();
        parent.Dispose();
    }

    private sealed record Sample(string Operation, string Activity, DateTime StartedAtUtc, double Milliseconds);
}
