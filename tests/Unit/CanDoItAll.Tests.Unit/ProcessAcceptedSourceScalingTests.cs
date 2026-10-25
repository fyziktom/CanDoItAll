using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Persistence;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "LongRunning")]
public sealed class ProcessAcceptedSourceScalingTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1, 256)]
    [InlineData(64, 256)]
    [InlineData(256, 256)]
    [InlineData(1024, 256)]
    [InlineData(1, 1_048_576)]
    public void Selected_source_read_reports_cost_independent_of_unrelated_steps_and_prompts(int steps, int promptCharacters) {
        var saved = ProcessAcceptedSourceCodecTests.Saved(steps, promptCharacters);
        var captureAllocated = GC.GetAllocatedBytesForCurrentThread();
        var captureStarted = Stopwatch.GetTimestamp();
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var captureMilliseconds = Stopwatch.GetElapsedTime(captureStarted).TotalMilliseconds;
        var captureBytes = GC.GetAllocatedBytesForCurrentThread() - captureAllocated;
        var selected = saved.Preparation.InitialCommit.InitialAssignments![^1].StepInstanceId;
        var assignment = projection.Assignments[selected];
        var payloadBytes = Encoding.UTF8.GetByteCount(projection.Header.PayloadJson) + Encoding.UTF8.GetByteCount(assignment.PayloadJson);
        ProcessAcceptedSource Read() => ProcessAcceptedSourceCodec.Read(ProcessAcceptedSourceVersion.OriginalProjection,
            saved.Preparation.AdmissionId, selected, saved.PreparationFingerprint, projection.Header, assignment);
        for (var warmup = 0; warmup < 5; warmup++) {
            ProcessAcceptedSourceCodecTests.AssertOriginal(saved, selected, Read());
        }
        var repetitions = steps >= 256 || promptCharacters >= 1_048_576 ? 8 : 32;
        for (var round = 0; round < 3; round++) {
            ProcessAcceptedSource? last = null;
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var iteration = 0; iteration < repetitions; iteration++) {
                last = Read();
            }
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            ProcessAcceptedSourceCodecTests.AssertOriginal(saved, selected, Assert.IsType<ProcessAcceptedSource>(last));
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "accepted-source-projection-read", Steps = steps, PromptCharacters = promptCharacters,
                PayloadUtf8Bytes = payloadBytes, Round = round, Repetitions = repetitions, ElapsedMilliseconds = elapsed,
                AllocatedBytesPerRead = bytes / repetitions, CaptureElapsedMilliseconds = captureMilliseconds,
                CaptureAllocatedBytes = captureBytes
            }));
        }
    }
}
