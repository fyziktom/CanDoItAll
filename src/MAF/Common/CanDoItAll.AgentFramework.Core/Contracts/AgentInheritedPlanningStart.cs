using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentInheritedPlanningStartMode { FreshEnrollment = 1, ExistingOnly = 2 }

public sealed record AgentInheritedPlanningStart(DecisionBudgetAllocation Allocation, AgentInheritedPlanningStartMode Mode) {
    internal TimeSpan PreparationLimit => TimeSpan.FromTicks(Math.Min(TimeSpan.FromSeconds(10).Ticks, Allocation.Grant.WorkTicks));

    internal void Validate() {
        ArgumentNullException.ThrowIfNull(Allocation);
        Allocation.Validate();
        if (!Enum.IsDefined(Mode) || Allocation.Settlement is not null || Allocation.Grant.WorkTicks <= 0 || Allocation.Grant.Replans != 0) {
            throw new InvalidDataException("Inherited startup requires its original open finite work allocation and explicit start mode.");
        }
    }

    internal void RequireOriginal(AgentPlanningInheritedOrigin original) {
        if (original.Allocation != Allocation) {
            throw new InvalidDataException("Inherited startup cannot replace its original parent allocation.");
        }
    }
}
