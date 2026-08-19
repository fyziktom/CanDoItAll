using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Builder;

public sealed partial class ProcessInstancePlanCompiler
{
    private static void ValidateSelectedDriverHostCapabilityLimit(
        IReadOnlyList<ProcessDriverDescriptor> selectedDrivers,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        if (!ExceedsHostCapabilityLimit(
                selectedDrivers.SelectMany(driver => driver.RequiredHostCapabilities)))
        {
            return;
        }

        diagnostics.Add(Error(
            "Builder.DriverHostCapabilityLimitExceeded",
            $"The selected driver stack requires more than {MaximumEffectiveHostCapabilities} process host capabilities."));
    }

    private static void ValidateEffectiveHostCapabilityLimit(
        IReadOnlyList<ProcessDriverDescriptor> selectedDrivers,
        IReadOnlyList<StepInstancePlan> steps,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        var effectiveCapabilities = selectedDrivers
            .SelectMany(driver => driver.RequiredHostCapabilities)
            .Concat(steps.SelectMany(step => step.RequiredHostCapabilities));
        if (!ExceedsHostCapabilityLimit(effectiveCapabilities))
        {
            return;
        }

        diagnostics.Add(Error(
            "Builder.PlanHostCapabilityLimitExceeded",
            $"The effective process plan requires more than {MaximumEffectiveHostCapabilities} process host capabilities."));
    }

    private static bool ExceedsHostCapabilityLimit(IEnumerable<ProcessHostCapabilityId> capabilities)
    {
        var distinct = new HashSet<ProcessHostCapabilityId>();
        foreach (var capability in capabilities)
        {
            distinct.Add(capability);
            if (distinct.Count > MaximumEffectiveHostCapabilities)
            {
                return true;
            }
        }

        return false;
    }

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
        IReadOnlyList<ProcessDriverDescriptor> selectedDrivers,
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
            var requiredRuntimeToolNames = ProcessRequiredRuntimeToolNames
                .NormalizeDeclaredRuntimeToolNames(step.RequiredRuntimeToolNames);
            if (ProcessRequiredRuntimeToolNames.HasInvalidRuntimeToolContract(requiredRuntimeToolNames))
            {
                diagnostics.Add(Error(
                    "Builder.StepRuntimeToolContractInvalid",
                    $"Step '{step.Key}' declares an invalid or over-bound runtime-tool requirement contract."));
            }

            var requiredHostCapabilities = new HashSet<ProcessHostCapabilityId>();
            if (isExecutable)
            {
                requiredHostCapabilities.UnionWith(
                    selectedDrivers.SelectMany(driver => driver.RequiredHostCapabilities));
            }

            if (step.RequiredHostCapabilities.Count > MaximumEffectiveHostCapabilities)
            {
                diagnostics.Add(Error(
                    "Builder.StepHostCapabilityLimitExceeded",
                    $"Step '{step.Key}' declares more than {MaximumEffectiveHostCapabilities} process host capabilities."));
            }

            foreach (var value in step.RequiredHostCapabilities.Take(MaximumEffectiveHostCapabilities + 1))
            {
                if (!ProcessHostCapabilityId.TryParse(value, out var capabilityId))
                {
                    diagnostics.Add(Error(
                        "Builder.StepHostCapabilityInvalid",
                        $"Step '{step.Key}' declares an invalid process host capability id."));
                    continue;
                }

                requiredHostCapabilities.Add(capabilityId);
            }

            ProcessStrategyBindingSnapshot? binding = null;
            if (isExecutable)
            {
                if (TryBindRequiredStrategy(
                    step.StrategyId,
                    GetExpectedStrategyKind(step.Kind),
                    strategies,
                    request.CapabilityRequest.HostCapabilities,
                    diagnostics,
                    $"step '{step.Key}'",
                    out binding))
                {
                    requiredHostCapabilities.UnionWith(
                        binding.HostCapabilities.Select(capability => capability.Id));
                    if (strategies.TryGetValue(binding.StrategyId, out var resolvedStrategy))
                    {
                        requiredHostCapabilities.UnionWith(
                            resolvedStrategy.Driver.RequiredHostCapabilities);
                    }
                }
            }

            if (requiredHostCapabilities.Count > MaximumEffectiveHostCapabilities)
            {
                diagnostics.Add(Error(
                    "Builder.StepHostCapabilityLimitExceeded",
                    $"Step '{step.Key}' has more than {MaximumEffectiveHostCapabilities} effective process host capabilities across its selected driver stack, declaration, and active strategy."));
            }

            foreach (var capabilityId in requiredHostCapabilities
                         .Where(capabilityId => !request.CapabilityRequest.HostCapabilities.IsAvailable(capabilityId))
                         .OrderBy(capabilityId => capabilityId.Value, StringComparer.Ordinal)
                         .Take(MaximumEffectiveHostCapabilities + 1))
            {
                diagnostics.Add(Error(
                    "Builder.StepHostCapabilityMissing",
                    $"Step '{step.Key}' requires unavailable host capability '{capabilityId}' on profile '{request.CapabilityRequest.HostCapabilities.ProfileId}'. Configure the required host adapter or choose a compatible process strategy."));
            }

            steps.Add(new StepInstancePlan(
                ProcessStepInstanceId.New(),
                step.Id,
                step.Key,
                step.Kind,
                isExecutable,
                subprocessStepIds.Contains(step.Id),
                binding)
            {
                RequiredHostCapabilities = requiredHostCapabilities,
                RequiredRuntimeToolNames = requiredRuntimeToolNames
            });
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
                request.CapabilityRequest.HostCapabilities,
                diagnostics,
                "manager",
                out managerBinding);
        }

        var recoveryBindings = BindStrategyList(
            request.Manager.RecoveryStrategyIds,
            ProcessStrategyKind.ArtifactRecovery,
            strategies,
            request.CapabilityRequest.HostCapabilities,
            diagnostics,
            "recovery");
        var resupplyBindings = BindStrategyList(
            request.Manager.ResupplyStrategyIds,
            ProcessStrategyKind.ArtifactResupply,
            strategies,
            request.CapabilityRequest.HostCapabilities,
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
        ProcessHostCapabilitySnapshot hostCapabilities,
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
                hostCapabilities,
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
        ProcessHostCapabilitySnapshot hostCapabilities,
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

        if (resolved.Strategy.RequiredHostCapabilities.Count > MaximumEffectiveHostCapabilities)
        {
            diagnostics.Add(Error(
                "Builder.StrategyHostCapabilityLimitExceeded",
                $"Strategy '{strategyId}' required for {source} declares more than {MaximumEffectiveHostCapabilities} process host capabilities."));
            return false;
        }

        var requiredHostCapabilities = resolved.Strategy.RequiredHostCapabilities
            .OrderBy(capability => capability.Value, StringComparer.Ordinal)
            .ToArray();
        if (requiredHostCapabilities.Length > 0 &&
            expectedKind is not ProcessStrategyKind.StepExecution and not ProcessStrategyKind.BranchDecision)
        {
            diagnostics.Add(Error(
                "Builder.NonExecutionStrategyHostCapabilitiesUnsupported",
                $"Strategy '{strategyId}' required for {source} declares host capabilities, but runtime revalidation is currently supported only for executable step and branch strategies."));
            return false;
        }

        var missingHostCapabilities = requiredHostCapabilities
            .Where(capability => !hostCapabilities.IsAvailable(capability))
            .ToArray();
        foreach (var missingHostCapability in missingHostCapabilities)
        {
            var reason = hostCapabilities.TryGet(missingHostCapability, out var fact)
                ? fact!.Reason.ToString()
                : "NotReported";
            diagnostics.Add(Error(
                "Builder.StrategyHostCapabilityMissing",
                $"Strategy '{strategyId}' required for {source} cannot use host capability '{missingHostCapability}' on profile '{hostCapabilities.ProfileId}' ({reason}). Choose a compatible strategy or configure the required host adapter."));
        }

        if (missingHostCapabilities.Length > 0)
        {
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
            ProcessStrategyBindingVersions.ForDriver(resolved.Driver.DriverVersion),
            resolved.Driver.MinRuntimeSchema,
            resolved.Driver.MaxRuntimeSchema,
            ProcessPlanHasher.ComputeBindingInputHash(inputs),
            inputs)
        {
            HostProfileId = hostCapabilities.ProfileId,
            HostCapabilities = requiredHostCapabilities
                .Select(capability =>
                {
                    hostCapabilities.TryGet(capability, out var fact);
                    return fact!;
                })
                .ToArray()
        };
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

    private static DriverStackSnapshot BuildDriverStack(
        IReadOnlyList<ProcessDriverDescriptor> selectedDrivers,
        IReadOnlyList<StepInstancePlan> steps,
        ProcessHostCapabilitySnapshot hostCapabilities)
    {
        var requiredHostCapabilities = selectedDrivers
            .SelectMany(driver => driver.RequiredHostCapabilities)
            .Concat(steps.SelectMany(step => step.RequiredHostCapabilities))
            .Distinct()
            .OrderBy(capability => capability.Value, StringComparer.Ordinal)
            .ToArray();
        return new DriverStackSnapshot(selectedDrivers
            .Select(driver => new ResolvedDriverSnapshot(
                driver.DriverId,
                driver.DriverVersion,
                driver.Layer,
                driver.MinRuntimeSchema,
                driver.MaxRuntimeSchema,
                driver.CapabilityTags)
            {
                RequiredHostCapabilities = driver.RequiredHostCapabilities
            })
            .ToArray())
        {
            HostProfileId = hostCapabilities.ProfileId,
            HostCapabilities = requiredHostCapabilities
                .Select(capability =>
                {
                    hostCapabilities.TryGet(capability, out var fact);
                    return fact!;
                })
                .ToArray()
        };
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
