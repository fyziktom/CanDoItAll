using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

internal sealed record ProcessDecisionStepIdentity(ProcessRunId RunId, ProcessStepInstanceId StepId,
    DecisionBudgetId BudgetId, DispatchClaimToken Claim) {
    internal RuntimeCommandId Reserve => new(Derive(IdentityKind.Reserve));
    internal RuntimeCommandId Complete => new(Derive(IdentityKind.Complete));
    internal RuntimeCommandId Abort => new(Derive(IdentityKind.Abort));
    internal DecisionBudgetAllocationId CaptureAllocation => new(Derive(IdentityKind.CaptureAllocation));
    internal ProcessDecisionId Decision => new(Derive(IdentityKind.Decision));
    internal ProcessDecisionMaterialScopeId Scope => new(Derive(IdentityKind.Scope));
    internal ProcessDecisionMaterialId Material => new(Derive(IdentityKind.Material));

    internal static RuntimeCommandId Enrollment(ProcessDecisionChildId child) => new(ChildIdentity(child, IdentityKind.Enroll));
    internal static DecisionBudgetAllocationId ChildAllocation(ProcessDecisionChildId child) => new(ChildIdentity(child, IdentityKind.ChildAllocation));

    internal static long Remaining(DecisionBudgetBalance budget) {
        budget.Validate();
        if (budget.OverrunDetected || budget.AllocationCount == DecisionBudgetBalance.MaximumAllocations) {
            return 0;
        }
        return checked(budget.Ceiling.MaximumWorkDuration.Ticks - budget.ConsumedWorkTicks - budget.ReservedWorkTicks);
    }

    private Guid Derive(IdentityKind kind) => Hash(new {
        Domain = "process-selected-step-preparation/v1", Run = RunId.Value, Step = StepId.Value,
        Budget = BudgetId.Value, Claim = Claim.Value, Kind = kind
    });

    private static Guid ChildIdentity(ProcessDecisionChildId child, IdentityKind kind)
        => Hash(new { Domain = "process-selected-step-child/v1", Child = child.Value, Kind = kind });

    private static Guid Hash<T>(T value) => new(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)).AsSpan(0, 16), bigEndian: true);
    private enum IdentityKind { Reserve, Complete, Abort, CaptureAllocation, Decision, Scope, Material, Enroll, ChildAllocation }
}
