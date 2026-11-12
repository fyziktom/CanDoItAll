using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspaceProcessIndexActivityCostTests(ITestOutputHelper output) {
    [Fact]
    public async Task Small_history_cost_activity_sample_preserves_each_accepted_revision() {
        var samples = new ConcurrentDictionary<(string Operation, string Payload), Measurement>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (!activity.OperationName.StartsWith("workspace.", StringComparison.Ordinal)) {
                    return;
                }
                var key = (activity.OperationName, activity.GetTagItem("payload.type") as string ?? string.Empty);
                samples.AddOrUpdate(key, new Measurement(1, activity.Duration.TotalMilliseconds),
                    (_, previous) => new(previous.Count + 1, previous.Milliseconds + activity.Duration.TotalMilliseconds));
            }
        };
        ActivitySource.AddActivityListener(listener);
        await new FileSandboxWorkspaceProcessIndexCostTests(output)
            .Actual_owner_writes_and_concurrent_pages_preserve_each_accepted_revision(0, 1);
        Assert.Contains(samples, sample => sample.Key.Operation == "workspace.json.write" && sample.Value.Count > 0);
        output.WriteLine(JsonSerializer.Serialize(new {
            activityMeasurements = samples.OrderBy(sample => sample.Key.Operation, StringComparer.Ordinal)
                .ThenBy(sample => sample.Key.Payload, StringComparer.Ordinal)
                .Select(sample => new { operation = sample.Key.Operation, payload = sample.Key.Payload,
                    count = sample.Value.Count, inclusiveMilliseconds = sample.Value.Milliseconds }),
            scope = "Diagnostic listener includes fixture, warmup and measured workload; nested inclusive durations are not additive. Listener overhead is not a capacity sample."
        }));
    }

    private sealed record Measurement(long Count, double Milliseconds);
}
