using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExecutionOccurrenceTests {
    [Fact]
    public void PersistedEnvelopeRetainsIdentityWhileUserPayloadCannotSetIt() {
        var occurrence = WorkflowExecutionOccurrence.Start(WorkflowRunId.New()).Advance(WorkflowVersionId.New(), new("effect"));
        var input = new WorkflowNodeInput("{\"runContext\":{\"runId\":\"untrusted\"},\"ExecutionOccurrence\":{\"Path\":\"forged\"}}") {
            ExecutionOccurrence = occurrence
        };
        var restored = JsonSerializer.Deserialize<WorkflowNodeInput>(JsonSerializer.Serialize(input));
        Assert.Equal(occurrence, restored!.ExecutionOccurrence);
        Assert.Equal(input.PayloadJson, restored.PayloadJson);
        Assert.Null(new WorkflowNodeInput(input.PayloadJson).ExecutionOccurrence);
    }

    [Fact]
    public void LegacyEnvelopeRemainsReadableWithoutInventingAnEffectIdentity() {
        var input = JsonSerializer.Deserialize<WorkflowNodeInput>("{\"PayloadJson\":\"{}\"}");
        Assert.NotNull(input);
        Assert.Equal("{}", input.PayloadJson);
        Assert.Null(input.ExecutionOccurrence);
        Assert.DoesNotContain("ExecutionOccurrence", JsonSerializer.Serialize(input), StringComparison.Ordinal);
    }

    [Fact]
    public void VersionNodeAndRunAllBindOccurrenceIdentity() {
        var run = WorkflowRunId.New();
        var version = WorkflowVersionId.New();
        var start = WorkflowExecutionOccurrence.Start(run);
        var occurrence = start.Advance(version, new("effect"));
        Assert.Equal(occurrence, start.Advance(version, new("effect")));
        Assert.NotEqual(occurrence, start.Advance(WorkflowVersionId.New(), new("effect")));
        Assert.NotEqual(occurrence, start.Advance(version, new("other")));
        Assert.NotEqual(occurrence, WorkflowExecutionOccurrence.Start(WorkflowRunId.New()).Advance(version, new("effect")));
        Assert.Throws<ArgumentException>(() => start.Advance(default, new("effect")));
        Assert.Throws<ArgumentException>(() => start.Advance(version, default));
    }
}
