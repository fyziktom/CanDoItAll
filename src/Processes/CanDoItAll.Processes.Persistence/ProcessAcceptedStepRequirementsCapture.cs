using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal sealed class ProcessAcceptedStepRequirementsCapture {
    private readonly ProcessRuntimeStateSnapshot state;
    private readonly Dictionary<ProcessStepInstanceId, StepInstancePlan> plannedSteps;
    private readonly Dictionary<ProcessStepInstanceId, ProcessRuntimeStepState> runtimeSteps;
    private readonly ILookup<ProcessStepDefinitionId, BranchRoutePlan> branchRoutes;

    internal ProcessAcceptedStepRequirementsCapture(ProcessRuntimeCommitRequest initial) {
        var plan = initial.InitialPlan ?? throw Invalid();
        state = initial.Mutation.State;
        if (state.PlanId != plan.Header.PlanId || state.PlanHash != plan.PlanHash ||
                plan.Steps is null || state.Steps is null || plan.Branches?.Routes is null ||
                plan.Steps.Any(item => item is null) || state.Steps.Any(item => item is null) ||
                plan.Branches.Routes.Any(item => item is null) ||
                plan.Steps.Select(item => item.StepInstanceId).Distinct().Count() != plan.Steps.Count ||
                state.Steps.Select(item => item.StepInstanceId).Distinct().Count() != state.Steps.Count) {
            throw Invalid();
        }
        plannedSteps = plan.Steps.ToDictionary(item => item.StepInstanceId);
        runtimeSteps = state.Steps.ToDictionary(item => item.StepInstanceId);
        branchRoutes = plan.Branches.Routes.ToLookup(item => item.BranchStepId);
    }

    internal ProcessAcceptedStepRequirements Capture(ProcessRuntimeStepAssignment assignment) {
        if (assignment.RunId != state.RunId || assignment.PlanId != state.PlanId ||
                !plannedSteps.TryGetValue(assignment.StepInstanceId, out var planned) ||
                !runtimeSteps.TryGetValue(assignment.StepInstanceId, out var runtime) ||
                planned.StepDefinitionId != runtime.StepDefinitionId || planned.StepKey != assignment.StepKey ||
                !planned.IsExecutable || !runtime.IsExecutable ||
                !SameSlots(assignment.RequiredArtifactSlotIds, runtime.RequiredArtifactSlots) ||
                !SameSlots(assignment.ProducedArtifactSlotIds, runtime.ProducedArtifactSlots) ||
                !ProcessRequiredRuntimeToolNames.IsValidBoundedContract(planned.RequiredRuntimeToolNames) ||
                !ProcessRequiredRuntimeToolNames.IsValidBoundedContract(runtime.RequiredRuntimeToolNames) ||
                planned.RequiredHostCapabilities is null || runtime.RequiredHostCapabilities is null ||
                !planned.RequiredHostCapabilities.IsSubsetOf(runtime.RequiredHostCapabilities) ||
                planned.RequiredRuntimeToolNames.Except(runtime.RequiredRuntimeToolNames, StringComparer.OrdinalIgnoreCase).Any()) {
            throw Invalid();
        }
        ProcessAcceptedStepRequirements requirements = new(planned.StepDefinitionId, planned.Kind, planned.StartsSubprocess,
            runtime.RequiredRuntimeToolNames.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            runtime.RequiredHostCapabilities.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
            branchRoutes[planned.StepDefinitionId].OrderBy(item => item.FamilyId.Value, StringComparer.Ordinal)
                .ThenBy(item => item.OutcomeId.Value, StringComparer.Ordinal).Select(item => item.OutcomeId).ToArray(),
            runtime.ArtifactDescriptors, runtime.SubprocessArtifactMappings);
        requirements.Validate();
        return requirements;
    }

    private static bool SameSlots(IReadOnlyList<ArtifactSlotId>? assignment, IReadOnlySet<ArtifactSlotId>? runtime)
        => assignment is not null && runtime is not null &&
            assignment.Count <= ProcessAcceptedStepRequirements.MaximumDeclarations &&
            assignment.Count == runtime.Count && runtime.SetEquals(assignment);

    private static InvalidDataException Invalid()
        => new("The original Process step requirements differ from their prepared plan, runtime or assignment.");
}
