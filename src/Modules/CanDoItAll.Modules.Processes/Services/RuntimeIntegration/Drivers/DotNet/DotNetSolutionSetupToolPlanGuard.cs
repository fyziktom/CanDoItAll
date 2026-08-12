using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal enum DotNetSolutionSetupToolPlanKind
{
    CreateProject,
    AddTestProject,
    RepairSolutionSetup
}

internal sealed record DotNetSolutionSetupExecutionPlan(
    string PlanKey,
    string ScriptRef,
    string WorkspaceAlias,
    bool RequiresScaffold);

internal sealed record DotNetSolutionSetupToolPlan(
    DotNetSolutionSetupToolPlanKind Kind,
    string PlanKey,
    string ScriptRefVariableName,
    string ScriptVariableName,
    string SideEffectManifestVariableName,
    string ExecutionPlanVariableName,
    IReadOnlyList<string> RequiredReceipts,
    IReadOnlyList<string> RequiredPaths,
    IReadOnlyList<ProcessToolOperationExecutionPolicy> OperationPolicies,
    string ScriptRef,
    string Script,
    string SideEffectManifest,
    string ExecutionPlan);

internal sealed record ProcessRuntimeToolPlanGuardIssue(
    string Code,
    string SafeSummary,
    string Evidence);

internal sealed record DotNetSolutionSetupToolPlanGuardResult(
    DotNetSolutionSetupToolPlan? Plan,
    IReadOnlyList<ProcessRuntimeToolPlanGuardIssue> Issues)
{
    public static DotNetSolutionSetupToolPlanGuardResult Satisfied { get; } = new(null, []);

    public bool IsSatisfied => Issues.Count == 0;
}

internal static class DotNetSolutionSetupToolPlanGuard
{
    private const string WorkspacePwshRunScript = "workspace_pwsh_run_script";
    private const string WorkspaceDotnetNew = "workspace_dotnet_new";
    private const string WorkspaceWriteFile = "workspace_write_file";
    private const string WorkspaceReadFile = "workspace_read_file";
    private const string RunHelperScriptOperationKey = "run-helper-script";
    private const string CreateSolutionOperationKey = "create-solution";
    private const string CreateAppProjectOperationKey = "create-app-project";
    private const string WriteHelperScriptOperationKey = "write-helper-script";
    private const string SolutionMembershipReadbackOperationKey = "solution-membership-readback";
    private const string ProjectReferenceReadbackOperationKey = "project-reference-readback";
    private const string ProductMutationMode = "ProductMutation";
    private const string CreateProjectPlanKey = "dotnet.create-project";
    private const string AddTestProjectPlanKey = "dotnet.add-test-project";
    private const string RepairSolutionSetupPlanKey = "dotnet.repair-solution-setup";
    private const string CreateProjectPlanKind = "DotNetSolutionCreate";
    private const string AddTestProjectPlanKind = "DotNetSolutionAddTestProject";
    private const string RepairSolutionSetupPlanKind = "DotNetSolutionRepair";

    public static DotNetSolutionSetupToolPlanGuardResult Evaluate(
        ProcessRuntimeStepAssignment assignment,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(physicalPathPolicyFactory);

        var selectsDotNetSetupExecutor =
            ProcessRuntimeLaunchVariables.TryReadProcessStepRuntimeOwnedExecutorKey(
                assignment.LaunchVariables,
                out var executorKey) &&
            string.Equals(executorKey, DotNetSolutionSetupRuntimeExecutor.DriverKey, StringComparison.OrdinalIgnoreCase);

        if (DotNetSolutionProvisioningModeReader.TryRead(
                assignment.LaunchVariables,
                out var provisioningMode,
                out var provisioningIssue))
        {
            if (provisioningMode == DotNetSolutionProvisioningMode.VerifyExisting)
            {
                return DotNetExistingSolutionVerifier.TryResolveInputs(
                    assignment.LaunchVariables,
                    physicalPathPolicyFactory,
                    out _,
                    out var verificationIssue)
                    ? DotNetSolutionSetupToolPlanGuardResult.Satisfied
                    : new DotNetSolutionSetupToolPlanGuardResult(
                        null,
                        [new ProcessRuntimeToolPlanGuardIssue(
                            "dotnet.existing_solution.context_invalid",
                            $"Step '{assignment.StepKey}' declares an invalid existing .NET solution context: {verificationIssue}",
                            $"{assignment.RunId}:{assignment.StepInstanceId}:dotnet.existing_solution.context_invalid:{verificationIssue}")]);
            }
        }
        else if (selectsDotNetSetupExecutor && !string.IsNullOrWhiteSpace(provisioningIssue))
        {
            return new DotNetSolutionSetupToolPlanGuardResult(
                null,
                [new ProcessRuntimeToolPlanGuardIssue(
                    "dotnet.solution_context.provisioning_mode_invalid",
                    $"Step '{assignment.StepKey}' declares an invalid .NET solution provisioning mode: {provisioningIssue}",
                    $"{assignment.RunId}:{assignment.StepInstanceId}:dotnet.solution_context.provisioning_mode_invalid:{provisioningIssue}")]);
        }

        if (!ProcessRuntimeLaunchVariables.TryReadProcessStepScriptHelperDescriptor(
                assignment.LaunchVariables,
                out var descriptor))
        {
            return selectsDotNetSetupExecutor
                ? new DotNetSolutionSetupToolPlanGuardResult(
                    null,
                    [CreateMissingDescriptorIssue(assignment)])
                : DotNetSolutionSetupToolPlanGuardResult.Satisfied;
        }

        if (!TryResolvePlan(assignment, descriptor, out var plan))
        {
            return selectsDotNetSetupExecutor
                ? new DotNetSolutionSetupToolPlanGuardResult(
                    null,
                    [CreateInvalidDescriptorIssue(assignment, descriptor)])
                : DotNetSolutionSetupToolPlanGuardResult.Satisfied;
        }

        var issues = new List<ProcessRuntimeToolPlanGuardIssue>();
        ValidateOperationPolicies(assignment, plan, issues);
        ValidateRequiredReceipts(assignment, plan, issues);
        ValidateScriptRef(assignment, plan, issues);
        ValidateScript(assignment, plan, issues);
        ValidateManifest(assignment, plan, physicalPathPolicyFactory, issues);
        ValidateExecutionPlan(assignment, plan, issues);
        ValidateRequiredPaths(assignment, plan, physicalPathPolicyFactory, issues);
        ValidateReadbackChecks(assignment, physicalPathPolicyFactory, issues);

        return issues.Count == 0
            ? new DotNetSolutionSetupToolPlanGuardResult(plan, [])
            : new DotNetSolutionSetupToolPlanGuardResult(plan, issues);
    }

    private static void ValidateOperationPolicies(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (plan.OperationPolicies.Count == 0)
        {
            return;
        }

        var duplicateKey = plan.OperationPolicies
            .Where(policy => !string.IsNullOrWhiteSpace(policy.OperationKey))
            .GroupBy(policy => policy.OperationKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateKey is not null)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.operation_policy_duplicate",
                $"Step '{assignment.StepKey}' declares deterministic operation policy '{duplicateKey.Key}' more than once.",
                assignment,
                duplicateKey.Key);
            return;
        }

        if (plan.OperationPolicies.Any(policy =>
                string.IsNullOrWhiteSpace(policy.OperationKey) ||
                string.IsNullOrWhiteSpace(policy.ToolName)))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.operation_policy_invalid",
                $"Step '{assignment.StepKey}' declares an incomplete deterministic operation policy.",
                assignment,
                plan.PlanKey);
            return;
        }

        (string Key, string Tool)[] expectedOperations = plan.Kind switch
        {
            DotNetSolutionSetupToolPlanKind.CreateProject =>
            [
                (CreateSolutionOperationKey, WorkspaceDotnetNew),
                (CreateAppProjectOperationKey, WorkspaceDotnetNew),
                (WriteHelperScriptOperationKey, WorkspaceWriteFile),
                (RunHelperScriptOperationKey, WorkspacePwshRunScript),
                (SolutionMembershipReadbackOperationKey, WorkspaceReadFile)
            ],
            DotNetSolutionSetupToolPlanKind.AddTestProject or
                DotNetSolutionSetupToolPlanKind.RepairSolutionSetup =>
            [
                (WriteHelperScriptOperationKey, WorkspaceWriteFile),
                (RunHelperScriptOperationKey, WorkspacePwshRunScript),
                (SolutionMembershipReadbackOperationKey, WorkspaceReadFile),
                (ProjectReferenceReadbackOperationKey, WorkspaceReadFile)
            ],
            _ => []
        };
        if (plan.OperationPolicies.Count != expectedOperations.Length ||
            expectedOperations.Any(expected =>
                !plan.OperationPolicies.Any(policy =>
                    string.Equals(
                        policy.OperationKey,
                        expected.Key,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        policy.ToolName,
                        expected.Tool,
                        StringComparison.OrdinalIgnoreCase))))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.operation_policy_coverage_invalid",
                $"Step '{assignment.StepKey}' operation policies must exactly cover the deterministic '{plan.Kind}' plan.",
                assignment,
                $"{plan.PlanKey}:{string.Join(",", plan.OperationPolicies.Select(policy => $"{policy.OperationKey}={policy.ToolName}").Order(StringComparer.OrdinalIgnoreCase))}");
            return;
        }

        var helperPolicies = plan.OperationPolicies
            .Where(policy => string.Equals(
                policy.OperationKey,
                RunHelperScriptOperationKey,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (helperPolicies.Length != 1 ||
            !string.Equals(
                helperPolicies[0].ToolName,
                WorkspacePwshRunScript,
                StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.helper_operation_policy_invalid",
                $"Step '{assignment.StepKey}' must bind exactly one '{RunHelperScriptOperationKey}' policy to '{WorkspacePwshRunScript}'.",
                assignment,
                plan.PlanKey);
            return;
        }

        if (plan.OperationPolicies.Any(policy =>
                !string.Equals(
                    policy.OperationKey,
                    RunHelperScriptOperationKey,
                    StringComparison.OrdinalIgnoreCase) &&
                policy.FailureReconciliation !=
                    ProcessToolOperationFailureReconciliationPolicy.None))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.operation_reconciliation_scope_invalid",
                $"Step '{assignment.StepKey}' may declare failed-command reconciliation only for '{RunHelperScriptOperationKey}'.",
                assignment,
                plan.PlanKey);
            return;
        }

        if (plan.OperationPolicies.Any(policy =>
                policy.Idempotency != ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.operation_not_repeatable",
                $"Step '{assignment.StepKey}' deterministic .NET setup plan must explicitly declare every operation current-run repeatable.",
                assignment,
                plan.PlanKey);
        }
    }

    private static bool TryResolvePlan(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeScriptHelperDescriptor descriptor,
        out DotNetSolutionSetupToolPlan plan)
    {
        plan = null!;
        if (!TryMapPlanKind(descriptor.PlanKey, descriptor.PlanKind, out var kind))
        {
            return false;
        }

        var planKey = NormalizeDescriptorValue(descriptor.PlanKey);
        var scriptRefVariableName = NormalizeDescriptorValue(descriptor.ScriptRefVariableName);
        var scriptVariableName = NormalizeDescriptorValue(descriptor.ScriptVariableName);
        var manifestVariableName = NormalizeDescriptorValue(descriptor.ManifestVariableName);
        var executionPlanVariableName = NormalizeDescriptorValue(descriptor.ExecutionPlanVariableName);
        plan = new DotNetSolutionSetupToolPlan(
            kind,
            planKey,
            scriptRefVariableName,
            scriptVariableName,
            manifestVariableName,
            executionPlanVariableName,
            ReadStringList(
                assignment.LaunchVariables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts),
            ReadStringList(
                assignment.LaunchVariables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths),
            descriptor.OperationPolicies ?? [],
            ReadLaunchVariable(assignment.LaunchVariables, scriptRefVariableName),
            ReadLaunchVariable(assignment.LaunchVariables, scriptVariableName),
            ReadLaunchVariable(assignment.LaunchVariables, manifestVariableName),
            ReadLaunchVariable(assignment.LaunchVariables, executionPlanVariableName));
        if (!assignment.LaunchVariables.ContainsKey(
                ProcessRuntimeLaunchVariables.ProcessStepDeterministicToolPlanDescriptorJson))
        {
            return true;
        }

        if (!ProcessRuntimeLaunchVariables.TryReadProcessStepDeterministicToolPlanDescriptor(
                assignment.LaunchVariables,
                out var deterministicDescriptor) ||
            !string.Equals(
                deterministicDescriptor.PlanKey,
                planKey,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                deterministicDescriptor.PlanKind,
                descriptor.PlanKind,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                deterministicDescriptor.ExecutionPlanVariableName,
                executionPlanVariableName,
                StringComparison.Ordinal))
        {
            plan = null!;
            return false;
        }

        plan = plan with
        {
            OperationPolicies = deterministicDescriptor.OperationPolicies
        };
        return true;
    }

    private static string NormalizeDescriptorValue(string? value)
        => value?.Trim() ?? string.Empty;

    private static bool TryMapPlanKind(
        string planKey,
        string planKind,
        out DotNetSolutionSetupToolPlanKind kind)
    {
        var normalizedPlanKey = planKey?.Trim() ?? string.Empty;
        var normalizedPlanKind = planKind?.Trim() ?? string.Empty;
        if (string.Equals(normalizedPlanKey, CreateProjectPlanKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalizedPlanKind, CreateProjectPlanKind, StringComparison.OrdinalIgnoreCase))
        {
            kind = DotNetSolutionSetupToolPlanKind.CreateProject;
            return true;
        }

        if (string.Equals(normalizedPlanKey, AddTestProjectPlanKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalizedPlanKind, AddTestProjectPlanKind, StringComparison.OrdinalIgnoreCase))
        {
            kind = DotNetSolutionSetupToolPlanKind.AddTestProject;
            return true;
        }

        if (string.Equals(normalizedPlanKey, RepairSolutionSetupPlanKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalizedPlanKind, RepairSolutionSetupPlanKind, StringComparison.OrdinalIgnoreCase))
        {
            kind = DotNetSolutionSetupToolPlanKind.RepairSolutionSetup;
            return true;
        }

        kind = default;
        return false;
    }

    private static ProcessRuntimeToolPlanGuardIssue CreateInvalidDescriptorIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeScriptHelperDescriptor descriptor)
        => new(
            "dotnet.setup.plan.descriptor_invalid",
            $"Step '{assignment.StepKey}' declares an unsupported .NET solution setup plan descriptor '{descriptor.PlanKey}' / '{descriptor.PlanKind}'.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:dotnet.setup.plan.descriptor_invalid:{descriptor.PlanKey}:{descriptor.PlanKind}");

    private static ProcessRuntimeToolPlanGuardIssue CreateMissingDescriptorIssue(
        ProcessRuntimeStepAssignment assignment)
        => new(
            "dotnet.setup.plan.descriptor_missing",
            $"Step '{assignment.StepKey}' selects the .NET solution setup executor but does not declare a valid script-helper descriptor.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:dotnet.setup.plan.descriptor_missing");

    private static void ValidateRequiredReceipts(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (plan.RequiredReceipts.Count == 0)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.required_receipts_missing",
                $"Step '{assignment.StepKey}' does not declare typed required tool receipts.",
                assignment,
                plan.PlanKey);
            return;
        }

        RequireReceipt(assignment, plan, issues, WorkspacePwshRunScript);
        if (plan.Kind != DotNetSolutionSetupToolPlanKind.CreateProject)
        {
            return;
        }

        RequireReceipt(assignment, plan, issues, "template=sln");
        var appTemplate = ReadLaunchVariable(assignment.LaunchVariables, "DotNetAppTemplate");
        if (string.IsNullOrWhiteSpace(appTemplate))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.app_template_missing",
                $"Step '{assignment.StepKey}' requires a selected DotNetAppTemplate for project creation.",
                assignment,
                "DotNetAppTemplate");
            return;
        }

        RequireReceipt(assignment, plan, issues, $"template={appTemplate.Trim()}");
    }

    private static void RequireReceipt(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues,
        string requiredReceipt)
    {
        if (plan.RequiredReceipts.Any(receipt => ReceiptMatches(receipt, requiredReceipt)))
        {
            return;
        }

        AddIssue(
            issues,
            "dotnet.setup.plan.required_receipt_missing",
            $"Step '{assignment.StepKey}' deterministic tool plan is missing required receipt '{requiredReceipt}'.",
            assignment,
            $"{plan.PlanKey}:{requiredReceipt}");
    }

    private static void ValidateScriptRef(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(plan.ScriptRef))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.script_ref_missing",
                $"Step '{assignment.StepKey}' is missing launch variable '{plan.ScriptRefVariableName}'.",
                assignment,
                plan.ScriptRefVariableName);
            return;
        }

        var normalized = NormalizeRef(plan.ScriptRef);
        if (ContainsUnresolvedTemplateToken(plan.ScriptRef))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.script_ref_unresolved",
                $"Step '{assignment.StepKey}' has unresolved script ref '{plan.ScriptRefVariableName}'.",
                assignment,
                plan.ScriptRef);
        }

        if (!normalized.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
            !normalized.StartsWith("artifacts/process-runs/", StringComparison.OrdinalIgnoreCase) ||
            !normalized.Contains("/scripts/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/steps/", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.script_ref_invalid",
                $"Step '{assignment.StepKey}' script ref must be a current-run managed .ps1 path under artifacts/process-runs/.../scripts, not '{plan.ScriptRef}'.",
                assignment,
                plan.ScriptRef);
        }
    }

    private static void ValidateScript(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(plan.Script))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.script_missing",
                $"Step '{assignment.StepKey}' is missing deterministic helper script launch variable '{plan.ScriptVariableName}'.",
                assignment,
                plan.ScriptVariableName);
            return;
        }

        if (!plan.Script.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
            !plan.Script.Contains("sln", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.script_missing_solution_wiring",
                $"Step '{assignment.StepKey}' helper script does not include solution membership wiring.",
                assignment,
                plan.ScriptVariableName);
        }
    }

    private static void ValidateManifest(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(plan.SideEffectManifest))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.manifest_missing",
                $"Step '{assignment.StepKey}' is missing side-effect manifest launch variable '{plan.SideEffectManifestVariableName}'.",
                assignment,
                plan.SideEffectManifestVariableName);
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(plan.SideEffectManifest);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                AddIssue(
                    issues,
                    "dotnet.setup.plan.manifest_invalid",
                    $"Step '{assignment.StepKey}' side-effect manifest must be a JSON object.",
                    assignment,
                    plan.SideEffectManifestVariableName);
                return;
            }

            var root = document.RootElement;
            if (!root.TryGetProperty("mode", out var mode) ||
                !string.Equals(mode.GetString(), ProductMutationMode, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    "dotnet.setup.plan.manifest_mode_invalid",
                    $"Step '{assignment.StepKey}' side-effect manifest must use mode '{ProductMutationMode}'.",
                    assignment,
                    plan.SideEffectManifestVariableName);
            }

            if (!root.TryGetProperty("allowShellDelegation", out var allowShellDelegation) ||
                allowShellDelegation.ValueKind != JsonValueKind.True)
            {
                AddIssue(
                    issues,
                    "dotnet.setup.plan.manifest_shell_delegation_missing",
                    $"Step '{assignment.StepKey}' side-effect manifest must explicitly allow shell delegation for dotnet commands.",
                    assignment,
                    plan.SideEffectManifestVariableName);
            }

            ValidateManifestPathArray(assignment, plan, root, "declaredReadPaths", physicalPathPolicyFactory, issues);
            ValidateManifestPathArray(assignment, plan, root, "declaredWritePaths", physicalPathPolicyFactory, issues);
        }
        catch (JsonException exception)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.manifest_invalid",
                $"Step '{assignment.StepKey}' side-effect manifest is not valid JSON: {exception.Message}",
                assignment,
                plan.SideEffectManifestVariableName);
        }
    }

    private static void ValidateManifestPathArray(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        JsonElement root,
        string propertyName,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var paths) ||
            paths.ValueKind != JsonValueKind.Array ||
            paths.GetArrayLength() == 0)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.manifest_paths_missing",
                $"Step '{assignment.StepKey}' side-effect manifest must declare non-empty '{propertyName}'.",
                assignment,
                $"{plan.SideEffectManifestVariableName}:{propertyName}");
            return;
        }

        foreach (var path in paths.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String))
        {
            ValidateNativeProductPathScope(
                assignment,
                propertyName,
                path.GetString() ?? string.Empty,
                physicalPathPolicyFactory,
                issues);
        }
    }

    private static void ValidateExecutionPlan(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (!TryParseExecutionPlan(plan.ExecutionPlan, out var executionPlan))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_invalid",
                $"Step '{assignment.StepKey}' must declare a valid structured execution plan in launch variable '{plan.ExecutionPlanVariableName}'.",
                assignment,
                plan.ExecutionPlanVariableName);
            return;
        }

        if (!string.Equals(executionPlan.PlanKey, plan.PlanKey, StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_key_mismatch",
                $"Step '{assignment.StepKey}' structured execution plan does not match deterministic plan key '{plan.PlanKey}'.",
                assignment,
                plan.ExecutionPlanVariableName);
        }

        if (!string.Equals(
                NormalizeRef(executionPlan.ScriptRef),
                NormalizeRef(plan.ScriptRef),
                StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_script_ref_mismatch",
                $"Step '{assignment.StepKey}' structured execution plan does not match the resolved script ref.",
                assignment,
                plan.ExecutionPlanVariableName);
        }

        if (!executionPlan.WorkspaceAlias.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_workspace_alias_invalid",
                $"Step '{assignment.StepKey}' structured execution plan must declare an external-target workspace alias.",
                assignment,
                plan.ExecutionPlanVariableName);
        }

        var requiresScaffold = plan.Kind == DotNetSolutionSetupToolPlanKind.CreateProject;
        if (executionPlan.RequiresScaffold != requiresScaffold)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_scaffold_mismatch",
                $"Step '{assignment.StepKey}' structured execution plan does not match the plan's scaffold requirement.",
                assignment,
                plan.ExecutionPlanVariableName);
        }
    }

    private static bool TryParseExecutionPlan(
        string value,
        out DotNetSolutionSetupExecutionPlan executionPlan)
    {
        executionPlan = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<DotNetSolutionSetupExecutionPlan>(
                value,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            if (parsed is null)
            {
                return false;
            }

            executionPlan = parsed;
            return !string.IsNullOrWhiteSpace(executionPlan.PlanKey) &&
                   !string.IsNullOrWhiteSpace(executionPlan.ScriptRef) &&
                   !string.IsNullOrWhiteSpace(executionPlan.WorkspaceAlias);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateRequiredPaths(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        var minimumPathCount = plan.Kind == DotNetSolutionSetupToolPlanKind.CreateProject ? 1 : 2;
        if (plan.RequiredPaths.Count < minimumPathCount)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.required_paths_missing",
                $"Step '{assignment.StepKey}' must declare every required project path for product completion.",
                assignment,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths);
        }

        if (!plan.RequiredPaths.Any(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.project_path_missing",
                $"Step '{assignment.StepKey}' required paths do not include a project file.",
                assignment,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths);
        }

        foreach (var path in plan.RequiredPaths)
        {
            ValidateNativeProductPathScope(
                assignment,
                "requiredPaths",
                path,
                physicalPathPolicyFactory,
                issues);
        }
    }

    private static void ValidateReadbackChecks(
        ProcessRuntimeStepAssignment assignment,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        var authoritativeSolutionCandidates = new[]
            {
                ReadLaunchVariable(assignment.LaunchVariables, "DotNetSolutionFile")
            }
            .Concat(ReadStringList(
                assignment.LaunchVariables,
                DotNetSolutionSetupTemplatePolicyBindings.SolutionFileCandidatesVariableKey))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .ToArray();
        var checks = ReadCanonicalArrayElement(
            assignment.LaunchVariables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks);
        if (checks is null || authoritativeSolutionCandidates.Length == 0)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.readback_checks_missing",
                $"Step '{assignment.StepKey}' must declare authoritative solution candidates and solution membership readback file-content checks.",
                assignment,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks);
            return;
        }

        var checkElements = checks.Value.ValueKind == JsonValueKind.Array
            ? checks.Value.EnumerateArray().ToArray()
            : [checks.Value];
        var productRoot = ReadLaunchVariable(assignment.LaunchVariables, "ProductRoot");
        IPhysicalFileSystemPathPolicy productRootPolicy;
        try
        {
            productRootPolicy = physicalPathPolicyFactory.Create(productRoot);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException or PhysicalPathValidationException)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.product_root_invalid",
                $"Step '{assignment.StepKey}' declares an invalid ProductRoot for .NET setup readback checks.",
                assignment,
                "ProductRoot");
            return;
        }

        var normalizedAuthoritativeCandidates = authoritativeSolutionCandidates
            .Select(NormalizeNativePath)
            .ToHashSet(productRootPolicy.PathComparer);
        var hasCompleteSolutionCandidateCheck = false;
        foreach (var check in checkElements)
        {
            if (check.ValueKind != JsonValueKind.Object ||
                !check.TryGetProperty("pathCandidates", out var pathCandidates) ||
                pathCandidates.ValueKind != JsonValueKind.Array ||
                pathCandidates.GetArrayLength() == 0 ||
                !check.TryGetProperty("requiredTextAnyGroups", out var textGroups) ||
                textGroups.ValueKind != JsonValueKind.Array ||
                textGroups.GetArrayLength() == 0 ||
                !HasOnlyNonEmptyStrings(pathCandidates) ||
                !HasOnlyNonEmptyStringGroups(textGroups) ||
                check.TryGetProperty("mustExist", out var mustExist) &&
                mustExist.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                AddIssue(
                    issues,
                    "dotnet.setup.plan.readback_check_invalid",
                    $"Step '{assignment.StepKey}' readback check must declare pathCandidates and requiredTextAnyGroups.",
                    assignment,
                    ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks);
                return;
            }

            var normalizedCheckCandidates = pathCandidates
                .EnumerateArray()
                .Select(candidate => candidate.GetString() ?? string.Empty)
                .SelectMany(SplitStringList)
                .Select(NormalizeNativePath)
                .ToHashSet(productRootPolicy.PathComparer);
            hasCompleteSolutionCandidateCheck |=
                normalizedAuthoritativeCandidates.All(normalizedCheckCandidates.Contains);
        }

        if (!hasCompleteSolutionCandidateCheck)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.solution_readback_candidates_missing",
                $"Step '{assignment.StepKey}' must declare one solution membership readback check containing every authoritative solution candidate.",
                assignment,
                DotNetSolutionSetupTemplatePolicyBindings.SolutionFileCandidatesVariableKey);
        }
    }

    private static bool HasOnlyNonEmptyStrings(JsonElement values)
        => values.EnumerateArray().All(value =>
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()));

    private static bool HasOnlyNonEmptyStringGroups(JsonElement groups)
        => groups.EnumerateArray().All(group =>
            group.ValueKind == JsonValueKind.Array &&
            group.GetArrayLength() > 0 &&
            HasOnlyNonEmptyStrings(group));

    private static void ValidateNativeProductPathScope(
        ProcessRuntimeStepAssignment assignment,
        string source,
        string path,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (path.Contains("external-target/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("external-target\\", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.native_path_scope_invalid",
                $"Step '{assignment.StepKey}' {source} must use native ProductRoot/DotNet paths, not external-target aliases: '{path}'.",
                assignment,
                $"{source}:{path}");
            return;
        }

        if (ContainsUnresolvedTemplateToken(path))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.path_unresolved",
                $"Step '{assignment.StepKey}' {source} contains unresolved template placeholder: '{path}'.",
                assignment,
                $"{source}:{path}");
            return;
        }

        var productRoot = ReadLaunchVariable(assignment.LaunchVariables, "ProductRoot");
        if (string.IsNullOrWhiteSpace(productRoot) || !LooksLikeNativeAbsolutePath(path))
        {
            return;
        }

        var isWithinRoot = false;
        try
        {
            isWithinRoot = physicalPathPolicyFactory.Create(productRoot).IsWithinRoot(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException or PhysicalPathValidationException)
        {
        }

        if (!isWithinRoot)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.path_outside_product_root",
                $"Step '{assignment.StepKey}' {source} path is outside ProductRoot: '{path}'.",
                assignment,
                $"{source}:{path}");
        }
    }

    private static IReadOnlyList<string> ReadStringList(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableKey)
    {
        var value = ReadLaunchVariable(launchVariables, variableKey);
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return ReadStringList(document.RootElement);
        }
        catch (JsonException)
        {
            return SplitStringList(value);
        }
    }

    private static JsonElement? ReadCanonicalArrayElement(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableKey)
    {
        var value = ReadLaunchVariable(launchVariables, variableKey);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            var element = document.RootElement;
            if (element.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object) ||
                element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0)
            {
                return null;
            }

            return element.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return SplitStringList(element.GetString());
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .SelectMany(SplitStringList)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> SplitStringList(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string ReadLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
        => !string.IsNullOrWhiteSpace(key) &&
           launchVariables.TryGetValue(key, out var value) &&
           !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : string.Empty;

    private static bool ReceiptMatches(string actual, string expected)
        => string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsUnresolvedTemplateToken(string value)
        => value.Contains("${", StringComparison.Ordinal) ||
           Regex.IsMatch(value, @"\{[A-Za-z0-9_.:-]+\}", RegexOptions.CultureInvariant);

    private static bool LooksLikeNativeAbsolutePath(string path)
        => Regex.IsMatch(path, @"^[A-Za-z]:[\\/]", RegexOptions.CultureInvariant) ||
           path.StartsWith("/", StringComparison.Ordinal);

    private static string NormalizeRef(string value)
        => value.Trim().Replace('\\', '/');

    private static string NormalizeNativePath(string value)
        => value.Trim().TrimEnd('\\', '/').Replace('\\', '/');

    private static void AddIssue(
        List<ProcessRuntimeToolPlanGuardIssue> issues,
        string code,
        string safeSummary,
        ProcessRuntimeStepAssignment assignment,
        string evidence)
    {
        issues.Add(new ProcessRuntimeToolPlanGuardIssue(
            code,
            safeSummary,
            $"{assignment.RunId}:{assignment.StepInstanceId}:{code}:{evidence}"));
    }
}
