using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal enum DotNetSolutionSetupToolPlanKind
{
    CreateProject,
    AddTestProject,
    RepairSolutionSetup
}

internal sealed record DotNetSolutionSetupToolPlan(
    DotNetSolutionSetupToolPlanKind Kind,
    string StepKey,
    string ScriptRefKey,
    string ScriptKey,
    string SideEffectManifestKey,
    string ExecutionPlanKey,
    IReadOnlyList<string> RequiredReceipts,
    IReadOnlyList<string> RequiredPaths,
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
    private const string ProductMutationMode = "ProductMutation";

    public static DotNetSolutionSetupToolPlanGuardResult Evaluate(ProcessRuntimeStepAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (!TryResolvePlan(assignment, out var plan))
        {
            return DotNetSolutionSetupToolPlanGuardResult.Satisfied;
        }

        var issues = new List<ProcessRuntimeToolPlanGuardIssue>();
        ValidateRequiredReceipts(assignment, plan, issues);
        ValidateScriptRef(assignment, plan, issues);
        ValidateScript(assignment, plan, issues);
        ValidateManifest(assignment, plan, issues);
        ValidateExecutionPlan(assignment, plan, issues);
        ValidateRequiredPaths(assignment, plan, issues);
        ValidateReadbackChecks(assignment, plan, issues);

        return issues.Count == 0
            ? new DotNetSolutionSetupToolPlanGuardResult(plan, [])
            : new DotNetSolutionSetupToolPlanGuardResult(plan, issues);
    }

    private static bool TryResolvePlan(
        ProcessRuntimeStepAssignment assignment,
        out DotNetSolutionSetupToolPlan plan)
    {
        plan = null!;
        var stepKey = assignment.StepKey.Trim();
        if (!IsDotNetSetupStep(stepKey) || !HasDotNetSetupPlanVariables(assignment.LaunchVariables))
        {
            return false;
        }

        var kind = stepKey switch
        {
            "create-dotnet-project" => DotNetSolutionSetupToolPlanKind.CreateProject,
            "add-test-project" => DotNetSolutionSetupToolPlanKind.AddTestProject,
            "repair-solution-setup" => DotNetSolutionSetupToolPlanKind.RepairSolutionSetup,
            _ => throw new InvalidOperationException($"Unsupported .NET setup step key '{stepKey}'.")
        };
        var useCreatePlan = kind == DotNetSolutionSetupToolPlanKind.CreateProject;
        var scriptRefKey = useCreatePlan ? "DotNetCreateProjectScriptRef" : "DotNetAddTestProjectScriptRef";
        var scriptKey = useCreatePlan ? "DotNetCreateProjectScript" : "DotNetAddTestProjectScript";
        var manifestKey = useCreatePlan ? "DotNetCreateProjectSideEffectManifest" : "DotNetAddTestProjectSideEffectManifest";
        var executionPlanKey = useCreatePlan ? "DotNetCreateProjectExecutionPlan" : "DotNetAddTestProjectExecutionPlan";

        plan = new DotNetSolutionSetupToolPlan(
            kind,
            stepKey,
            scriptRefKey,
            scriptKey,
            manifestKey,
            executionPlanKey,
            ReadStepStringList(
                assignment.LaunchVariables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
                stepKey),
            ReadStepStringList(
                assignment.LaunchVariables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep,
                stepKey),
            ReadLaunchVariable(assignment.LaunchVariables, scriptRefKey),
            ReadLaunchVariable(assignment.LaunchVariables, scriptKey),
            ReadLaunchVariable(assignment.LaunchVariables, manifestKey),
            ReadLaunchVariable(assignment.LaunchVariables, executionPlanKey));
        return true;
    }

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
                $"Step '{plan.StepKey}' does not declare typed required tool receipts.",
                assignment,
                plan.StepKey);
            return;
        }

        RequireReceipt(assignment, plan, issues, WorkspacePwshRunScript);
        if (plan.Kind != DotNetSolutionSetupToolPlanKind.CreateProject)
        {
            return;
        }

        RequireReceipt(assignment, plan, issues, "template=sln");
        var appTemplate = ReadLaunchVariable(assignment.LaunchVariables, "DotNetAppTemplate");
        var appTemplateReceipt = string.IsNullOrWhiteSpace(appTemplate)
            ? WorkspaceDotnetNew
            : $"template={appTemplate.Trim()}";
        RequireReceipt(assignment, plan, issues, appTemplateReceipt);
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
            $"Step '{plan.StepKey}' deterministic tool plan is missing required receipt '{requiredReceipt}'.",
            assignment,
            $"{plan.StepKey}:{requiredReceipt}");
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
                $"Step '{plan.StepKey}' is missing launch variable '{plan.ScriptRefKey}'.",
                assignment,
                plan.ScriptRefKey);
            return;
        }

        var normalized = NormalizeRef(plan.ScriptRef);
        if (ContainsUnresolvedTemplateToken(plan.ScriptRef))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.script_ref_unresolved",
                $"Step '{plan.StepKey}' has unresolved script ref '{plan.ScriptRefKey}'.",
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
                $"Step '{plan.StepKey}' script ref must be a current-run managed .ps1 path under artifacts/process-runs/.../scripts, not '{plan.ScriptRef}'.",
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
                $"Step '{plan.StepKey}' is missing deterministic helper script launch variable '{plan.ScriptKey}'.",
                assignment,
                plan.ScriptKey);
            return;
        }

        if (!plan.Script.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
            !plan.Script.Contains("sln", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.script_missing_solution_wiring",
                $"Step '{plan.StepKey}' helper script does not include solution membership wiring.",
                assignment,
                plan.ScriptKey);
        }
    }

    private static void ValidateManifest(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(plan.SideEffectManifest))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.manifest_missing",
                $"Step '{plan.StepKey}' is missing side-effect manifest launch variable '{plan.SideEffectManifestKey}'.",
                assignment,
                plan.SideEffectManifestKey);
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
                    $"Step '{plan.StepKey}' side-effect manifest must be a JSON object.",
                    assignment,
                    plan.SideEffectManifestKey);
                return;
            }

            var root = document.RootElement;
            if (!root.TryGetProperty("mode", out var mode) ||
                !string.Equals(mode.GetString(), ProductMutationMode, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    "dotnet.setup.plan.manifest_mode_invalid",
                    $"Step '{plan.StepKey}' side-effect manifest must use mode '{ProductMutationMode}'.",
                    assignment,
                    plan.SideEffectManifestKey);
            }

            if (!root.TryGetProperty("allowShellDelegation", out var allowShellDelegation) ||
                allowShellDelegation.ValueKind != JsonValueKind.True)
            {
                AddIssue(
                    issues,
                    "dotnet.setup.plan.manifest_shell_delegation_missing",
                    $"Step '{plan.StepKey}' side-effect manifest must explicitly allow shell delegation for dotnet commands.",
                    assignment,
                    plan.SideEffectManifestKey);
            }

            ValidateManifestPathArray(assignment, plan, root, "declaredReadPaths", issues);
            ValidateManifestPathArray(assignment, plan, root, "declaredWritePaths", issues);
        }
        catch (JsonException exception)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.manifest_invalid",
                $"Step '{plan.StepKey}' side-effect manifest is not valid JSON: {exception.Message}",
                assignment,
                plan.SideEffectManifestKey);
        }
    }

    private static void ValidateManifestPathArray(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        JsonElement root,
        string propertyName,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var paths) ||
            paths.ValueKind != JsonValueKind.Array ||
            paths.GetArrayLength() == 0)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.manifest_paths_missing",
                $"Step '{plan.StepKey}' side-effect manifest must declare non-empty '{propertyName}'.",
                assignment,
                $"{plan.SideEffectManifestKey}:{propertyName}");
            return;
        }

        foreach (var path in paths.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String))
        {
            ValidateNativeProductPathScope(assignment, plan, propertyName, path.GetString() ?? string.Empty, issues);
        }
    }

    private static void ValidateExecutionPlan(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(plan.ExecutionPlan))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_missing",
                $"Step '{plan.StepKey}' is missing deterministic execution plan launch variable '{plan.ExecutionPlanKey}'.",
                assignment,
                plan.ExecutionPlanKey);
            return;
        }

        if (ContainsUnresolvedTemplateToken(plan.ExecutionPlan))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_unresolved",
                $"Step '{plan.StepKey}' execution plan still contains unresolved template placeholders.",
                assignment,
                plan.ExecutionPlanKey);
        }

        if (!string.IsNullOrWhiteSpace(plan.ScriptRef) &&
            !plan.ExecutionPlan.Contains(plan.ScriptRef, StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_script_ref_mismatch",
                $"Step '{plan.StepKey}' execution plan does not invoke the resolved script ref '{plan.ScriptRef}'.",
                assignment,
                plan.ExecutionPlanKey);
        }

        if (!plan.ExecutionPlan.Contains(WorkspacePwshRunScript, StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_missing_script_tool",
                $"Step '{plan.StepKey}' execution plan does not require '{WorkspacePwshRunScript}'.",
                assignment,
                plan.ExecutionPlanKey);
        }

        if (plan.Kind == DotNetSolutionSetupToolPlanKind.CreateProject &&
            !plan.ExecutionPlan.Contains(WorkspaceDotnetNew, StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_missing_scaffold_tool",
                $"Step '{plan.StepKey}' execution plan does not require '{WorkspaceDotnetNew}'.",
                assignment,
                plan.ExecutionPlanKey);
        }

        var productRoot = ReadLaunchVariable(assignment.LaunchVariables, "ProductRoot");
        if (!string.IsNullOrWhiteSpace(productRoot) &&
            plan.ExecutionPlan.Contains($"workingDirectory '{productRoot}'", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.execution_plan_wrong_scope",
                $"Step '{plan.StepKey}' execution plan must use the external-target workspace alias for workspace tool workingDirectory, not ProductRoot.",
                assignment,
                plan.ExecutionPlanKey);
        }
    }

    private static void ValidateRequiredPaths(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        var minimumPathCount = plan.Kind == DotNetSolutionSetupToolPlanKind.CreateProject ? 2 : 3;
        if (plan.RequiredPaths.Count < minimumPathCount)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.required_paths_missing",
                $"Step '{plan.StepKey}' must declare solution/project required paths for product completion.",
                assignment,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep);
        }

        if (!plan.RequiredPaths.Any(path => path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                                            path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.solution_path_missing",
                $"Step '{plan.StepKey}' required paths do not include a solution file.",
                assignment,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep);
        }

        if (!plan.RequiredPaths.Any(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.project_path_missing",
                $"Step '{plan.StepKey}' required paths do not include a project file.",
                assignment,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep);
        }

        foreach (var path in plan.RequiredPaths)
        {
            ValidateNativeProductPathScope(assignment, plan, "requiredPaths", path, issues);
        }
    }

    private static void ValidateReadbackChecks(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        List<ProcessRuntimeToolPlanGuardIssue> issues)
    {
        var checks = ReadStepArrayElement(
            assignment.LaunchVariables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep,
            plan.StepKey);
        if (checks is null)
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.readback_checks_missing",
                $"Step '{plan.StepKey}' must declare solution membership readback file-content checks.",
                assignment,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep);
            return;
        }

        foreach (var check in checks.Value.EnumerateArray())
        {
            if (check.ValueKind != JsonValueKind.Object ||
                !check.TryGetProperty("pathCandidates", out var pathCandidates) ||
                pathCandidates.ValueKind != JsonValueKind.Array ||
                pathCandidates.GetArrayLength() == 0 ||
                !check.TryGetProperty("requiredTextAnyGroups", out var textGroups) ||
                textGroups.ValueKind != JsonValueKind.Array ||
                textGroups.GetArrayLength() == 0)
            {
                AddIssue(
                    issues,
                    "dotnet.setup.plan.readback_check_invalid",
                    $"Step '{plan.StepKey}' readback check must declare pathCandidates and requiredTextAnyGroups.",
                    assignment,
                    ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep);
                return;
            }
        }
    }

    private static void ValidateNativeProductPathScope(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        string source,
        string path,
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
                $"Step '{plan.StepKey}' {source} must use native ProductRoot/DotNet paths, not external-target aliases: '{path}'.",
                assignment,
                $"{source}:{path}");
            return;
        }

        if (ContainsUnresolvedTemplateToken(path))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.path_unresolved",
                $"Step '{plan.StepKey}' {source} contains unresolved template placeholder: '{path}'.",
                assignment,
                $"{source}:{path}");
            return;
        }

        var productRoot = ReadLaunchVariable(assignment.LaunchVariables, "ProductRoot");
        if (string.IsNullOrWhiteSpace(productRoot) || !LooksLikeNativeAbsolutePath(path))
        {
            return;
        }

        var normalizedProductRoot = NormalizeNativePath(productRoot);
        var normalizedPath = NormalizeNativePath(path);
        if (!normalizedPath.Equals(normalizedProductRoot, StringComparison.OrdinalIgnoreCase) &&
            !normalizedPath.StartsWith($"{normalizedProductRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                issues,
                "dotnet.setup.plan.path_outside_product_root",
                $"Step '{plan.StepKey}' {source} path is outside ProductRoot: '{path}'.",
                assignment,
                $"{source}:{path}");
        }
    }

    private static IReadOnlyList<string> ReadStepStringList(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableKey,
        string stepKey)
    {
        if (!launchVariables.TryGetValue(variableKey, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (TryResolveStepElement(document.RootElement, stepKey, out var stepElement))
            {
                return ReadStringList(stepElement);
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static JsonElement? ReadStepArrayElement(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableKey,
        string stepKey)
    {
        if (!launchVariables.TryGetValue(variableKey, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (!TryResolveStepElement(document.RootElement, stepKey, out var stepElement) ||
                stepElement.ValueKind != JsonValueKind.Array ||
                stepElement.GetArrayLength() == 0)
            {
                return null;
            }

            return stepElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryResolveStepElement(
        JsonElement root,
        string stepKey,
        out JsonElement stepElement)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    stepElement = property.Value;
                    return true;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            stepElement = root;
            return true;
        }

        stepElement = default;
        return false;
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
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> SplitStringList(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string ReadLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
        => launchVariables.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static bool HasDotNetSetupPlanVariables(IReadOnlyDictionary<string, string> launchVariables)
        => launchVariables.Keys.Any(key =>
            key.StartsWith("DotNetCreateProject", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("DotNetAddTestProject", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep, StringComparison.OrdinalIgnoreCase));

    private static bool IsDotNetSetupStep(string stepKey)
        => stepKey is "create-dotnet-project" or "add-test-project" or "repair-solution-setup";

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

