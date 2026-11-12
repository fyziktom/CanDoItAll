using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessLaunchDecisionDescriptor(ProcessDecisionExecutionContract Execution,
    string DisplayName, string Description, IReadOnlyList<DecisionDataPolicy> SupportedPolicies) {
    public bool Supports(DecisionDataPolicy policy) => SupportedPolicies.Any(supported =>
        supported.ReadBasisMode == policy.ReadBasisMode &&
        supported.PublicationFreshness == policy.PublicationFreshness &&
        supported.OnBasisChange == policy.OnBasisChange &&
        policy.Budget.MaximumReplans <= supported.Budget.MaximumReplans &&
        policy.Budget.MaximumWorkDuration <= supported.Budget.MaximumWorkDuration &&
        policy.Budget.MaximumBasisAge <= supported.Budget.MaximumBasisAge);

    internal ProcessLaunchDecisionDescriptor Snapshot() {
        if (Execution is null || string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Description) ||
                SupportedPolicies is not { Count: > 0 } || SupportedPolicies.Any(policy => policy is null) ||
                SupportedPolicies.Distinct().Count() != SupportedPolicies.Count) {
            throw new InvalidDataException("A Process decision descriptor requires an execution identity, operator guidance and distinct supported policies.");
        }
        Execution.Validate();
        return this with { SupportedPolicies = Array.AsReadOnly(SupportedPolicies.ToArray()) };
    }
}
