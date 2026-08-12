using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Builder;

public enum ProcessPlanHashAlgorithmVersion
{
    LegacyV1,
    HostCapabilitiesV2
}

public static class ProcessPlanHasher
{
    public const ProcessPlanHashAlgorithmVersion CurrentAlgorithmVersion =
        ProcessPlanHashAlgorithmVersion.HostCapabilitiesV2;

    public static string Compute(ProcessInstancePlan plan)
        => Compute(plan, CurrentAlgorithmVersion);

    public static string Compute(
        ProcessInstancePlan plan,
        ProcessPlanHashAlgorithmVersion algorithmVersion)
    {
        ArgumentNullException.ThrowIfNull(plan);

        bool includeHostCapabilities = algorithmVersion switch
        {
            ProcessPlanHashAlgorithmVersion.LegacyV1 => false,
            ProcessPlanHashAlgorithmVersion.HostCapabilitiesV2 => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(algorithmVersion),
                algorithmVersion,
                "Unsupported process-plan hash algorithm version.")
        };

        var builder = new StringBuilder();
        Append(builder, "schema", plan.Header.PlanSchemaVersion);
        Append(builder, "depth", plan.Header.HierarchyDepth);
        Append(builder, "definition.id", plan.Definition.DefinitionId);
        Append(builder, "definition.version", plan.Definition.VersionId);
        Append(builder, "definition.hash", plan.Definition.DefinitionContentHash);
        Append(builder, "definition.sourceSchema", plan.Definition.SourceSchemaVersion);
        Append(builder, "definition.targetSchema", plan.Definition.TargetSchemaVersion);
        AppendValues(builder, "definition.migration", plan.Definition.AppliedMigrationIds);
        foreach (var component in plan.Definition.TemplateComponents.OrderBy(component => component.Key, StringComparer.Ordinal))
        {
            Append(builder, "component.id", component.ComponentId);
            Append(builder, "component.key", component.Key);
            Append(builder, "component.version", component.ContentVersion);
            Append(builder, "component.hash", component.ContentHash);
        }

        AppendValues(builder, "override", plan.Definition.AppliedLocalOverridePointers);
        if (includeHostCapabilities)
        {
            Append(builder, "host.profile", plan.DriverStack.HostProfileId);
            foreach (var capability in plan.DriverStack.HostCapabilities.OrderBy(capability => capability.Id.Value, StringComparer.Ordinal))
            {
                AppendHostCapability(builder, "driver.host", capability);
            }
        }

        foreach (var driver in plan.DriverStack.Drivers.OrderBy(driver => driver.DriverId.Value, StringComparer.Ordinal))
        {
            Append(builder, "driver.id", driver.DriverId);
            Append(builder, "driver.version", driver.DriverVersion);
            Append(builder, "driver.layer", driver.Layer);
            Append(builder, "driver.minRuntime", driver.MinRuntimeSchema);
            Append(builder, "driver.maxRuntime", driver.MaxRuntimeSchema);
            AppendValues(builder, "driver.capability", driver.CapabilityTags.Select(tag => tag.Value).Order(StringComparer.Ordinal));
            if (includeHostCapabilities)
            {
                AppendValues(builder, "driver.hostRequirement", driver.RequiredHostCapabilities.Select(capability => capability.Value).Order(StringComparer.Ordinal));
            }
        }

        foreach (var step in plan.Steps.OrderBy(step => step.StepKey, StringComparer.Ordinal))
        {
            Append(builder, "step.definitionId", step.StepDefinitionId);
            Append(builder, "step.key", step.StepKey);
            Append(builder, "step.kind", step.Kind);
            Append(builder, "step.executable", step.IsExecutable);
            Append(builder, "step.subprocess", step.StartsSubprocess);
            if (includeHostCapabilities)
            {
                AppendValues(
                    builder,
                    "step.hostRequirement",
                    step.RequiredHostCapabilities.Select(capability => capability.Value).Order(StringComparer.Ordinal));
                AppendValues(
                    builder,
                    "step.runtimeToolRequirement",
                    step.RequiredRuntimeToolNames.Order(StringComparer.OrdinalIgnoreCase));
            }

            AppendBinding(builder, "step.binding", step.ExecutionStrategyBinding, includeHostCapabilities);
        }

        AppendBindings(builder, "execution.binding", plan.Strategies.ExecutionBindings, includeHostCapabilities);
        AppendBindings(builder, "manager.binding", plan.Strategies.ManagerBindings, includeHostCapabilities);
        AppendBindings(builder, "recovery.binding", plan.Strategies.RecoveryBindings, includeHostCapabilities);
        AppendBindings(builder, "resupply.binding", plan.Strategies.ResupplyBindings, includeHostCapabilities);
        foreach (var slot in plan.ArtifactPlan.Slots.OrderBy(slot => slot.SlotKey, StringComparer.Ordinal))
        {
            Append(builder, "artifact.slotId", slot.SlotId);
            Append(builder, "artifact.slotKey", slot.SlotKey);
            Append(builder, "artifact.definitionId", slot.ArtifactDefinitionId);
            Append(builder, "artifact.requirement", slot.RequirementMode);
            Append(builder, "artifact.scope", slot.Scope);
        }

        foreach (var ledger in plan.ArtifactPlan.InitialLedgerEntries.OrderBy(ledger => ledger.SlotId.ToString(), StringComparer.Ordinal))
        {
            Append(builder, "ledger.slotId", ledger.SlotId);
            Append(builder, "ledger.artifactId", ledger.ArtifactId);
            Append(builder, "ledger.scope", ledger.Scope);
            Append(builder, "ledger.hash", ledger.ContentHash);
        }

        foreach (var route in plan.Branches.Routes.OrderBy(route => route.OutcomeId.Value, StringComparer.Ordinal))
        {
            Append(builder, "branch.stepId", route.BranchStepId);
            Append(builder, "branch.family", route.FamilyId);
            Append(builder, "branch.outcome", route.OutcomeId);
            Append(builder, "branch.category", route.Category);
            Append(builder, "branch.target.kind", route.RouteTarget.Kind);
            Append(builder, "branch.target.step", route.RouteTarget.StepId);
            AppendLoopBudget(builder, "branch.loop", route.LoopBudget);
        }

        foreach (var subprocess in plan.Subprocesses.OrderBy(subprocess => subprocess.ChildPlanHash, StringComparer.Ordinal))
        {
            Append(builder, "subprocess.childHash", subprocess.ChildPlanHash);
            Append(builder, "subprocess.depth", subprocess.HierarchyDepth);
            Append(builder, "subprocess.parentToChild", subprocess.ParentToChildArtifactProjectionHash);
            Append(builder, "subprocess.childToParent", subprocess.ChildToParentArtifactProjectionHash);
            Append(builder, "subprocess.cancel", subprocess.CancellationPolicyHash);
            Append(builder, "subprocess.escalate", subprocess.EscalationPolicyHash);
        }

        foreach (var loopBudget in plan.Budgets.LoopBudgets.OrderBy(budget => budget.SourceKey, StringComparer.Ordinal))
        {
            AppendLoopBudget(builder, "budget.loop", loopBudget);
        }

        Append(builder, "manager.policy", plan.Manager.PolicyHash);
        AppendBinding(builder, "manager.strategy", plan.Manager.ManagerStrategyBinding, includeHostCapabilities);
        Append(builder, "monitoring.enabled", plan.Monitoring.Enabled);
        Append(builder, "monitoring.hash", plan.Monitoring.ProjectionConfigHash);
        Append(builder, "security.hash", plan.Security.GovernancePolicyHash);
        AppendValues(builder, "security.approval", plan.Security.RequiredApprovalKeys);

        return ComputeContentHash(builder.ToString());
    }

    public static string ComputeBindingInputHash(IReadOnlyList<StrategyBindingInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var builder = new StringBuilder();
        foreach (var input in inputs.OrderBy(input => input.Key.Value, StringComparer.Ordinal))
        {
            Append(builder, "input.key", input.Key);
            Append(builder, "input.hash", input.ValueHash);
        }

        return ComputeContentHash(builder.ToString());
    }

    public static string ComputeContentHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void AppendBindings(
        StringBuilder builder,
        string prefix,
        IReadOnlyList<ProcessStrategyBindingSnapshot> bindings,
        bool includeHostCapabilities)
    {
        foreach (var binding in bindings.OrderBy(binding => binding.StrategyId.Value, StringComparer.Ordinal))
        {
            AppendBinding(builder, prefix, binding, includeHostCapabilities);
        }
    }

    private static void AppendBinding(
        StringBuilder builder,
        string prefix,
        ProcessStrategyBindingSnapshot? binding,
        bool includeHostCapabilities)
    {
        if (binding is null)
        {
            Append(builder, prefix, "none");
            return;
        }

        Append(builder, prefix + ".driver", binding.DriverId);
        Append(builder, prefix + ".strategy", binding.StrategyId);
        Append(builder, prefix + ".version", binding.StrategyVersion);
        Append(builder, prefix + ".factory", binding.FactoryVersion);
        Append(builder, prefix + ".minRuntime", binding.MinRuntimeSchema);
        Append(builder, prefix + ".maxRuntime", binding.MaxRuntimeSchema);
        Append(builder, prefix + ".hash", binding.BindingInputsHash);
        if (includeHostCapabilities)
        {
            Append(builder, prefix + ".hostProfile", binding.HostProfileId);
            foreach (var capability in binding.HostCapabilities.OrderBy(capability => capability.Id.Value, StringComparer.Ordinal))
            {
                AppendHostCapability(builder, prefix + ".host", capability);
            }
        }

        foreach (var input in binding.Inputs.OrderBy(input => input.Key.Value, StringComparer.Ordinal))
        {
            Append(builder, prefix + ".input.key", input.Key);
            Append(builder, prefix + ".input.hash", input.ValueHash);
        }
    }

    private static void AppendHostCapability(
        StringBuilder builder,
        string prefix,
        ProcessHostCapabilityFact capability)
    {
        Append(builder, prefix + ".id", capability.Id);
        Append(builder, prefix + ".availability", capability.Availability);
        Append(builder, prefix + ".reason", capability.Reason);
        Append(builder, prefix + ".port", capability.ExecutionPort);
    }

    private static void AppendLoopBudget(
        StringBuilder builder,
        string prefix,
        LoopBudgetPlan? budget)
    {
        if (budget is null)
        {
            Append(builder, prefix, "none");
            return;
        }

        Append(builder, prefix + ".source", budget.SourceKey);
        Append(builder, prefix + ".maximumRepeats", budget.MaximumRepeats);
        Append(builder, prefix + ".fingerprint", budget.FingerprintPolicyId);
        Append(builder, prefix + ".escalation.kind", budget.EscalationTarget.Kind);
        Append(builder, prefix + ".escalation.step", budget.EscalationTarget.StepId);
    }

    private static void AppendValues<T>(
        StringBuilder builder,
        string key,
        IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            Append(builder, key, value);
        }
    }

    private static void Append<T>(
        StringBuilder builder,
        string key,
        T? value)
    {
        builder.Append(key);
        builder.Append('=');
        builder.Append(value?.ToString() ?? "<null>");
        builder.Append('\n');
    }
}
