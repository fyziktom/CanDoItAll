using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Core;

public sealed record ProcessBranchDefinition(
    ProcessStepDefinitionId StepId,
    BranchFamilyId FamilyId,
    IReadOnlyList<BranchInputRequirement> InputRequirements,
    IReadOnlyList<BranchOutcomeDefinition> Outcomes);

public sealed record BranchInputRequirement(
    string Key,
    BranchInputRequirementKind Kind,
    bool IsRequired);

public sealed record BranchOutcomeDefinition(
    BranchOutcomeId Id,
    string DisplayLabel,
    BranchOutcomeCategory Category,
    ProcessRouteTarget RouteTarget,
    LoopBudgetDefinition? LoopBudget = null);

public sealed record ProcessRouteTarget(
    ProcessRouteTargetKind Kind,
    ProcessStepDefinitionId? StepId = null);

public sealed record LoopBudgetDefinition(
    int MaximumRepeats,
    LoopFingerprintPolicyId FingerprintPolicyId,
    ProcessRouteTarget EscalationTarget);

public enum BranchInputRequirementKind
{
    Artifact,
    StepResult,
    Incident,
    Metric,
    UserDecision,
    DriverFacet
}

public enum BranchOutcomeCategory
{
    Continue,
    Wait,
    Repeat,
    Escalate,
    Complete,
    Fail,
    Cancel
}

public enum ProcessRouteTargetKind
{
    NextStep,
    SpecificStep,
    PreviousStep,
    SubprocessStart,
    SubprocessResume,
    WaitForArtifact,
    WaitForUser,
    Escalate,
    CompleteRun,
    FailRun,
    CancelRun
}

public static class ProcessBranchRules
{
    public static ProcessValidationResult Validate(IReadOnlyList<ProcessBranchDefinition> branches)
    {
        ArgumentNullException.ThrowIfNull(branches);

        var failures = new List<ProcessValidationFailure>();
        foreach (var branch in branches)
        {
            ValidateBranch(branch, failures);
        }

        return ProcessValidationResult.From(failures);
    }

    private static void ValidateBranch(
        ProcessBranchDefinition branch,
        ICollection<ProcessValidationFailure> failures)
    {
        if (branch.Outcomes.Count == 0)
        {
            failures.Add(new ProcessValidationFailure(
                "Branch.NoOutcomes",
                $"Branch '{branch.FamilyId}' must define at least one outcome."));
        }

        var requirementKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requirement in branch.InputRequirements)
        {
            if (string.IsNullOrWhiteSpace(requirement.Key))
            {
                failures.Add(new ProcessValidationFailure(
                    "BranchInput.EmptyKey",
                    $"Branch '{branch.FamilyId}' has an input requirement with an empty key."));
            }
            else if (!requirementKeys.Add(requirement.Key))
            {
                failures.Add(new ProcessValidationFailure(
                    "BranchInput.DuplicateKey",
                    $"Branch '{branch.FamilyId}' has duplicate input requirement key '{requirement.Key}'."));
            }
        }

        var outcomeIds = new HashSet<BranchOutcomeId>();
        foreach (var outcome in branch.Outcomes)
        {
            if (!outcomeIds.Add(outcome.Id))
            {
                failures.Add(new ProcessValidationFailure(
                    "BranchOutcome.DuplicateId",
                    $"Branch '{branch.FamilyId}' has duplicate outcome id '{outcome.Id}'."));
            }

            ValidateRouteTarget(branch, outcome, failures);
        }
    }

    private static void ValidateRouteTarget(
        ProcessBranchDefinition branch,
        BranchOutcomeDefinition outcome,
        ICollection<ProcessValidationFailure> failures)
    {
        if (RequiresStep(outcome.RouteTarget.Kind) && outcome.RouteTarget.StepId is null)
        {
            failures.Add(new ProcessValidationFailure(
                "BranchRoute.MissingStepTarget",
                $"Branch outcome '{outcome.Id}' must declare a step target."));
        }

        if (IsBackwardRoute(outcome.RouteTarget.Kind) && outcome.LoopBudget is null)
        {
            failures.Add(new ProcessValidationFailure(
                "BranchRoute.BackwardMissingBudget",
                $"Branch outcome '{outcome.Id}' on branch '{branch.FamilyId}' must declare a loop budget."));
        }

        if (outcome.LoopBudget is { MaximumRepeats: <= 0 })
        {
            failures.Add(new ProcessValidationFailure(
                "BranchRoute.InvalidLoopBudget",
                $"Branch outcome '{outcome.Id}' loop budget must be greater than zero."));
        }
    }

    private static bool RequiresStep(ProcessRouteTargetKind kind)
    {
        return kind is ProcessRouteTargetKind.SpecificStep or
            ProcessRouteTargetKind.SubprocessStart or
            ProcessRouteTargetKind.SubprocessResume;
    }

    private static bool IsBackwardRoute(ProcessRouteTargetKind kind)
    {
        return kind == ProcessRouteTargetKind.PreviousStep;
    }
}
