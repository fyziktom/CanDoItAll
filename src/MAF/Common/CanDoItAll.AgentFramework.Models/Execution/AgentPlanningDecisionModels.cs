using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Models;

public enum AgentPlanningOwner { ProjectTaskPlan = 1 }
public enum AgentPlanningGenerationReason { Initial = 1, ExplicitRefresh = 2, AutomaticReplan = 3 }
public enum AgentPlanningGenerationState { Active = 1, AwaitingApproval = 2, PausedBasisChanged = 3, Completed = 4, Cancelled = 5, Failed = 6, Superseded = 7 }
public enum AgentPlanningStepState { CapturingBasis = 1, PreparedInput = 2, ProviderEntered = 3, ResponseAdmitted = 4, Retired = 5 }
public enum AgentPlanningWorkPhase { BasisPreparation = 1, LiveProvider = 2, SavedEffectContinuation = 3, Publication = 4 }
public enum AgentPlanningWorkSettlementReason { AwaitingApproval = 1, PausedBasisChanged = 2, Terminal = 3, ReconciliationOnly = 4, StepCompleted = 5, InitialAdmission = 6 }

public sealed record AgentPlanningPolicyProvenance(
    [property: JsonRequired] Guid WorkspaceNamespaceId,
    [property: JsonRequired] Guid WorkspaceHeadRevision,
    [property: JsonRequired] Guid AgentId,
    [property: JsonRequired] long CatalogDataRevision,
    [property: JsonRequired] DecisionDataPolicy WorkspacePolicy,
    [property: JsonRequired] DecisionDataPolicyOverride? AgentOverride,
    [property: JsonRequired] DecisionDataPolicyOverride? AuthorizedInvocationOverride,
    [property: JsonRequired] DecisionDataPolicyCapabilities OwnerCapabilities,
    [property: JsonRequired] ImmutableArray<DecisionDataPolicyBudget> EnclosingBudgets) {
    public DecisionDataPolicy Resolve() {
        if (WorkspaceNamespaceId == Guid.Empty || WorkspaceHeadRevision == Guid.Empty || AgentId == Guid.Empty ||
            CatalogDataRevision < 1 || WorkspacePolicy is null || OwnerCapabilities is null || EnclosingBudgets.IsDefault ||
            EnclosingBudgets.Length > AgentPlanningDecisionRecord.MaximumSteps || EnclosingBudgets.Any(item => item is null)) {
            throw AgentPlanningDecisionRecord.Invalid();
        }
        _ = DecisionDataPolicyCodec.Serialize(WorkspacePolicy);
        if (AgentOverride is not null) {
            _ = DecisionDataPolicyOverrideCodec.Serialize(AgentOverride);
        }
        if (AuthorizedInvocationOverride is not null) {
            _ = DecisionDataPolicyOverrideCodec.Serialize(AuthorizedInvocationOverride);
        }
        return DecisionDataPolicyResolver.Resolve(WorkspacePolicy, OwnerCapabilities, definition: AgentOverride,
            authorizedInvocation: AuthorizedInvocationOverride, enclosingBudgets: EnclosingBudgets);
    }
}

public sealed record AgentPlanningBasisRecord(
    [property: JsonRequired] Guid Id,
    [property: JsonRequired] DateTimeOffset CapturedAtUtc,
    [property: JsonRequired] AgentToolProtocolEnvelope Value);

public sealed record AgentPlanningPublicationBinding(
    [property: JsonRequired] AgentToolApprovalBinding Proposal,
    [property: JsonRequired] Guid BasisId,
    [property: JsonRequired] AgentToolSemanticDigest BasisDigest,
    [property: JsonRequired] AgentToolProtocolEnvelope OriginalOperation);

public sealed record AgentPlanningStepRecord(
    [property: JsonRequired] Guid Id,
    [property: JsonRequired] int Ordinal,
    [property: JsonRequired] Guid OriginDispatchLeaseId,
    [property: JsonRequired] AgentPlanningStepState State,
    [property: JsonRequired] Guid? BasisId,
    [property: JsonRequired] Guid? OriginSegmentId,
    [property: JsonRequired] AgentToolProviderDispatchId? ProviderDispatchId,
    [property: JsonRequired] AgentToolBatchId? ResponseBatchId,
    [property: JsonRequired] ImmutableArray<AgentPlanningPublicationBinding> Publications);

public sealed record AgentPlanningGenerationRecord(
    [property: JsonRequired] int Number,
    [property: JsonRequired] AgentPlanningGenerationReason Reason,
    [property: JsonRequired] AgentPlanningGenerationState State,
    [property: JsonRequired] long ClosedWorkTicksAtStart,
    [property: JsonRequired] ImmutableArray<AgentPlanningBasisRecord> Bases,
    [property: JsonRequired] ImmutableArray<AgentPlanningStepRecord> Steps);

public sealed record AgentPlanningOpenWorkInterval(
    [property: JsonRequired] Guid Id,
    [property: JsonRequired] int Generation,
    [property: JsonRequired] Guid StepId,
    [property: JsonRequired] Guid DispatchLeaseId,
    [property: JsonRequired] DateTimeOffset OpenedAtUtc,
    [property: JsonRequired] AgentPlanningWorkPhase Phase);

public sealed record AgentPlanningWorkSettlement(
    [property: JsonRequired] Guid IntervalId,
    [property: JsonRequired] DateTimeOffset EndedAtUtc,
    [property: JsonRequired] long ChargedTicks,
    [property: JsonRequired] AgentPlanningWorkSettlementReason Reason);

public sealed record AgentPlanningWorkRecord(
    [property: JsonRequired] long Revision,
    [property: JsonRequired] long ClosedWorkTicks,
    [property: JsonRequired] DateTimeOffset LastObservedAtUtc,
    [property: JsonRequired] AgentPlanningOpenWorkInterval? Open,
    [property: JsonRequired] AgentPlanningWorkSettlement? LastSettlement);

[JsonConverter(typeof(AgentPlanningDecisionJsonConverter))]
public sealed record AgentPlanningDecisionRecord(
    Guid DecisionId,
    AgentToolSessionReference Session,
    DateTimeOffset StartedAtUtc,
    AgentPlanningOwner Owner,
    AgentToolProtocolEnvelope OriginalTarget,
    DecisionDataPolicy Policy,
    AgentPlanningPolicyProvenance? Provenance,
    int CurrentGeneration,
    int AutomaticReplans,
    ImmutableArray<AgentPlanningGenerationRecord> Generations,
    AgentPlanningWorkRecord Work,
    AgentPlanningInheritedOrigin? InheritedOrigin = null) {
    public const int Version = 1;
    public const int InheritedVersion = 2;
    public const int MaximumGenerations = 32;
    public const int MaximumSteps = AgentToolJournalRecord.MaximumSegments;
    public const int MaximumUtf8Bytes = AgentToolProtocolEnvelope.MaximumUtf8Bytes;

    [JsonIgnore]
    public AgentPlanningGenerationRecord Current => Generations[CurrentGeneration - 1];

    public void Validate() {
        if (DecisionId == Guid.Empty || Session is null || !IsUtc(StartedAtUtc) || !Enum.IsDefined(Owner) ||
            OriginalTarget is null || Policy is null || (Provenance is null) == (InheritedOrigin is null) || Work is null ||
            Generations.IsDefaultOrEmpty || Generations.Length > MaximumGenerations ||
            CurrentGeneration != Generations.Length || AutomaticReplans < 0 ||
            AutomaticReplans > Policy.Budget.MaximumReplans ||
            Policy != (InheritedOrigin?.ResolvePolicy() ?? Provenance!.Resolve())) {
            throw Invalid();
        }
        if (InheritedOrigin is { } inherited) {
            inherited.Validate(Session, StartedAtUtc);
            if (CurrentGeneration != 1 || AutomaticReplans != 0) {
                throw Invalid();
            }
        } else if (Session.BackgroundSource is not null) {
            throw Invalid();
        }
        var stepIds = new HashSet<Guid>();
        var basisIds = new HashSet<Guid>();
        var segmentIds = new HashSet<Guid>();
        var dispatchIds = new HashSet<AgentToolProviderDispatchId>();
        var proposalIds = new HashSet<AgentToolBusinessIntentId>();
        var replans = 0;
        long payloadBytes = Encoding.UTF8.GetByteCount(OriginalTarget.PayloadJson);
        if (InheritedOrigin is { } origin) {
            payloadBytes += Encoding.UTF8.GetByteCount(origin.ParentBinding.PayloadJson);
        }
        for (var ordinal = 0; ordinal < Generations.Length; ordinal++) {
            var generation = Generations[ordinal];
            if (generation is null || generation.Number != ordinal + 1 || !Enum.IsDefined(generation.Reason) ||
                !Enum.IsDefined(generation.State) || (ordinal == 0) != (generation.Reason == AgentPlanningGenerationReason.Initial) ||
                ordinal < Generations.Length - 1 && generation.State != AgentPlanningGenerationState.Superseded ||
                generation.ClosedWorkTicksAtStart < 0 || generation.ClosedWorkTicksAtStart > Work.ClosedWorkTicks ||
                ordinal == 0 && generation.ClosedWorkTicksAtStart != 0 ||
                ordinal > 0 && generation.ClosedWorkTicksAtStart < Generations[ordinal - 1].ClosedWorkTicksAtStart ||
                generation.Bases.IsDefault || generation.Steps.IsDefault || generation.Bases.Length > MaximumSteps ||
                generation.Steps.Length > MaximumSteps ||
                Policy.ReadBasisMode == ReadBasisMode.RunSnapshot && generation.Bases.Length > 1 ||
                generation.Bases.Any(basis => basis is null || !generation.Steps.Any(step => step?.BasisId == basis.Id)) ||
                generation.Steps.Take(Math.Max(0, generation.Steps.Length - 1))
                    .Any(step => step?.State != AgentPlanningStepState.ResponseAdmitted) ||
                generation.State is AgentPlanningGenerationState.AwaitingApproval or AgentPlanningGenerationState.PausedBasisChanged or
                    AgentPlanningGenerationState.Completed && generation.Steps.LastOrDefault()?.State != AgentPlanningStepState.ResponseAdmitted ||
                generation.State == AgentPlanningGenerationState.AwaitingApproval &&
                    generation.Steps.LastOrDefault()?.Publications.IsEmpty != false) {
                throw Invalid();
            }
            if (generation.Reason == AgentPlanningGenerationReason.AutomaticReplan) {
                replans = checked(replans + 1);
            }
            foreach (var basis in generation.Bases) {
                if (basis is null || basis.Id == Guid.Empty || !basisIds.Add(basis.Id) || basis.Value is null ||
                    !IsUtc(basis.CapturedAtUtc) || InheritedOrigin is null && basis.CapturedAtUtc < StartedAtUtc ||
                    basis.CapturedAtUtc > Work.LastObservedAtUtc) {
                    throw Invalid();
                }
                InheritedOrigin?.RequireOriginalBasis(basis);
                payloadBytes = checked(payloadBytes + Encoding.UTF8.GetByteCount(basis.Value.PayloadJson));
            }
            for (var index = 0; index < generation.Steps.Length; index++) {
                var step = generation.Steps[index];
                if (step is null || step.Id == Guid.Empty || !stepIds.Add(step.Id) || step.Ordinal != index ||
                    step.OriginDispatchLeaseId == Guid.Empty || !Enum.IsDefined(step.State) || step.Publications.IsDefault ||
                    step.Publications.Length > AgentToolJournalRecord.MaximumCallsPerBatch ||
                    (step.BasisId is null) != (step.OriginSegmentId is null) ||
                    step.State == AgentPlanningStepState.CapturingBasis && step.BasisId is not null ||
                    step.State is not (AgentPlanningStepState.CapturingBasis or AgentPlanningStepState.Retired) && step.BasisId is null ||
                    step.OriginSegmentId is { } segment && (segment == Guid.Empty || !segmentIds.Add(segment)) ||
                    step.ProviderDispatchId is { } dispatch && (dispatch.Value == Guid.Empty || !dispatchIds.Add(dispatch)) ||
                    step.State is AgentPlanningStepState.ProviderEntered or AgentPlanningStepState.ResponseAdmitted && step.ProviderDispatchId is null ||
                    step.State is AgentPlanningStepState.CapturingBasis or AgentPlanningStepState.PreparedInput && step.ProviderDispatchId is not null ||
                    step.ResponseBatchId is { } response && response.Value == Guid.Empty ||
                    step.ResponseBatchId is not null && step.ProviderDispatchId is null ||
                    step.State == AgentPlanningStepState.ResponseAdmitted && step.ResponseBatchId is null ||
                    step.State != AgentPlanningStepState.ResponseAdmitted && step.ResponseBatchId is not null ||
                    step.State != AgentPlanningStepState.ResponseAdmitted && !step.Publications.IsEmpty) {
                    throw Invalid();
                }
                var basis = step.BasisId is { } selected ? generation.Bases.SingleOrDefault(item => item.Id == selected) : null;
                if (step.BasisId is not null && basis is null ||
                    Policy.ReadBasisMode == ReadBasisMode.StepSnapshot && basis is not null &&
                    generation.Steps.Take(index).Any(prior => prior.BasisId == basis.Id)) {
                    throw Invalid();
                }
                foreach (var publication in step.Publications) {
                    if (publication is null || publication.Proposal is null || publication.OriginalOperation is null ||
                        basis is null || publication.BasisId != basis.Id || publication.BasisDigest != basis.Value.Digest ||
                        publication.Proposal.BatchId != step.ResponseBatchId || publication.Proposal.IntentId.Value == Guid.Empty ||
                        publication.Proposal.SemanticVersion < 1 || !proposalIds.Add(publication.Proposal.IntentId)) {
                        throw Invalid();
                    }
                    payloadBytes = checked(payloadBytes + Encoding.UTF8.GetByteCount(publication.OriginalOperation.PayloadJson));
                }
            }
        }
        if (stepIds.Count > MaximumSteps || replans != AutomaticReplans || payloadBytes > MaximumUtf8Bytes) {
            throw Invalid();
        }
        ValidateWork();
    }

    public void ValidateJournal(AgentToolJournalRecord journal) {
        Validate();
        if (journal.Session.Reference != Session || journal.Session.AgentId != (InheritedOrigin?.AgentId ?? Provenance!.AgentId) ||
            InheritedOrigin is { } inherited && (journal.Session.Profile != inherited.Profile ||
                journal.Session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation)) {
            throw Invalid();
        }
        var steps = Generations.SelectMany(generation => generation.Steps).ToArray();
        foreach (var segment in journal.Segments) {
            var step = steps.SingleOrDefault(item => item.OriginSegmentId == segment.Id);
            if (step is null || segment.CheckpointKind != AgentToolCheckpointKind.EffectsOnly ||
                segment.OriginDispatchLeaseId != step.OriginDispatchLeaseId) {
                throw Invalid();
            }
        }
        foreach (var step in steps) {
            if (step.OriginSegmentId is { } segment && !journal.Segments.Any(item => item.Id == segment)) {
                throw Invalid();
            }
            if (step.ProviderDispatchId is { } dispatch) {
                var saved = journal.ProviderDispatches.IsDefault ? null : journal.ProviderDispatches.FirstOrDefault(item => item.Id == dispatch);
                if (saved is null || saved.SegmentId != step.OriginSegmentId || saved.ResponseBatchId != step.ResponseBatchId ||
                    (saved.State == AgentToolProviderDispatchState.ResponseAdmitted) != (step.State == AgentPlanningStepState.ResponseAdmitted)) {
                    throw Invalid();
                }
            }
            foreach (var publication in step.Publications) {
                var proposal = journal.Batches.FirstOrDefault(item => item.Id == publication.Proposal.BatchId)?.Proposals
                    .FirstOrDefault(item => item.IntentId == publication.Proposal.IntentId);
                if (proposal is null || proposal.Payload.SemanticVersion != publication.Proposal.SemanticVersion ||
                    proposal.Payload.Digest != publication.Proposal.Digest ||
                    proposal.Payload.SourcePreparation != publication.OriginalOperation ||
                    proposal.Payload.Effect != AgentToolProposalEffect.Mutation ||
                    proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt ||
                    !proposal.RequiresApproval || proposal.ProviderCall is not null) {
                    throw Invalid();
                }
                if (Generations.Single(generation => generation.Steps.Any(item => item.Id == step.Id)).State == AgentPlanningGenerationState.Superseded &&
                    (proposal.State is AgentToolProposalState.Prepared or AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired ||
                        proposal.EffectState is AgentToolEffectState.Unknown or AgentToolEffectState.Committed)) {
                    throw Invalid();
                }
            }
        }
        if ((!journal.ProviderDispatches.IsDefault && journal.ProviderDispatches.Any(item =>
                !item.ApprovedCalls.IsEmpty || !steps.Any(step => step.ProviderDispatchId == item.Id))) ||
            journal.Batches.Any(batch => batch.ProviderDispatchId is null || !steps.Any(step => step.ResponseBatchId == batch.Id) ||
                batch.Proposals.Any(proposal => !steps.Any(step => step.Publications.Any(item => item.Proposal.IntentId == proposal.IntentId)))) ||
            Current.State == AgentPlanningGenerationState.Completed &&
                (journal.HasUnresolvedEffects || journal.Batches.SelectMany(batch => batch.Proposals).Any(proposal => proposal.EffectState == AgentToolEffectState.Unknown))) {
            throw Invalid();
        }
    }

    private void ValidateWork() {
        if (Work.Revision < 1 || Work.ClosedWorkTicks < 0 || !IsUtc(Work.LastObservedAtUtc) || Work.LastObservedAtUtc < StartedAtUtc ||
            Work.ClosedWorkTicks > 0 && Work.LastSettlement is null ||
            Current.State == AgentPlanningGenerationState.Active && !Current.Steps.IsEmpty && Work.Open is null) {
            throw Invalid();
        }
        if (Work.Open is { } open) {
            if (open.Id == Guid.Empty || open.Generation != CurrentGeneration || open.DispatchLeaseId == Guid.Empty ||
                !IsUtc(open.OpenedAtUtc) || open.OpenedAtUtc < StartedAtUtc || open.OpenedAtUtc > Work.LastObservedAtUtc ||
                !Enum.IsDefined(open.Phase) || Current.Steps.LastOrDefault()?.Id != open.StepId || Current.State is AgentPlanningGenerationState.AwaitingApproval or
                    AgentPlanningGenerationState.PausedBasisChanged or AgentPlanningGenerationState.Completed or AgentPlanningGenerationState.Superseded) {
                throw Invalid();
            }
            var step = Current.Steps[^1];
            if (open.Phase switch {
                AgentPlanningWorkPhase.BasisPreparation => step.State is not (AgentPlanningStepState.CapturingBasis or AgentPlanningStepState.PreparedInput) ||
                    open.DispatchLeaseId != step.OriginDispatchLeaseId,
                AgentPlanningWorkPhase.LiveProvider => step.State is not (AgentPlanningStepState.ProviderEntered or AgentPlanningStepState.ResponseAdmitted) ||
                    open.DispatchLeaseId != step.OriginDispatchLeaseId,
                AgentPlanningWorkPhase.SavedEffectContinuation or AgentPlanningWorkPhase.Publication => step.State != AgentPlanningStepState.ResponseAdmitted,
                _ => true
            }) {
                throw Invalid();
            }
        }
        if (Work.LastSettlement is { } settlement && (settlement.IntervalId == Guid.Empty || !IsUtc(settlement.EndedAtUtc) ||
                settlement.EndedAtUtc < StartedAtUtc || settlement.EndedAtUtc > Work.LastObservedAtUtc ||
                settlement.ChargedTicks < 0 || settlement.ChargedTicks > Work.ClosedWorkTicks || !Enum.IsDefined(settlement.Reason) ||
                Work.Open?.Id == settlement.IntervalId)) {
            throw Invalid();
        }
    }

    internal static bool IsUtc(DateTimeOffset value) => value != default && value.Offset == TimeSpan.Zero;
    internal static InvalidDataException Invalid() => new("The saved planning decision, basis, origin or work record is invalid.");
    public override string ToString() => $"Planning decision {DecisionId:N}, generation {CurrentGeneration}";
}
