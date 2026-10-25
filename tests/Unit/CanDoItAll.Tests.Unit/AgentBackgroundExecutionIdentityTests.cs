using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentBackgroundExecutionIdentityTests {
    [Fact]
    public void Extracted_fingerprint_preserves_the_original_field_order_and_wire_bytes() {
        var run = new AgentInheritedParentStopFixture().Detail!.Run with {
            Id = new("00000000-0000-0000-0000-000000000001"), AgentId = new("00000000-0000-0000-0000-000000000002"),
            SourceKind = "source", SourceId = "step", CorrelationId = "correlation", CausationId = "cause",
            RequestedBy = "operator", RequestedByKind = "system", ProcessRunId = "process", ProcessStepId = "step",
            SchedulerRunId = string.Empty, MetadataJson = "{}", CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
        Assert.Equal("78e435c99566c8a9a188d254522c3a93f50996fac2a64a5111a8e0f275be4db3", AgentBackgroundExecutionIdentity.Fingerprint(run).Value);
        Assert.Equal(AgentBackgroundExecutionIdentity.Fingerprint(run), AgentBackgroundExecutionIdentity.Fingerprint(run with {
            ResultSummary = "Later result", State = ExecutionState.Failed, Revision = 7, CompletedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(5)
        }));
    }

    [Theory]
    [InlineData(ChangedField.Run)]
    [InlineData(ChangedField.Agent)]
    [InlineData(ChangedField.SourceKind)]
    [InlineData(ChangedField.SourceId)]
    [InlineData(ChangedField.Correlation)]
    [InlineData(ChangedField.Causation)]
    [InlineData(ChangedField.Requester)]
    [InlineData(ChangedField.RequesterKind)]
    [InlineData(ChangedField.Process)]
    [InlineData(ChangedField.Step)]
    [InlineData(ChangedField.Scheduler)]
    [InlineData(ChangedField.Metadata)]
    [InlineData(ChangedField.Created)]
    public void Every_original_background_field_remains_bound(ChangedField field) {
        var run = new AgentInheritedParentStopFixture().Detail!.Run;
        var changed = field switch {
            ChangedField.Run => run with { Id = Guid.NewGuid() },
            ChangedField.Agent => run with { AgentId = Guid.NewGuid() },
            ChangedField.SourceKind => run with { SourceKind = "another-source" },
            ChangedField.SourceId => run with { SourceId = "another-id" },
            ChangedField.Correlation => run with { CorrelationId = "another-correlation" },
            ChangedField.Causation => run with { CausationId = "another-cause" },
            ChangedField.Requester => run with { RequestedBy = "another-requester" },
            ChangedField.RequesterKind => run with { RequestedByKind = "another-kind" },
            ChangedField.Process => run with { ProcessRunId = Guid.NewGuid().ToString("D") },
            ChangedField.Step => run with { ProcessStepId = Guid.NewGuid().ToString("D") },
            ChangedField.Scheduler => run with { SchedulerRunId = Guid.NewGuid().ToString("D") },
            ChangedField.Metadata => run with { MetadataJson = "{}" },
            _ => run with { CreatedAtUtc = run.CreatedAtUtc.AddTicks(1) }
        };
        var session = run.ToolAdmission!.Session;
        Assert.Throws<AgentToolAdmissionException>(() => AgentBackgroundExecutionIdentity.Require(session, changed,
            session.Reference.BackgroundSource!, session.Profile));
    }

    [Fact]
    public void Dispatch_identity_still_rejects_another_current_profile_generation() {
        var run = new AgentInheritedParentStopFixture().Detail!.Run;
        var session = run.ToolAdmission!.Session;
        Assert.Throws<AgentToolAdmissionException>(() => AgentBackgroundExecutionIdentity.Require(session, run,
            session.Reference.BackgroundSource!, new(session.Profile.ProfileId, session.Profile.Fingerprint, new(session.Profile.Generation.Value + 1))));
    }

    public enum ChangedField { Run, Agent, SourceKind, SourceId, Correlation, Causation, Requester, RequesterKind, Process, Step, Scheduler, Metadata, Created }
}
