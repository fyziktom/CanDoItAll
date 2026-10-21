using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class AgentInheritedPlanningReviewFixture(AgentInheritedPlanningFixture source, ExecutionRunRecord waiting)
    : IAsyncDisposable, IAgentContinuationRuntime, IAgentPlanningStepRuntime, IAgentInheritedPlanningParticipant,
        IAgentToolReceiptReconciliationProvider {
    private readonly CancellationTokenSource deadline = new(TimeSpan.FromSeconds(45));
    internal AgentInheritedPlanningFixture Source => source;
    internal ExecutionRunRecord Waiting => waiting;
    internal CancellationToken Token => deadline.Token;
    internal AgentToolSemanticDigest OwnerIntent { get; set; } = AgentToolProtocolEnvelope.ComputeDigest("original-process-action-and-manager");
    internal bool ManagerAllowed { get; set; } = true;
    internal Action<int>? OnManagerCheck { get; set; }
    internal Action? AfterDispatch { get; set; }
    internal bool StopBeforeClaim { get; set; }
    internal int ManagerChecks { get; private set; }
    internal int Dispatches { get; private set; }
    internal AgentToolApprovalBinding Binding => waiting.PendingApprovals.Single().ToolAdmission!;
    public AgentSavedEffectDispatchSupport SavedEffectDispatchSupport => AgentSavedEffectDispatchSupport.OriginalPreparedOwnerEffect;
    public AgentPlanningOwner Owner => AgentPlanningOwner.ProjectTaskPlan;
    public AgentPlanningPromptContract Prompt { get; } = new("Use the original material.", "original_plan", "{\"type\":\"object\"}");

    internal static async Task<AgentInheritedPlanningReviewFixture> CreateAsync(ReadBasisMode mode = ReadBasisMode.StepSnapshot) {
        var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync(parentMode: mode);
        try {
            return new(source, await source.WaitForApprovalAsync());
        } catch {
            await source.DisposeAsync();
            throw;
        }
    }

    internal AgentInheritedPlanningReviewRequest Request(AgentPlanningReviewChoice choice)
        => new(Guid.NewGuid(), choice, AgentPlanningDecisionExpectation.From(waiting), Binding, waiting.PendingApprovals.Single().ApprovalId);

    internal AgentInheritedSavedEffectContinuation Authority(AgentInheritedPlanningReviewRequest request, bool currentManager = true) {
        var original = waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin!;
        return currentManager
            ? AgentInheritedSavedEffectContinuation.ForManagerReview(original, request.Expected, request.Binding, RequireParentAsync, RequireManagerAsync)
            : AgentInheritedSavedEffectContinuation.ForManagerReview(original, request.Expected, request.Binding, RequireParentAsync);
    }

    internal AgentInheritedPlanningReviewCoordinator Coordinator(bool fresh = false, ISandboxWorkspaceExecutionRunStore? reader = null) {
        var store = source.Files.NewStore();
        var journal = fresh ? source.NewJournal(store) : source.Journal;
        return new(reader ?? store, journal, new(store, journal, this, [this]));
    }

    internal Task<AgentInheritedPlanningReviewResult> ReviewAsync(AgentInheritedPlanningReviewRequest request, bool fresh = false)
        => Coordinator(fresh).ReviewAsync(request, Authority(request), OwnerIntent, this, Token);

    private Task RequireManagerAsync(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        ManagerChecks++;
        OnManagerCheck?.Invoke(ManagerChecks);
        if (!ManagerAllowed) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        return Task.CompletedTask;
    }

    private Task RequireParentAsync(ExecutionRunRecord run, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        Assert.Equal(waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin, run.ToolAdmission!.PlanningDecision!.InheritedOrigin);
        return Task.CompletedTask;
    }

    public async Task<AgentSavedEffectDispatchResult> DispatchSavedEffectAsync(AgentSavedEffectDispatchRequest request,
        CancellationToken cancellationToken = default) {
        Dispatches++;
        Assert.Equal(waiting.ToolAdmission!.Session.Reference, request.Session);
        Assert.Equal(Binding, request.Binding);
        if (StopBeforeClaim) {
            throw new IOException("Stopped after durable approval, before original effect entry.");
        }
        var lease = AgentToolRunLease.Current!;
        var payload = AgentToolJournalTransitions.RequireProposal(waiting.ToolAdmission, Binding).Payload;
        await source.Journal.ObserveSavedEffectReceiptAsync(lease, this, cancellationToken);
        var claim = await source.Journal.ClaimSavedInvocationAsync(lease, Binding, payload, cancellationToken);
        using var claimed = claim.Bind();
        var current = await source.ReadAsync();
        await using (var held = await source.Files.NewStore().AcquirePlanningAuthoritySnapshotAsync(
            AgentPlanningPublicationExpectation.Inherited(source.Files.StorageScope, current.ToolAdmission!.Session,
                current.Revision, current.ToolAdmission.Revision), cancellationToken)) {
            await lease.PlanningWork!.RequireCurrentManagerPublicationAsync(held, Binding, cancellationToken);
        }
        source.Clock.Advance(TimeSpan.FromSeconds(1));
        await source.Journal.CompleteInvocationAsync(claim, AgentPlanningDecisionTestData.Envelope("{\"committed\":true}"),
            AgentToolEffectState.Committed, cancellationToken);
        var result = await source.Journal.CheckpointSavedEffectAsync(lease, cancellationToken);
        AfterDispatch?.Invoke();
        return result;
    }

    public bool Supports(string toolName) => toolName == AgentPlanningLiveOwnerFixture.Publication().Payload.ToolName;
    public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken = default) {
        await claim.RequireAsync(cancellationToken);
        return AgentToolReceiptObservation.NotObserved;
    }
    public bool SupportsParent(AgentToolProtocolEnvelope parent)
        => parent == waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin!.ParentBinding;
    public Task RequireCurrentReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Equal(waiting.ToolAdmission!.PlanningDecision!.DecisionId, decision.DecisionId);
        return Task.CompletedTask;
    }
    public AgentPlanningPublicationAssessment ClassifyPublication(AgentToolSessionObservation original,
        AgentToolPreparedPayload payload, AgentSavedEffectDispatchResult result)
        => new(result.EffectState == AgentToolEffectState.Committed
            ? AgentPlanningPublicationOutcome.Committed : AgentPlanningPublicationOutcome.Refused, "Original effect outcome.");
    public AgentToolSemanticDigest CaptureRuntimeContract(AgentDefinition agent, ProviderProfile provider, IAgentPlanningStepParticipant participant)
        => throw new InvalidOperationException("Review must not prepare another provider execution.");
    public Task<AgentPlanningStepResult> RunAsync(AgentPlanningStepRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Review must not invoke the planning provider.");
    public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Review must not invoke ordinary continuation.");
    public Task<AgentPlanningBasisRecord> ReadOriginalBasisAsync(AgentPlanningInheritedOrigin original, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Review must retain the original material.");
    public AgentPlanningResponsePreparation PreparePublication(AgentPlanningDecisionRecord decision,
        AgentPlanningBasisRecord basis, string responseText)
        => throw new InvalidOperationException("Review must not prepare another proposal.");

    public async ValueTask DisposeAsync() {
        deadline.Dispose();
        await source.DisposeAsync();
    }
}
