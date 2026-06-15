using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Builder;

public sealed partial class ProcessInstancePlanCompiler
{
    private static IReadOnlyDictionary<StrategyId, ResolvedStrategyDescriptor> BuildStrategyIndex(
        IReadOnlyList<ProcessDriverDescriptor> selectedDrivers,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        var index = new Dictionary<StrategyId, ResolvedStrategyDescriptor>();
        foreach (var driver in selectedDrivers)
        {
            foreach (var strategy in driver.Strategies)
            {
                var resolved = new ResolvedStrategyDescriptor(driver, strategy);
                if (!index.TryAdd(strategy.StrategyId, resolved))
                {
                    diagnostics.Add(Error(
                        "Builder.StrategyAmbiguous",
                        $"Strategy '{strategy.StrategyId}' is provided by multiple selected drivers."));
                }
            }
        }

        return index;
    }

    private static IReadOnlyList<StepInstancePlan> BuildStepPlans(
        ProcessInstancePlanCompileRequest request,
        IReadOnlyDictionary<StrategyId, ResolvedStrategyDescriptor> strategies,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        var subprocessStepIds = request.Subprocesses
            .Where(subprocess => IsOwnedByDefinition(subprocess, request.Definition))
            .Select(subprocess => subprocess.ParentStepDefinitionId)
            .ToHashSet();
        var steps = new List<StepInstancePlan>();
        foreach (var step in request.Definition.Steps)
        {
            var isExecutable = step.Kind is not ProcessStepKind.Start and not ProcessStepKind.End;
            ProcessStrategyBindingSnapshot? binding = null;
            if (isExecutable)
            {
                TryBindRequiredStrategy(
                    step.StrategyId,
                    GetExpectedStrategyKind(step.Kind),
                    strategies,
                    diagnostics,
                    $"step '{step.Key}'",
                    out binding);
            }

            steps.Add(new StepInstancePlan(
                ProcessStepInstanceId.New(),
                step.Id,
                step.Key,
                step.Kind,
                isExecutable,
                subprocessStepIds.Contains(step.Id),
                binding));
        }

        return steps;
    }

    private static ProcessStrategyKind GetExpectedStrategyKind(ProcessStepKind stepKind)
    {
        return stepKind == ProcessStepKind.Branch
            ? ProcessStrategyKind.BranchDecision
            : ProcessStrategyKind.StepExecution;
    }

    private static ManagerPlan BuildManagerPlan(
        ProcessInstancePlanCompileRequest request,
        IReadOnlyDictionary<StrategyId, ResolvedStrategyDescriptor> strategies,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        ProcessStrategyBindingSnapshot? managerBinding = null;
        if (request.Manager.ManagerStrategyId is { } managerStrategyId)
        {
            TryBindRequiredStrategy(
                managerStrategyId,
                ProcessStrategyKind.ManagerDecision,
                strategies,
                diagnostics,
                "manager",
                out managerBinding);
        }

        var recoveryBindings = BindStrategyList(
            request.Manager.RecoveryStrategyIds,
            ProcessStrategyKind.ArtifactRecovery,
            strategies,
            diagnostics,
            "recovery");
        var resupplyBindings = BindStrategyList(
            request.Manager.ResupplyStrategyIds,
            ProcessStrategyKind.ArtifactResupply,
            strategies,
            diagnostics,
            "resupply");

        return new ManagerPlan(
            request.Manager.PolicyHash,
            managerBinding,
            recoveryBindings,
            resupplyBindings);
    }

    private static IReadOnlyList<ProcessStrategyBindingSnapshot> BindStrategyList(
        IEnumerable<StrategyId> strategyIds,
        ProcessStrategyKind expectedKind,
        IReadOnlyDictionary<StrategyId, ResolvedStrategyDescriptor> strategies,
        ICollection<ProcessBuildDiagnostic> diagnostics,
        string source)
    {
        var bindings = new List<ProcessStrategyBindingSnapshot>();
        foreach (var strategyId in strategyIds)
        {
            if (TryBindRequiredStrategy(
                strategyId,
                expectedKind,
                strategies,
                diagnostics,
                source,
                out var binding))
            {
                bindings.Add(binding);
            }
        }

        return bindings;
    }

    private static bool TryBindRequiredStrategy(
        StrategyId? strategyId,
        ProcessStrategyKind expectedKind,
        IReadOnlyDictionary<StrategyId, ResolvedStrategyDescriptor> strategies,
        ICollection<ProcessBuildDiagnostic> diagnostics,
        string source,
        out ProcessStrategyBindingSnapshot binding)
    {
        binding = default!;
        if (strategyId is null)
        {
            diagnostics.Add(Error(
                "Builder.StrategyMissing",
                $"A strategy binding is required for {source}."));
            return false;
        }

        if (!strategies.TryGetValue(strategyId.Value, out var resolved))
        {
            diagnostics.Add(Error(
                "Builder.StrategyUnavailable",
                $"Strategy '{strategyId}' required for {source} is not available from the selected driver stack."));
            return false;
        }

        if (resolved.Strategy.Kind != expectedKind)
        {
            diagnostics.Add(Error(
                "Builder.StrategyKindMismatch",
                $"Strategy '{strategyId}' required for {source} has kind '{resolved.Strategy.Kind}' instead of '{expectedKind}'."));
            return false;
        }

        var inputs = new[]
        {
            new StrategyBindingInput(new StrategyBindingInputKey("source"), ProcessPlanHasher.ComputeContentHash(source)),
            new StrategyBindingInput(new StrategyBindingInputKey("strategy"), ProcessPlanHasher.ComputeContentHash(strategyId.Value.ToString()))
        };

        binding = new ProcessStrategyBindingSnapshot(
            resolved.Driver.DriverId,
            resolved.Strategy.StrategyId,
            resolved.Strategy.StrategyVersion,
            $"{BuilderBindingVersion}:{resolved.Driver.DriverVersion}",
            resolved.Driver.MinRuntimeSchema,
            resolved.Driver.MaxRuntimeSchema,
            ProcessPlanHasher.ComputeBindingInputHash(inputs),
            inputs);
        return true;
    }

    private IReadOnlyList<SubprocessInstancePlanRef> BuildSubprocessRefs(
        ProcessInstancePlanCompileRequest request,
        IReadOnlyList<DefinitionIdentity> ancestorDefinitions,
        ProcessInstancePlanId rootPlanId,
        ProcessInstancePlanId parentPlanId,
        IReadOnlyList<StepInstancePlan> parentSteps,
        int parentHierarchyDepth,
        int maximumSubprocessDepth,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        var stepByDefinitionId = parentSteps.ToDictionary(step => step.StepDefinitionId);
        var refs = new List<SubprocessInstancePlanRef>();
        foreach (var subprocess in request.Subprocesses.Where(subprocess => IsOwnedByDefinition(subprocess, request.Definition)))
        {
            if (!stepByDefinitionId.TryGetValue(subprocess.ParentStepDefinitionId, out var parentStep))
            {
                diagnostics.Add(Error(
                    "Builder.SubprocessParentStepMissing",
                    $"Subprocess parent step '{subprocess.ParentStepDefinitionId}' does not exist."));
                continue;
            }

            var childPlanId = ProcessInstancePlanId.New();
            var childRequest = new ProcessInstancePlanCompileRequest(
                subprocess.ChildSource,
                request.Subprocesses,
                request.MaximumSubprocessDepth);
            var childResult = Compile(
                childRequest,
                ancestorDefinitions,
                rootPlanId,
                parentPlanId,
                parentStep.StepInstanceId,
                childPlanId,
                parentHierarchyDepth + 1,
                maximumSubprocessDepth);
            if (!childResult.Succeeded || childResult.Plan is null)
            {
                foreach (var diagnostic in childResult.Diagnostics)
                {
                    diagnostics.Add(diagnostic with
                    {
                        Source = $"subprocess:{parentStep.StepKey}"
                    });
                }

                continue;
            }

            refs.Add(new SubprocessInstancePlanRef(
                parentStep.StepInstanceId,
                childPlanId,
                childResult.Plan.PlanHash,
                childResult.Plan.Header.HierarchyDepth,
                subprocess.ParentToChildArtifactProjectionHash,
                subprocess.ChildToParentArtifactProjectionHash,
                subprocess.CancellationPolicyHash,
                subprocess.EscalationPolicyHash));
        }

        return refs;
    }

    private static bool IsOwnedByDefinition(
        SubprocessCompileRequest subprocess,
        ProcessDefinitionKernel definition)
    {
        return subprocess.ParentDefinitionId == definition.DefinitionId &&
            subprocess.ParentDefinitionVersionId == definition.VersionId;
    }

    private static DriverStackSnapshot BuildDriverStack(IReadOnlyList<ProcessDriverDescriptor> selectedDrivers)
    {
        return new DriverStackSnapshot(selectedDrivers
            .Select(driver => new ResolvedDriverSnapshot(
                driver.DriverId,
                driver.DriverVersion,
                driver.Layer,
                driver.MinRuntimeSchema,
                driver.MaxRuntimeSchema,
                driver.CapabilityTags))
            .ToArray());
    }

    private static StrategyBindingSet BuildStrategyBindingSet(
        IReadOnlyList<StepInstancePlan> steps,
        ManagerPlan managerPlan)
    {
        return new StrategyBindingSet(
            steps
                .Select(step => step.ExecutionStrategyBinding)
                .Where(binding => binding is not null)
                .Select(binding => binding!)
                .ToArray(),
            managerPlan.ManagerStrategyBinding is null ? [] : [managerPlan.ManagerStrategyBinding],
            managerPlan.RecoveryBindings,
            managerPlan.ResupplyBindings);
    }

    private static ArtifactPlan BuildArtifactPlan(ProcessInstancePlanCompileRequest request)
    {
        return new ArtifactPlan(
            request.Definition.ArtifactSlots
                .Select(slot => new ArtifactSlotPlan(
                    slot.Id,
                    slot.Key,
                    slot.ArtifactDefinitionId,
                    slot.RequirementMode,
                    slot.Scope))
                .ToArray(),
            request.InitialArtifactReferences
                .Select(reference => new ArtifactLedgerSeed(
                    reference.SlotId,
                    reference.ArtifactId,
                    reference.Scope,
                    reference.ContentHash))
                .ToArray());
    }

    private static BranchRouteTable BuildBranchRouteTable(ProcessInstancePlanCompileRequest request)
    {
        return new BranchRouteTable(request.Definition.Branches
            .SelectMany(branch => branch.Outcomes.Select(outcome => new BranchRoutePlan(
                branch.StepId,
                branch.FamilyId,
                outcome.Id,
                outcome.Category,
                outcome.RouteTarget,
                outcome.LoopBudget is null
                    ? null
                    : new LoopBudgetPlan(
                        $"branch:{branch.FamilyId}:{outcome.Id}",
                        outcome.LoopBudget.MaximumRepeats,
                        outcome.LoopBudget.FingerprintPolicyId,
                        outcome.LoopBudget.EscalationTarget))))
            .ToArray());
    }

    private static BudgetPlan BuildBudgetPlan(ProcessInstancePlanCompileRequest request)
    {
        var loopBudgets = new List<LoopBudgetPlan>();
        loopBudgets.AddRange(request.Definition.Edges
            .Where(edge => edge.IsBackwardRoute && edge.LoopBudget is not null)
            .Select(edge => new LoopBudgetPlan(
                $"edge:{edge.SourceId}:{edge.TargetId}",
                edge.LoopBudget!.MaximumRepeats,
                edge.LoopBudget.FingerprintPolicyId,
                edge.LoopBudget.EscalationTarget)));
        loopBudgets.AddRange(request.Definition.Branches
            .SelectMany(branch => branch.Outcomes)
            .Where(outcome => outcome.LoopBudget is not null)
            .Select(outcome => new LoopBudgetPlan(
                $"branch:{outcome.Id}",
                outcome.LoopBudget!.MaximumRepeats,
                outcome.LoopBudget.FingerprintPolicyId,
                outcome.LoopBudget.EscalationTarget)));

        return new BudgetPlan(loopBudgets);
    }
}
