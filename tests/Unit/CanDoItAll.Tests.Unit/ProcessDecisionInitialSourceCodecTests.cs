using System.Text.Json.Nodes;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionInitialSourceCodecTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Canonical_initial_evidence_retains_the_exact_child_claim_and_utc_creation_time(bool maximumKey) {
        var expected = Evidence() with { StepKey = maximumKey ? new string('\u4e2d', 256) : "original-step" };
        var json = ProcessDecisionInitialSourceCodec.Serialize(expected);
        var actual = ProcessDecisionInitialSourceCodec.Deserialize(json);
        Assert.Equal(expected, actual);
        Assert.Equal(json, ProcessDecisionInitialSourceCodec.Serialize(actual));
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) <= ProcessDecisionInitialSourceCodec.MaximumUtf8Bytes);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("executionRunId")]
    [InlineData("executorAgentId")]
    [InlineData("runId")]
    [InlineData("stepInstanceId")]
    [InlineData("dispatchClaimToken")]
    [InlineData("stepKey")]
    [InlineData("stepKey-too-long")]
    [InlineData("executionMayDispatch")]
    [InlineData("executionCreatedAtUtc")]
    [InlineData("offset")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("whitespace")]
    [InlineData("oversize")]
    public void Malformed_noncanonical_or_ineligible_evidence_is_refused(string change) {
        var original = ProcessDecisionInitialSourceCodec.Serialize(Evidence());
        var value = JsonNode.Parse(original)!.AsObject();
        switch (change) {
            case "version":
                value[change] = 2;
                break;
            case "executionRunId" or "executorAgentId" or "runId" or "stepInstanceId" or "dispatchClaimToken":
                value[change] = Guid.Empty;
                break;
            case "stepKey":
                value[change] = " ";
                break;
            case "stepKey-too-long":
                value["stepKey"] = new string('a', ProcessDecisionInitialSourceCodec.MaximumStepKeyCharacters + 1);
                break;
            case "executionMayDispatch":
                value[change] = false;
                break;
            case "executionCreatedAtUtc":
                value[change] = default(DateTimeOffset);
                break;
            case "offset":
                value["executionCreatedAtUtc"] = Evidence().ExecutionCreatedAtUtc.ToOffset(TimeSpan.FromHours(1));
                break;
            case "missing":
                value.Remove("dispatchClaimToken");
                break;
            case "unknown":
                value["unexpected"] = true;
                break;
        }
        var changed = change switch {
            "duplicate" => original.Insert(1, "\"version\":1,"),
            "whitespace" => " " + original,
            "oversize" => new string(' ', ProcessDecisionInitialSourceCodec.MaximumUtf8Bytes + 1),
            "null" => "null",
            _ => value.ToJsonString()
        };
        Assert.Throws<InvalidDataException>(() => ProcessDecisionInitialSourceCodec.Deserialize(changed));
    }

    [Fact]
    public void Typed_input_is_checked_before_it_can_be_sent_as_owner_evidence() {
        Assert.Throws<InvalidDataException>(() => ProcessDecisionInitialSourceCodec.Serialize(Evidence() with { ExecutionMayDispatch = false }));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionInitialSourceCodec.Serialize(Evidence() with { ExecutionRunId = Guid.Empty }));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionInitialSourceCodec.Serialize(Evidence() with {
            StepKey = new string('\u4e2d', ProcessDecisionInitialSourceCodec.MaximumUtf8Bytes)
        }));
    }

    private static ProcessExecutionClaimEvidence Evidence()
        => new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid()), new(Guid.NewGuid()), Guid.NewGuid(),
            "original-step", new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), true);
}
