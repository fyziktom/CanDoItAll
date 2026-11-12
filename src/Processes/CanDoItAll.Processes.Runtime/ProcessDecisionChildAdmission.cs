using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessDecisionChildIdentity {
    public static ProcessDecisionChildId Create(ProcessDecisionReference reference, ProcessDecisionChildOwner owner, int ordinal) {
        ArgumentNullException.ThrowIfNull(reference);
        reference.Validate();
        if (!Enum.IsDefined(owner) || ordinal is < 1 or > ProcessDecisionGeneration.MaximumChildren) {
            throw new ArgumentOutOfRangeException(nameof(ordinal), "A decision child requires a supported owner and a bounded logical ordinal.");
        }
        var identity = FormattableString.Invariant($"process-decision-child/v1|{reference.DecisionId.Value:N}|{reference.Generation}|{(int)owner}|{ordinal}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new(new Guid(hash.AsSpan(0, 16), bigEndian: true));
    }
}

public sealed record ProcessDecisionChildAddress(ProcessRunId RootRunId, ProcessRunId RunId, ProcessStepInstanceId StepInstanceId,
    ProcessDecisionReference Reference, ProcessDecisionChildId ChildId) {
    public void Validate() {
        if (RootRunId.Value == Guid.Empty || RunId.Value == Guid.Empty || StepInstanceId.Value == Guid.Empty || ChildId.Value == Guid.Empty || Reference is null) {
            throw new InvalidDataException("A decision child requires its complete original runtime address.");
        }
        Reference.Validate();
    }
}

public sealed record ProcessDecisionChildAdmission(ProcessDecisionChildAddress Address, ProcessDecisionMaterialId MaterialId,
    ProcessDecisionChildOwner Owner, DecisionBudgetId BudgetId, DecisionBudgetAllocationId AllocationId, DecisionBudgetAmount Grant) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Address);
        Address.Validate();
        if (MaterialId.Value == Guid.Empty || !Enum.IsDefined(Owner)) {
            throw new InvalidDataException("A decision child requires its original material and supported owner.");
        }
        new DecisionBudgetAllocation(BudgetId, AllocationId, Grant).Validate();
    }
}

public sealed record ProcessDecisionChildObservation(ProcessDecisionChildAdmission Admission, ProcessDecisionGeneration Generation,
    ProcessDecisionChildClosure? Closure, DecisionBudgetSettlement? Settlement, bool IsCurrent);
