using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentChatSubmissionRetentionTests {
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Detached_view_does_not_cancel_submission_observation_or_retarget_its_failure(bool originalTarget, bool persisted) {
        var owner = new AgentChatSubmissionRetention();
        var agent = Guid.NewGuid();
        var session = Guid.NewGuid();
        Assert.True(owner.TryReserve(agent, session, null, new(7), "original draft", "original request", [], out var submission));
        var profile = Guid.NewGuid();
        owner.BindStream(submission!.Id, new(profile, WorkspaceScopeDescriptor.Organization(profile.ToString("N")), new(7), AgentExecutionOperationId.New()));
        var execution = new TaskCompletionSource<AgentChatRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ambient = new AsyncLocal<object?> { Value = new object() };
        object? observedAmbient = ambient.Value;
        owner.Changed += () => observedAmbient = ambient.Value;
        var observation = owner.ObserveAsync(submission.Id, execution.Task);
        using var view = new CancellationTokenSource();
        var viewWait = observation.WaitAsync(view.Token);
        view.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => viewWait);
        Assert.False(execution.Task.IsCompleted);
        Assert.False(observation.IsCompleted);
        Assert.Equal(AgentChatSubmissionState.Pending, Assert.Single(owner.Snapshot()).State);
        var failureAgent = originalTarget ? agent : Guid.NewGuid();
        Exception failure = persisted
            ? new AgentChatRunFailedException(failureAgent, Guid.NewGuid(), session, "provider", "model", new IOException("fixture"), "The run failed.")
            : new AgentChatAdmissionConflictException(failureAgent, session, new SandboxWorkspaceConcurrencyException(2, 3));
        execution.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => observation));
        Assert.Null(observedAmbient);
        if (originalTarget && persisted) {
            Assert.Empty(owner.Snapshot());
        } else {
            var retained = Assert.Single(owner.Snapshot());
            Assert.Equal(agent, retained.AgentId);
            Assert.Equal(session, retained.SessionId);
            Assert.Equal("original draft", retained.Draft);
            Assert.Equal(originalTarget ? AgentChatSubmissionState.NotSent : AgentChatSubmissionState.Unconfirmed, retained.State);
        }
    }

    [Fact]
    public void Planning_purpose_is_retained_with_original_submission_and_never_inferred_for_another_send() {
        var owner = new AgentChatSubmissionRetention();
        var agent = Guid.NewGuid();
        Assert.True(owner.TryReserve(agent, null, null, new(1), "plan", "plan", [], out var planning, AgentPlanningOwner.ProjectTaskPlan));
        owner.RetainFailure(planning!.Id, definitelyNotSent: true);
        var failed = Assert.Single(owner.Snapshot());
        Assert.Equal(AgentPlanningOwner.ProjectTaskPlan, failed.PlanningOwner);
        Assert.True(owner.TryTakeNotSent(failed, out var restored));
        Assert.Equal(AgentPlanningOwner.ProjectTaskPlan, restored!.PlanningOwner);
        Assert.True(owner.TryReserve(agent, null, null, new(1), "ordinary", "ordinary", [], out var ordinary));
        Assert.Null(ordinary!.PlanningOwner);
    }

    [Fact]
    public void Retention_freezes_editable_content_and_refuses_capacity_without_evicting_originals() {
        var owner = new AgentChatSubmissionRetention();
        var paths = new List<string> { "uploads/original.png" };
        var agent = Guid.NewGuid();
        var session = Guid.NewGuid();
        Assert.True(owner.TryReserve(agent, session, null, new(7), "draft", "composed request", paths, out var original));
        paths[0] = "uploads/changed.png";
        var profile = Guid.NewGuid();
        var stream = new AgentExecutionActivityStreamId(profile, WorkspaceScopeDescriptor.Organization(profile.ToString("N")), new(7), AgentExecutionOperationId.New());
        owner.BindStream(original!.Id, stream);
        owner.RetainFailure(original.Id, definitelyNotSent: false);
        var uncertain = Assert.Single(owner.Snapshot());
        Assert.Equal("uploads/original.png", Assert.Single(uncertain.AttachmentPaths));
        Assert.Equal(stream, uncertain.Stream);
        Assert.False(owner.TryTakeNotSent(uncertain, out _));
        Assert.False(owner.TryReserve(agent, session, null, new(7), "", new string('a', AgentChatSubmissionRetention.MaximumCharacters), [], out _));
        Assert.False(owner.TryReserve(agent, session, null, new(7), "", "request",
            Enumerable.Repeat("a", AgentChatSubmissionRetention.MaximumAttachmentReferences + 1).ToArray(), out _));
        Assert.Equal(uncertain, Assert.Single(owner.Snapshot()));
        owner.Discard(original);
        Assert.Single(owner.Snapshot());
        owner.Discard(uncertain);
        Assert.Empty(owner.Snapshot());
        Assert.True(owner.TryReserve(agent, session, null, new(7), "draft", "request", [], out var refused));
        owner.RetainFailure(refused!.Id, definitelyNotSent: true);
        var retained = Assert.Single(owner.Snapshot());
        Assert.True(owner.TryTakeNotSent(retained, out var restored));
        Assert.Equal("draft", restored!.Draft);
        Assert.Empty(owner.Snapshot());
    }

    [Fact]
    public void Admission_refusal_requires_direct_prepublication_knowledge_and_preserves_original_identity() {
        var conflict = new SandboxWorkspaceConcurrencyException(2, 3);
        var agent = Guid.NewGuid();
        var session = Guid.NewGuid();
        var refusal = new AgentChatAdmissionConflictException(agent, session, conflict);
        Assert.Equal(agent, refusal.AgentId);
        Assert.Equal(session, refusal.ChatSessionId);
        Assert.Same(conflict, refusal.InnerException);
        Assert.Throws<ArgumentException>(() => new AgentChatAdmissionConflictException(agent, session, new AggregateException(conflict)));
        Assert.Throws<ArgumentException>(() => new AgentChatAdmissionConflictException(agent, session, new IOException("storage")));
    }
}
