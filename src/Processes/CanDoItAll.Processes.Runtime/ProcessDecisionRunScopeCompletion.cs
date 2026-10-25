namespace CanDoItAll.Processes.Runtime;

public static class ProcessDecisionRunScopeCompletion {
    public static ProcessDecisionRunScopeCommand? Prepare(ProcessDecisionRunScopeExpectation? scope,
        ProcessRuntimeStateSnapshot original, ProcessRuntimeMutation mutation) {
        if (scope is null || original.RunId != original.RootRunId || mutation.Outcome != ProcessRuntimeTransitionOutcome.Applied ||
                !ProcessRuntimeTerminalStates.IsRunTerminal(mutation.State.Status) ||
                mutation.State.Steps.Any(step => step.IsExecutable && !ProcessRuntimeTerminalStates.IsStepTerminal(step.Status))) {
            return null;
        }
        scope.Validate();
        var prior = original.AppliedResults.Select(result => (result.StepInstanceId, result.StrategyId, result.IdempotencyKey)).ToHashSet();
        var added = mutation.State.AppliedResults.Where(result => !prior.Contains((result.StepInstanceId, result.StrategyId, result.IdempotencyKey))).ToArray();
        if (mutation.State.RunId != original.RunId || mutation.State.RootRunId != original.RootRunId || added.Length != 1 ||
                added[0].AppliedStepStatus is not (ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled) ||
                mutation.State.Steps.SingleOrDefault(step => step.StepInstanceId == added[0].StepInstanceId)?.Status != added[0].AppliedStepStatus ||
                mutation.State.Steps.Any(step => step.ActiveClaimToken is not null) ||
                mutation.State.Claims.Any(claim => claim.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) ||
                ProcessRuntimeTerminalStates.CompleteRunIfTerminal(mutation.State, mutation.State.UpdatedAtUtc).Status != mutation.State.Status) {
            throw new InvalidOperationException("Shared scope completion requires one original terminal root result without remaining runtime work.");
        }
        return new(scope, mutation.State.Status == ProcessRuntimeStatus.Completed
            ? ProcessDecisionRunScopeCommandKind.CompleteResult : ProcessDecisionRunScopeCommandKind.CancelResult);
    }

    public static void Require(ProcessRuntimeCommitRequest request) {
        if (request.DecisionRunScope is not { CompletesRuntimeResult: true } command) {
            return;
        }
        command.Validate();
        if (request.InitialPlan is not null || request.InitialAssignments is not null || request.InitialLaunchAdmission is not null ||
                request.InitialRunDecisionAdmission is not null || request.DecisionMaterial is not null ||
                request.DecisionBudget is ProcessDecisionBudgetReservation ||
                command != Prepare(command.Expectation, request.OriginalState, request.Mutation)) {
            throw new InvalidOperationException("A terminal shared scope command must accompany its exact final root result and closed work.");
        }
    }

    public static void RequireReceipt(ProcessRuntimeCommitResult result, ProcessDecisionMaterialScopeId? originalScope) {
        if (result.DecisionRunScope is not { } receipt) {
            return;
        }
        receipt.Command.Validate();
        receipt.State.Validate();
        if (!receipt.Command.CompletesRuntimeResult || receipt.Command.Expectation.ScopeId != originalScope ||
                receipt.State.Admission.ScopeId != originalScope || result.State.RunId != result.State.RootRunId ||
                receipt.State.Admission.Funding.RootRunId != result.State.RootRunId ||
                !ProcessRuntimeTerminalStates.IsRunTerminal(result.State.Status) ||
                receipt.Command.Kind != (result.State.Status == ProcessRuntimeStatus.Completed
                    ? ProcessDecisionRunScopeCommandKind.CompleteResult : ProcessDecisionRunScopeCommandKind.CancelResult) ||
                receipt.State.Phase != (result.State.Status == ProcessRuntimeStatus.Completed
                    ? ProcessDecisionRunScopePhase.Completed : ProcessDecisionRunScopePhase.Cancelled)) {
            throw new InvalidDataException("The terminal result receipt differs from its original root and shared material scope.");
        }
    }
}
