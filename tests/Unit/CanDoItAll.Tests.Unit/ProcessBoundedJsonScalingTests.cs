using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "LongRunning")]
public sealed class ProcessBoundedJsonScalingTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1, 256)]
    [InlineData(128, 256)]
    [InlineData(1024, 256)]
    [InlineData(128, 1_048_576)]
    public void Matched_full_preparation_serializers_preserve_bytes_and_report_their_cost(int steps, int neighboringPromptCharacters) {
        var preparation = ProcessPreparedLaunchScalingFixture.Create(steps, 256);
        var assignments = preparation.InitialCommit.InitialAssignments!.ToArray();
        assignments[0] = assignments[0] with { Prompt = new('x', neighboringPromptCharacters) };
        preparation = preparation with { InitialCommit = preparation.InitialCommit with { InitialAssignments = assignments } };
        var options = ProcessInstancePlanPersistenceMapper.CreateSerializerOptions();
        var expected = Serialize(false);
        Assert.Equal(expected, Serialize(true));
        var bytes = Encoding.UTF8.GetByteCount(expected);
        Assert.InRange(bytes, 1, ProcessPreparedLaunchCodec.MaximumGovernedPayloadUtf8Bytes);
        var coldPool = ArrayPool<byte>.Create(ProcessPreparedLaunchCodec.MaximumGovernedPayloadUtf8Bytes, 1);
        var coldAllocated = GC.GetAllocatedBytesForCurrentThread();
        var coldStarted = Stopwatch.GetTimestamp();
        var cold = ProcessBoundedJson.Serialize(preparation, options, ProcessPreparedLaunchCodec.MaximumGovernedPayloadUtf8Bytes, coldPool);
        var coldElapsed = Stopwatch.GetElapsedTime(coldStarted);
        var coldAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - coldAllocated;
        Assert.Equal(expected, cold);
        output.WriteLine(JsonSerializer.Serialize(new {
            Workload = "full-preparation-cold-buffer", Steps = steps, NeighborPromptCharacters = neighboringPromptCharacters,
            Utf8Bytes = bytes, ElapsedMilliseconds = coldElapsed.TotalMilliseconds, ThreadAllocatedBytes = coldAllocatedBytes
        }));
        for (var round = 0; round < 6; round++) {
            Measure(round % 2 == 0, round);
            Measure(round % 2 != 0, round);
        }

        string Serialize(bool bounded) => bounded
            ? ProcessBoundedJson.Serialize(preparation, options, ProcessPreparedLaunchCodec.MaximumGovernedPayloadUtf8Bytes)
            : JsonSerializer.Serialize(preparation, options);

        void Measure(bool bounded, int round) {
            const int repetitions = 3;
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var characters = 0L;
            for (var iteration = 0; iteration < repetitions; iteration++) {
                characters += Serialize(bounded).Length;
            }
            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert.Equal((long)expected.Length * repetitions, characters);
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "full-preparation-serialization", Steps = steps, NeighborPromptCharacters = neighboringPromptCharacters,
                Bounded = bounded, Round = round, Repetitions = repetitions, Utf8Bytes = bytes,
                ElapsedMilliseconds = elapsed.TotalMilliseconds, ThreadAllocatedBytes = allocatedBytes
            }));
        }
    }
}
