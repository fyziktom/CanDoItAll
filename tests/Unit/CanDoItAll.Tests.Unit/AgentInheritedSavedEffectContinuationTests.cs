using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentInheritedSavedEffectContinuationTests {
    [Theory]
    [InlineData(Substitution.Run)]
    [InlineData(Substitution.RunRevision)]
    [InlineData(Substitution.JournalRevision)]
    [InlineData(Substitution.Decision)]
    [InlineData(Substitution.Generation)]
    [InlineData(Substitution.Batch)]
    [InlineData(Substitution.Intent)]
    [InlineData(Substitution.Version)]
    [InlineData(Substitution.Digest)]
    public void Review_capability_refuses_a_different_displayed_selection(Substitution changed) {
        var (run, expected, binding) = Create();
        var checks = 0;
        var capability = AgentInheritedSavedEffectContinuation.ForManagerReview(run.ToolAdmission!.PlanningDecision!.InheritedOrigin!,
            expected, binding, (_, _) => {
                checks++;
                return Task.CompletedTask;
            });
        var alternateExpected = new AgentPlanningDecisionExpectation(
            changed == Substitution.Run ? Guid.NewGuid() : expected.ExecutionRunId,
            expected.RunRevision + (changed == Substitution.RunRevision ? 1 : 0),
            expected.JournalRevision + (changed == Substitution.JournalRevision ? 1 : 0),
            changed == Substitution.Decision ? Guid.NewGuid() : expected.DecisionId,
            expected.Generation + (changed == Substitution.Generation ? 1 : 0));
        var alternateBinding = changed switch {
            Substitution.Batch => binding with { BatchId = new(Guid.NewGuid()) },
            Substitution.Intent => binding with { IntentId = new(Guid.NewGuid()) },
            Substitution.Version => binding with { SemanticVersion = binding.SemanticVersion + 1 },
            Substitution.Digest => binding with { Digest = AgentPlanningDecisionTestData.Envelope("{\"changed\":true}").Digest },
            _ => binding
        };
        Assert.Equal(AgentInheritedContinuationPurpose.ManagerReview, capability.Purpose);
        capability.RequireSelection(expected, binding);
        Assert.Throws<AgentToolAdmissionException>(() => capability.RequireSelection(alternateExpected, alternateBinding));
        Assert.Equal(0, checks);
    }

    [Fact]
    public async Task Review_rechecks_original_identity_without_rejecting_its_own_revision_advancement() {
        var (run, expected, binding) = Create();
        var checks = 0;
        var capability = AgentInheritedSavedEffectContinuation.ForManagerReview(run.ToolAdmission!.PlanningDecision!.InheritedOrigin!,
            expected, binding, (_, token) => {
                token.ThrowIfCancellationRequested();
                checks++;
                return Task.CompletedTask;
            });
        var advanced = run with { Revision = run.Revision + 2, ToolAdmission = run.ToolAdmission with { Revision = run.ToolAdmission.Revision + 2 } };
        await capability.RequireCurrentAsync(advanced, default);
        Assert.Equal(1, checks);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => capability.RequireCurrentAsync(advanced with { Id = Guid.NewGuid() }, default));
        Assert.Equal(1, checks);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => capability.RequireCurrentAsync(advanced, cancelled.Token));
        Assert.Equal(1, checks);
    }

    [Fact]
    public async Task Existing_constructor_keeps_dispatch_semantics_and_requires_current_parent() {
        var (run, _, _) = Create();
        var failure = new AgentToolReceiptAccessDeniedException();
        AgentInheritedSavedEffectContinuation capability = new(run.ToolAdmission!.PlanningDecision!.InheritedOrigin!, (_, _) => throw failure);
        Assert.Equal(AgentInheritedContinuationPurpose.Dispatch, capability.Purpose);
        Assert.Same(failure, await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => capability.RequireCurrentAsync(run, default)));
    }

    private static (ExecutionRunRecord Run, AgentPlanningDecisionExpectation Expected, AgentToolApprovalBinding Binding) Create() {
        var journal = AgentInheritedPlanningTestData.Create();
        var session = journal.Session;
        var source = session.Reference.BackgroundSource!;
        var metadata = AgentPlanningRunMetadata.ApplyInherited("{}", AgentPlanningOwner.ProjectTaskPlan,
            AgentPlanningDecisionTestData.Envelope().Digest, WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N")));
        ExecutionRunRecord run = new(
            Id: session.Reference.ExecutionRunId, AgentId: session.AgentId, ChatSessionId: null, Title: "Review",
            SourceKind: source.SourceKind, SourceId: source.SourceId, CorrelationId: "", CausationId: "",
            RequestedBy: "", RequestedByKind: "", MetadataJson: metadata, InputSummary: "", ResultSummary: "",
            ProviderName: "", Model: "", State: ExecutionState.Preparing, Outcome: null,
            CreatedAtUtc: AgentInheritedPlanningTestData.Start, UpdatedAtUtc: AgentInheritedPlanningTestData.Start,
            StartedAtUtc: null, CompletedAtUtc: null, RuntimeSessionKey: "", SerializedSessionStateJson: null, PendingApprovals: []) {
            ToolAdmission = journal
        };
        return (run, AgentPlanningDecisionExpectation.From(run),
            new(new(Guid.NewGuid()), new(Guid.NewGuid()), 1, AgentPlanningDecisionTestData.Envelope().Digest));
    }

    public enum Substitution { Run, RunRevision, JournalRevision, Decision, Generation, Batch, Intent, Version, Digest }
}
