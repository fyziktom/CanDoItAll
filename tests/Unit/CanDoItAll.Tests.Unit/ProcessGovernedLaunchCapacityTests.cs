using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "LongRunning")]
public sealed class ProcessGovernedLaunchCapacityTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Individually_valid_inputs_or_metadata_cannot_exceed_the_total_projection_limit(bool metadata) {
        var preparation = ProcessPreparedLaunchScalingFixture.Create(18, 256);
        var payload = new string('x', 1_000_000);
        var assignments = preparation.InitialCommit.InitialAssignments!.Select((item, index) => metadata
            ? index == 0 ? item : item with {
                DecisionDataPolicy = null, LaunchVariables = new Dictionary<string, string> { ["capacity-payload"] = payload }
            }
            : item with { Prompt = payload }).ToArray();
        preparation = preparation with { InitialCommit = preparation.InitialCommit with { InitialAssignments = assignments } };
        var saved = ProcessPreparedLaunchCodec.Read(ProcessPreparedLaunchCodec.ToEntity(preparation));
        var options = ProcessInstancePlanPersistenceMapper.CreateSerializerOptions();
        foreach (var assignment in assignments) {
            Assert.InRange(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(ProcessAssignmentSource.From(assignment), options)),
                1, ProcessAcceptedSourceCodec.MaximumPayloadUtf8Bytes);
            Assert.InRange(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(assignment, options)),
                1, ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes);
        }
        var failure = Assert.Throws<InvalidDataException>(() => ProcessAcceptedSourceCodec.Capture(saved));
        Assert.Equal("The accepted governed Process source exceeds its total UTF-8 byte limit.", failure.Message);
        Assert.Equal(preparation.AdmissionId, saved.Preparation.AdmissionId);
    }

    [Fact]
    public void Governed_preparation_is_bounded_while_ordinary_preparation_and_history_keep_existing_behavior() {
        var preparation = ProcessPreparedLaunchScalingFixture.Create(1, 256);
        var original = preparation.InitialCommit.InitialAssignments!.Single();
        var oversized = original with { Prompt = new('x', ProcessPreparedLaunchCodec.MaximumGovernedPayloadUtf8Bytes) };
        var governed = preparation with { InitialCommit = preparation.InitialCommit with { InitialAssignments = [oversized] } };
        Assert.Throws<InvalidDataException>(() => ProcessPreparedLaunchCodec.ToEntity(governed));
        var ordinary = governed with { InitialCommit = governed.InitialCommit with {
            InitialAssignments = [oversized with { DecisionDataPolicy = null }]
        } };
        var stored = ProcessPreparedLaunchCodec.ToEntity(ordinary);
        Assert.True(Encoding.UTF8.GetByteCount(stored.PayloadJson) > ProcessPreparedLaunchCodec.MaximumGovernedPayloadUtf8Bytes);
        var read = ProcessPreparedLaunchCodec.Read(stored);
        Assert.Equal(oversized.Prompt, read.Preparation.InitialCommit.InitialAssignments!.Single().Prompt);
        Assert.Empty(ProcessAcceptedSourceCodec.Capture(read).ExecutionInputs);
    }
}
