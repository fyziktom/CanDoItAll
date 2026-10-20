using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentPlanningAdmissionRefusal {
    DecisionInactive,
    WorkAlreadyOpen,
    OriginalStepChanged,
    WorkBudgetExhausted,
    BasisExpired,
    ClockInvalid,
    ReplanLimit,
    HistoryCapacity,
    InterruptedWorkAccountingUnavailable,
    InvalidPublication,
    InvalidProviderOutput,
    ParentReconciliationRequired
}

public sealed class AgentPlanningAdmissionException(Guid decisionId, int generation, AgentPlanningAdmissionRefusal reason)
    : InvalidOperationException($"Planning decision {decisionId:N}, generation {generation} refused admission: {reason}.") {
    public Guid DecisionId { get; } = decisionId;
    public int Generation { get; } = generation;
    public AgentPlanningAdmissionRefusal Reason { get; } = reason;
}

public static class AgentPlanningDecisionTransitions {
    public static AgentPlanningDecisionRecord Create(AgentToolSessionReference session, AgentToolProtocolEnvelope originalTarget,
        AgentPlanningPolicyProvenance provenance, DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(provenance);
        var result = new AgentPlanningDecisionRecord(Guid.NewGuid(), session, now, AgentPlanningOwner.ProjectTaskPlan,
            originalTarget, provenance.Resolve(), provenance, 1, 0,
            [new(1, AgentPlanningGenerationReason.Initial, AgentPlanningGenerationState.Active, 0, [], [])],
            new(1, 0, now, null, null));
        return Valid(result);
    }

    public static AgentPlanningDecisionRecord CreateInherited(AgentToolSessionReference session, AgentToolProtocolEnvelope originalTarget,
        AgentPlanningOwner owner, AgentPlanningInheritedOrigin origin, DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(origin);
        var result = Valid(new(Guid.NewGuid(), session, now, owner, originalTarget, origin.ResolvePolicy(), null, 1, 0,
            [new(1, AgentPlanningGenerationReason.Initial, AgentPlanningGenerationState.Active, 0, [], [])],
            new(1, 0, now, null, null), origin));
        if (now - origin.CapturedAtUtc >= result.Policy.Budget.MaximumBasisAge) {
            throw Refuse(result, AgentPlanningAdmissionRefusal.BasisExpired);
        }
        return result;
    }

    public static AgentPlanningDecisionRecord BeginStep(AgentPlanningDecisionRecord current, Guid dispatchLeaseId, DateTimeOffset now) {
        RequireActive(current, now);
        RequireClosed(current);
        RequireBudget(current, TimeSpan.Zero, now);
        if (!current.Current.Steps.IsEmpty) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        if (dispatchLeaseId == Guid.Empty) {
            throw new ArgumentException("A planning step requires its actual dispatch lease.", nameof(dispatchLeaseId));
        }
        if (current.Generations.Sum(item => item.Steps.Length) >= AgentPlanningDecisionRecord.MaximumSteps) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.HistoryCapacity);
        }
        var step = new AgentPlanningStepRecord(Guid.NewGuid(), current.Current.Steps.Length, dispatchLeaseId,
            AgentPlanningStepState.CapturingBasis, null, null, null, null, []);
        return Valid(WithGeneration(current, current.Current with { Steps = current.Current.Steps.Add(step) }) with {
            Work = Open(current, step.Id, dispatchLeaseId, AgentPlanningWorkPhase.BasisPreparation, now)
        });
    }

    public static AgentPlanningDecisionRecord AdvanceStep(AgentPlanningDecisionRecord current, Guid intervalId,
        Guid dispatchLeaseId, TimeSpan monotonicElapsed, DateTimeOffset now) {
        RequireActive(current, now);
        RequireBudget(current, monotonicElapsed, now);
        if (current.Work.Open is not { } open || open.Id != intervalId || dispatchLeaseId == Guid.Empty ||
            current.Current.Steps.LastOrDefault()?.State != AgentPlanningStepState.ResponseAdmitted) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        if (current.Generations.Sum(item => item.Steps.Length) >= AgentPlanningDecisionRecord.MaximumSteps) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.HistoryCapacity);
        }
        var charged = Elapsed(current, monotonicElapsed, now);
        var step = new AgentPlanningStepRecord(Guid.NewGuid(), current.Current.Steps.Length, dispatchLeaseId,
            AgentPlanningStepState.CapturingBasis, null, null, null, null, []);
        return Valid(WithGeneration(current, current.Current with { Steps = current.Current.Steps.Add(step) }) with {
            Work = new(checked(current.Work.Revision + 1), checked(current.Work.ClosedWorkTicks + charged), now,
                new(Guid.NewGuid(), current.CurrentGeneration, step.Id, dispatchLeaseId, now, AgentPlanningWorkPhase.BasisPreparation),
                new(open.Id, now, charged, AgentPlanningWorkSettlementReason.StepCompleted))
        });
    }

    public static AgentPlanningDecisionRecord AttachBasis(AgentPlanningDecisionRecord current, Guid stepId,
        AgentPlanningBasisRecord captured, Guid originSegmentId, DateTimeOffset now) {
        RequireActive(current, now);
        var step = RequireStep(current, stepId, AgentPlanningStepState.CapturingBasis);
        RequireOpen(current, stepId);
        if (originSegmentId == Guid.Empty) {
            throw new ArgumentException("A captured planning basis requires its actual journal origin.", nameof(originSegmentId));
        }
        var bases = current.Current.Bases;
        current.InheritedOrigin?.RequireOriginalBasis(captured);
        if (current.Policy.ReadBasisMode == ReadBasisMode.RunSnapshot && !bases.IsEmpty) {
            if (bases[0] != captured) {
                throw Refuse(current, AgentPlanningAdmissionRefusal.OriginalStepChanged);
            }
        } else {
            bases = bases.Add(captured);
        }
        return Valid(WithGeneration(current, current.Current with {
            Bases = bases,
            Steps = current.Current.Steps.SetItem(step.Ordinal, step with {
                State = AgentPlanningStepState.PreparedInput, BasisId = captured.Id, OriginSegmentId = originSegmentId
            })
        }) with { Work = current.Work with { Revision = checked(current.Work.Revision + 1), LastObservedAtUtc = now } });
    }

    public static AgentPlanningDecisionRecord EnterProvider(AgentPlanningDecisionRecord current, Guid stepId,
        AgentToolProviderDispatchId dispatchId, TimeSpan monotonicElapsed, DateTimeOffset now) {
        RequireActive(current, now);
        var step = RequireStep(current, stepId, AgentPlanningStepState.PreparedInput);
        var open = RequireOpen(current, stepId);
        RequireBudget(current, monotonicElapsed, now);
        RequireBasisAge(current, step, now);
        return Valid(WithGeneration(current, current.Current with {
            Steps = current.Current.Steps.SetItem(step.Ordinal, step with {
                State = AgentPlanningStepState.ProviderEntered, ProviderDispatchId = dispatchId
            })
        }) with { Work = current.Work with {
            Revision = checked(current.Work.Revision + 1), LastObservedAtUtc = now,
            Open = open with { Phase = AgentPlanningWorkPhase.LiveProvider }
        } });
    }

    public static AgentPlanningDecisionRecord AdmitResponse(AgentPlanningDecisionRecord current, Guid stepId,
        AgentToolBatchId batchId, ImmutableArray<AgentPlanningPublicationBinding> publications, DateTimeOffset now) {
        RequireTime(current, now);
        var step = RequireStep(current, stepId, AgentPlanningStepState.ProviderEntered);
        RequireOpen(current, stepId);
        return Valid(WithGeneration(current, current.Current with {
            Steps = current.Current.Steps.SetItem(step.Ordinal, step with {
                State = AgentPlanningStepState.ResponseAdmitted, ResponseBatchId = batchId, Publications = publications
            })
        }) with { Work = current.Work with { Revision = checked(current.Work.Revision + 1), LastObservedAtUtc = now } });
    }

    public static AgentPlanningDecisionRecord ResumeEffects(AgentPlanningDecisionRecord current, Guid stepId, Guid dispatchLeaseId,
        DateTimeOffset now) {
        current.Validate();
        RequireTime(current, now);
        RequireClosed(current);
        RequireBudget(current, TimeSpan.Zero, now);
        var step = RequireStep(current, stepId, AgentPlanningStepState.ResponseAdmitted);
        if (current.Current.State != AgentPlanningGenerationState.AwaitingApproval || dispatchLeaseId == Guid.Empty) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.DecisionInactive);
        }
        RequireBasisAge(current, step, now);
        return Valid(WithGeneration(current, current.Current with { State = AgentPlanningGenerationState.Active }) with {
            Work = Open(current, stepId, dispatchLeaseId, AgentPlanningWorkPhase.SavedEffectContinuation, now)
        });
    }

    public static AgentPlanningDecisionRecord Settle(AgentPlanningDecisionRecord current, Guid intervalId,
        TimeSpan monotonicElapsed, DateTimeOffset now, AgentPlanningWorkSettlementReason reason,
        AgentPlanningGenerationState state) {
        current.Validate();
        RequireTime(current, now);
        var open = current.Work.Open;
        if (open is null || open.Id != intervalId || !Enum.IsDefined(reason) || !Enum.IsDefined(state) ||
            !ValidSettlement(reason, state) ||
            current.Current.State is AgentPlanningGenerationState.Cancelled or AgentPlanningGenerationState.Failed && state != current.Current.State) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        var charged = Elapsed(current, monotonicElapsed, now);
        return Valid(WithGeneration(current, current.Current with { State = state }) with {
            Work = new(checked(current.Work.Revision + 1), checked(current.Work.ClosedWorkTicks + charged), now, null,
                new(open.Id, now, charged, reason))
        });
    }

    public static AgentPlanningDecisionRecord Cancel(AgentPlanningDecisionRecord current) {
        current.Validate();
        if (current.Current.State is AgentPlanningGenerationState.Completed or AgentPlanningGenerationState.Superseded) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.DecisionInactive);
        }
        return Valid(WithGeneration(current, current.Current with { State = AgentPlanningGenerationState.Cancelled }) with {
            Work = current.Work with { Revision = checked(current.Work.Revision + 1) }
        });
    }

    public static AgentPlanningDecisionRecord Fail(AgentPlanningDecisionRecord current) {
        current.Validate();
        if (current.Current.State is AgentPlanningGenerationState.Completed or AgentPlanningGenerationState.Cancelled or AgentPlanningGenerationState.Superseded) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.DecisionInactive);
        }
        return Valid(WithGeneration(current, current.Current with { State = AgentPlanningGenerationState.Failed }) with {
            Work = current.Work with { Revision = checked(current.Work.Revision + 1) }
        });
    }

    public static AgentPlanningDecisionRecord NewGeneration(AgentPlanningDecisionRecord current, bool automatic, DateTimeOffset now) {
        current.Validate();
        if (current.InheritedOrigin is not null) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.ParentReconciliationRequired);
        }
        RequireTime(current, now);
        RequireClosed(current);
        RequireBudget(current, TimeSpan.Zero, now);
        if (current.Current.State is not (AgentPlanningGenerationState.PausedBasisChanged or AgentPlanningGenerationState.AwaitingApproval)) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.DecisionInactive);
        }
        if (current.Generations.Length == AgentPlanningDecisionRecord.MaximumGenerations ||
            current.Generations.Sum(item => item.Steps.Length) >= AgentPlanningDecisionRecord.MaximumSteps) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.HistoryCapacity);
        }
        if (automatic && (current.Policy.OnBasisChange != OnBasisChange.Replan ||
                current.AutomaticReplans >= current.Policy.Budget.MaximumReplans)) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.ReplanLimit);
        }
        var previous = current.Current with { State = AgentPlanningGenerationState.Superseded };
        var next = new AgentPlanningGenerationRecord(checked(current.CurrentGeneration + 1), automatic
            ? AgentPlanningGenerationReason.AutomaticReplan : AgentPlanningGenerationReason.ExplicitRefresh,
            AgentPlanningGenerationState.Active, current.Work.ClosedWorkTicks, [], []);
        return Valid(current with {
            CurrentGeneration = next.Number,
            AutomaticReplans = automatic ? checked(current.AutomaticReplans + 1) : current.AutomaticReplans,
            Generations = current.Generations.SetItem(current.CurrentGeneration - 1, previous).Add(next),
            Work = current.Work with { Revision = checked(current.Work.Revision + 1), LastObservedAtUtc = now }
        });
    }

    public static void RequireBudget(AgentPlanningDecisionRecord current, TimeSpan monotonicElapsed, DateTimeOffset now)
        => _ = RemainingWork(current, monotonicElapsed, now);

    public static TimeSpan RemainingWork(AgentPlanningDecisionRecord current, TimeSpan monotonicElapsed, DateTimeOffset now) {
        RequireTime(current, now);
        var spent = checked(current.Work.ClosedWorkTicks + (current.Work.Open is null ? 0 : Elapsed(current, monotonicElapsed, now)));
        if (spent >= current.Policy.Budget.MaximumWorkDuration.Ticks) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.WorkBudgetExhausted);
        }
        return TimeSpan.FromTicks(current.Policy.Budget.MaximumWorkDuration.Ticks - spent);
    }

    public static void RequireBasisAge(AgentPlanningDecisionRecord current, AgentPlanningStepRecord step, DateTimeOffset now) {
        RequireTime(current, now);
        var basis = current.Current.Bases.SingleOrDefault(item => item.Id == step.BasisId)
            ?? throw Refuse(current, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        if (now < basis.CapturedAtUtc || now - basis.CapturedAtUtc >= current.Policy.Budget.MaximumBasisAge) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.BasisExpired);
        }
    }

    private static long Elapsed(AgentPlanningDecisionRecord current, TimeSpan monotonicElapsed, DateTimeOffset now) {
        var open = current.Work.Open ?? throw Refuse(current, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        if (monotonicElapsed < TimeSpan.Zero || now < open.OpenedAtUtc) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.ClockInvalid);
        }
        return Math.Max(monotonicElapsed.Ticks, (now - open.OpenedAtUtc).Ticks);
    }

    private static bool ValidSettlement(AgentPlanningWorkSettlementReason reason, AgentPlanningGenerationState state)
        => reason switch {
            AgentPlanningWorkSettlementReason.AwaitingApproval => state == AgentPlanningGenerationState.AwaitingApproval,
            AgentPlanningWorkSettlementReason.PausedBasisChanged => state == AgentPlanningGenerationState.PausedBasisChanged,
            AgentPlanningWorkSettlementReason.Terminal or AgentPlanningWorkSettlementReason.ReconciliationOnly => state is
                AgentPlanningGenerationState.Completed or AgentPlanningGenerationState.Cancelled or AgentPlanningGenerationState.Failed,
            _ => false
        };

    private static void RequireActive(AgentPlanningDecisionRecord current, DateTimeOffset now) {
        current.Validate();
        RequireTime(current, now);
        if (current.Current.State != AgentPlanningGenerationState.Active) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.DecisionInactive);
        }
    }
    private static void RequireClosed(AgentPlanningDecisionRecord current) {
        if (current.Work.Open is not null) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.WorkAlreadyOpen);
        }
    }
    private static AgentPlanningStepRecord RequireStep(AgentPlanningDecisionRecord current, Guid stepId, AgentPlanningStepState state) {
        var step = current.Current.Steps.SingleOrDefault(item => item.Id == stepId);
        return step is not null && step.State == state ? step : throw Refuse(current, AgentPlanningAdmissionRefusal.OriginalStepChanged);
    }
    private static AgentPlanningOpenWorkInterval RequireOpen(AgentPlanningDecisionRecord current, Guid stepId)
        => current.Work.Open is { } open && open.StepId == stepId
            ? open : throw Refuse(current, AgentPlanningAdmissionRefusal.OriginalStepChanged);
    private static AgentPlanningWorkRecord Open(AgentPlanningDecisionRecord current, Guid stepId, Guid leaseId,
        AgentPlanningWorkPhase phase, DateTimeOffset now)
        => current.Work with { Revision = checked(current.Work.Revision + 1), LastObservedAtUtc = now,
            Open = new(Guid.NewGuid(), current.CurrentGeneration, stepId, leaseId, now, phase) };
    private static void RequireTime(AgentPlanningDecisionRecord current, DateTimeOffset now) {
        if (now == default || now.Offset != TimeSpan.Zero || now < current.Work.LastObservedAtUtc) {
            throw Refuse(current, AgentPlanningAdmissionRefusal.ClockInvalid);
        }
    }
    private static AgentPlanningDecisionRecord WithGeneration(AgentPlanningDecisionRecord current, AgentPlanningGenerationRecord generation)
        => current with { Generations = current.Generations.SetItem(current.CurrentGeneration - 1, generation) };
    private static AgentPlanningDecisionRecord Valid(AgentPlanningDecisionRecord value) {
        value.Validate();
        return value;
    }
    private static AgentPlanningAdmissionException Refuse(AgentPlanningDecisionRecord current, AgentPlanningAdmissionRefusal reason)
        => new(current.DecisionId, current.CurrentGeneration, reason);
}
