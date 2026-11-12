using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "LongRunning")]
public sealed class ProcessPreparedSourceScalingTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1, 256)]
    [InlineData(64, 256)]
    [InlineData(256, 256)]
    [InlineData(1024, 256)]
    [InlineData(1, 1_048_576)]
    public void Exact_original_assignment_read_reports_width_payload_and_allocation_cost(int steps, int promptCharacters) {
        var preparation = Create(steps, promptCharacters);
        var row = ProcessPreparedLaunchCodec.ToEntity(preparation);
        row.AcceptedAtUtc = preparation.PreparedAtUtc;
        row.Execute = true;
        row.State = ProcessLaunchContinuationState.Accepted;
        var expected = preparation.InitialCommit.InitialAssignments![^1];
        var payloadBytes = Encoding.UTF8.GetByteCount(row.PayloadJson);
        ProcessRuntimeStepAssignment Read()
            => ProcessPreparedLaunchCodec.Read(row).Preparation.InitialCommit.InitialAssignments!
                .Single(item => item.StepInstanceId == expected.StepInstanceId);
        for (var warmup = 0; warmup < 5; warmup++) {
            AssertOriginal(expected, Read());
        }
        var repetitions = steps >= 256 || promptCharacters >= 1_048_576 ? 8 : 32;
        for (var round = 0; round < 3; round++) {
            ProcessRuntimeStepAssignment? last = null;
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var iteration = 0; iteration < repetitions; iteration++) {
                last = Read();
            }
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            AssertOriginal(expected, Assert.IsType<ProcessRuntimeStepAssignment>(last));
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "accepted-source-full-read", Steps = steps, PromptCharacters = promptCharacters, PayloadUtf8Bytes = payloadBytes,
                Round = round, Repetitions = repetitions, ElapsedMilliseconds = elapsed, AllocatedBytesPerRead = bytes / repetitions
            }));
        }
    }

    private static void AssertOriginal(ProcessRuntimeStepAssignment expected, ProcessRuntimeStepAssignment actual) {
        Assert.Equal(expected.StepInstanceId, actual.StepInstanceId);
        Assert.Equal(expected.ExecutorId, actual.ExecutorId);
        Assert.Equal(expected.Prompt, actual.Prompt);
        Assert.Equal(ProcessDecisionDataPolicyAdmissionCodec.Serialize(expected.DecisionDataPolicy!),
            ProcessDecisionDataPolicyAdmissionCodec.Serialize(actual.DecisionDataPolicy!));
    }

    internal static ProcessPreparedLaunch Create(int count, int promptCharacters)
        => ProcessPreparedLaunchScalingFixture.Create(count, promptCharacters, ProcessPreparedLaunchFixture.Local(Guid.NewGuid()));
}
