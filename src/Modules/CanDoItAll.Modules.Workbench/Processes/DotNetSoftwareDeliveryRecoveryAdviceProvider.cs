using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class DotNetSoftwareDeliveryRecoveryAdviceProvider : IProcessRecoveryAdviceProvider
{
    private const string ProductRequiredToolReceiptMissingCode = "process.adapter.product_required_tool_receipt_missing";
    private const string ProductRequiredToolReceiptBlockedRetryCode = "process.adapter.product_required_tool_receipt_blocked_retry";
    private const string RequiredToolReceiptMissingCode = "process.adapter.required_tool_receipt_missing";
    private const string RequiredToolReceiptBlockedRetryCode = "process.adapter.required_tool_receipt_blocked_retry";
    private const string ProductRequiredFileContentMissingCode = "process.adapter.product_required_file_content_missing";
    private const string WorkspacePwshRunScriptToolName = "workspace_pwsh_run_script";
    private const string WorkspaceDotNetNewToolName = "workspace_dotnet_new";
    private const string WorkspaceAliasVariableName = "WorkspaceAlias";
    private const string ProductRootVariableName = "ProductRoot";
    private const string OutputRootVariableName = "OutputRoot";
    private const string ProductRootAliasVariableName = "ProductRootAlias";
    private const string OutputRootAliasVariableName = "OutputRootAlias";
    private const string ExternalTargetRootVariableName = "ExternalTargetRoot";
    private const string QaValidationStepKey = "qa-validation";
    private const string QaRecheckStepKey = "qa-recheck";
    private const string QualityAcceptedBranchOutcomeKey = "quality-accepted";
    private const string RepairRequiredBranchOutcomeKey = "repair-required";
    private const string RepairEscalationBranchOutcomeKey = "repair-escalation";
    private const string DotNetCreateProjectPrefix = "DotNetCreateProject";
    private static readonly Regex UnresolvedPlaceholderRegex = new(@"\{[A-Za-z][A-Za-z0-9_.:-]*\}", RegexOptions.CultureInvariant);

    public bool CanHandle(ProcessStepRecoveryAdviceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Diagnostics.Any(diagnostic =>
                   IsRequiredToolReceiptDiagnostic(diagnostic.Code) ||
                   string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal) ||
                   string.Equals(
                       diagnostic.Code,
                       ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                       StringComparison.Ordinal)) &&
               (IsQaBranchDecisionStep(context.Request.Assignment.StepKey) ||
                HasDotNetLaunchMetadata(context.Request.Assignment.LaunchVariables) ||
                HasDotNetSetupStepKey(context.Request.Assignment.StepKey));
    }

    public IReadOnlyList<string> BuildAdvice(ProcessStepRecoveryAdviceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lines = new List<string>();
        AddRequiredReceiptGuidance(lines, context.Request.Assignment, context.Diagnostics);
        AddProductReadbackGuidance(lines, context.Request.Assignment, context.Diagnostics);
        AddBrowserSnapshotEvidenceGuidance(lines, context.Request.Assignment, context.Diagnostics);
        var addedDotNetGuidance = AddDotNetCreateProjectGuidance(lines, context.Request.Assignment, context.Diagnostics);
        AddPrimaryArtifactGuidance(lines, context.Request, context.Diagnostics, addedDotNetGuidance);
        return lines;
    }

    private static void AddBrowserSnapshotEvidenceGuidance(
        List<string> lines,
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics)
    {
        if (!diagnostics.Any(diagnostic => string.Equals(
                diagnostic.Code,
                ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                StringComparison.Ordinal)))
        {
            return;
        }

        lines.Add("Current-run browser DOM evidence contains a fatal UI state. A successful browser_console_messages receipt or zero console-error count does not override the visible browser snapshot.");
        if (IsQaBranchDecisionStep(assignment.StepKey))
        {
            lines.Add($"Submit the deterministic defect branch '{ResolveQaDefectBranchOutcomeKey(assignment.StepKey)}'; do not submit '{QualityAcceptedBranchOutcomeKey}' while the fatal browser marker remains visible.");
            return;
        }

        lines.Add("Repair the product runtime failure, launch the repaired app, and capture a new current-run browser snapshot that no longer contains the rejected marker before completing.");
    }

    private static bool HasDotNetLaunchMetadata(IReadOnlyDictionary<string, string> launchVariables)
        => launchVariables.Keys.Any(key =>
               key.StartsWith("DotNet", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Solution", StringComparison.OrdinalIgnoreCase)) ||
           launchVariables.Values.Any(value =>
               value.Contains("workspace_dotnet_", StringComparison.OrdinalIgnoreCase) ||
               value.Contains(WorkspacePwshRunScriptToolName, StringComparison.OrdinalIgnoreCase));

    private static bool HasDotNetSetupStepKey(string stepKey)
        => string.Equals(stepKey, "create-dotnet-project", StringComparison.Ordinal) ||
           string.Equals(stepKey, "repair-solution-setup", StringComparison.Ordinal) ||
           string.Equals(stepKey, "add-test-project", StringComparison.Ordinal);

    private static bool IsRequiredToolReceiptDiagnostic(string code)
        => string.Equals(code, ProductRequiredToolReceiptMissingCode, StringComparison.Ordinal) ||
           string.Equals(code, ProductRequiredToolReceiptBlockedRetryCode, StringComparison.Ordinal) ||
           string.Equals(code, RequiredToolReceiptMissingCode, StringComparison.Ordinal) ||
           string.Equals(code, RequiredToolReceiptBlockedRetryCode, StringComparison.Ordinal);

    private static void AddRequiredReceiptGuidance(
        List<string> lines,
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics)
    {
        if (!diagnostics.Any(diagnostic => IsRequiredToolReceiptDiagnostic(diagnostic.Code)))
        {
            return;
        }

        var requiredReceipts = ResolveStepStringList(
                assignment.LaunchVariables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
                assignment.StepKey)
            .ToArray();
        if (requiredReceipts.Length == 0)
        {
            return;
        }

        AddQaValidationReceiptGuidance(lines, assignment, requiredReceipts);
        AddProjectStructureNodeCreateReceiptGuidance(lines, requiredReceipts);
        AddProjectStructureReadReceiptGuidance(lines, requiredReceipts);

        if (requiredReceipts.Any(receipt => string.Equals(receipt, WorkspacePwshRunScriptToolName, StringComparison.OrdinalIgnoreCase)))
        {
            lines.Add($"Observed scaffold receipts such as {WorkspaceDotNetNewToolName} are not proof of solution membership; the retry must produce the missing {WorkspacePwshRunScriptToolName} receipt in the current run.");
        }
    }

    private static void AddProductReadbackGuidance(
        List<string> lines,
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics)
    {
        var readbackDiagnostics = diagnostics
            .Where(diagnostic => string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal))
            .ToArray();
        if (readbackDiagnostics.Length == 0)
        {
            return;
        }

        var shouldSelectRepairBranch = ShouldSelectQaDefectBranch(assignment, diagnostics);
        if (shouldSelectRepairBranch)
        {
            var productAlias = ResolveProductRootAlias(assignment.LaunchVariables);
            var aliasGuidance = string.IsNullOrWhiteSpace(productAlias)
                ? "grounded external-target aliases or product-root-relative file names"
                : $"grounded product alias {productAlias} or product-root-relative file names";
            lines.Add($"Use {aliasGuidance} in the QA artifact and final outcome. Do not copy native absolute product paths from diagnostics or launch variables.");
            var defectBranchOutcomeKey = ResolveQaDefectBranchOutcomeKey(assignment.StepKey);
            lines.Add($"QA repair branch decision: a product content/readback failure is a concrete implementation defect for this step. Do not return Blocked and do not submit quality-accepted. Submit a completed process-step outcome with branchOutcomeKey '{defectBranchOutcomeKey}'.");
        }
        else
        {
            AddProductMutationReadbackRepairContract(lines);
        }

        var checks = ResolveFileContentChecks(assignment.LaunchVariables, assignment.StepKey);
        foreach (var check in checks)
        {
            if (!string.IsNullOrWhiteSpace(check.Description))
            {
                lines.Add($"Configured content check: {check.Description}.");
            }

            var groundedPathCandidates = ResolveGroundedPathCandidates(check.PathCandidates, assignment.LaunchVariables);
            var pathText = groundedPathCandidates.Count == 0
                ? "configured product file"
                : string.Join(" | ", groundedPathCandidates);
            foreach (var requiredGroup in check.RequiredTextAnyGroups)
            {
                if (requiredGroup.Count == 0)
                {
                    continue;
                }

                lines.Add($"Verify readback for {pathText} contains one of: {string.Join(" | ", requiredGroup)}.");
            }

            if (check.ForbiddenTextAny.Count > 0)
            {
                lines.Add($"Failed content must be treated as repair-required when {pathText} contains any forbidden text: {string.Join(" | ", check.ForbiddenTextAny)}.");
            }
        }
    }

    private static void AddProductMutationReadbackRepairContract(List<string> lines)
    {
        lines.Add("Product readback repair contract: this retry must mutate the product files that fail configured content checks before rewriting the managed artifact; rewriting artifacts or summaries alone is not a repair.");
        lines.Add("For forbidden stock .NET or Blazor scaffold text, remove or replace the shipped scaffold route, navigation, sample-data, starter-copy, and framework-documentation content from the app entrypoint and referenced pages.");
        lines.Add("For Blazor starter pages, deleting Counter.razor or Weather.razor is acceptable when they are default scaffold pages; placeholder pages that keep @page \"/counter\" or @page \"/weather\" still publish forbidden product routes and do not satisfy the repair.");
        lines.Add("After editing, read back the affected files with current-run workspace tool receipts and verify every configured required/forbidden text check passes before submitting Completed.");
        lines.Add("If mutation or readback is blocked by a missing tool, capability, access issue, or product-root problem, return Blocked with the exact failed tool or capability instead of Completed.");
    }

    private static bool AddDotNetCreateProjectGuidance(
        List<string> lines,
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics)
    {
        if (!ShouldAddDotNetCreateProjectGuidance(assignment, diagnostics))
        {
            return false;
        }

        if (!TryResolveScriptVariables(
                assignment,
                out var scriptVariableName,
                out var scriptRefVariableName,
                out var manifestVariableName))
        {
            return false;
        }

        var scriptRef = TryGetResolvedVariable(assignment.LaunchVariables, scriptRefVariableName);
        if (string.IsNullOrWhiteSpace(scriptRef))
        {
            lines.Add($"Resolved {scriptRefVariableName} is unavailable; fix launch variable resolution before retrying this diagnostic.");
            return false;
        }

        lines.Add($"Write {scriptVariableName} verbatim to {scriptRef}.");
        lines.Add("Verify that script ref with workspace_stat_path or workspace_read_file before invoking it.");

        var workspaceAlias = TryGetResolvedVariable(assignment.LaunchVariables, WorkspaceAliasVariableName) ?? WorkspaceAliasVariableName;
        var manifestGuidance = !string.IsNullOrWhiteSpace(manifestVariableName) &&
                               TryGetResolvedVariable(assignment.LaunchVariables, manifestVariableName) is not null
            ? $" and sideEffectManifest from {manifestVariableName}"
            : string.Empty;
        lines.Add($"Invoke {WorkspacePwshRunScriptToolName} with script path {scriptRef}, workingDirectory {workspaceAlias}{manifestGuidance}.");
        lines.Add($"Do not rerun {WorkspaceDotNetNewToolName} with force=true unless contracted files are missing.");
        return true;
    }

    private static void AddProjectStructureReadReceiptGuidance(
        List<string> lines,
        IReadOnlyList<string> requiredReceipts)
    {
        if (!requiredReceipts.Any(receipt => string.Equals(receipt, "project_structure_read", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        lines.Add("Project-structure readback receipt repair: call project_structure_read in this retry after creating or reusing the durable nodes, targeting the relevant process-run node or created node ids.");
        lines.Add("A project_structure_node_create result, a node id copied into text, or a managed artifact that says readback happened is not a project_structure_read receipt.");
    }

    private static void AddProjectStructureNodeCreateReceiptGuidance(
        List<string> lines,
        IReadOnlyList<string> requiredReceipts)
    {
        if (!requiredReceipts.Any(receipt => string.Equals(receipt, "project_structure_node_create", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        lines.Add("Project-structure runtime node creation repair: project_structure_node_create payloads for Run app and Run tests must include schema-valid runtime metadata, not only title, subtype, notes, or command prose.");
        lines.Add("For a .NET Run app Environment node, set metadataJson with metadata.environment.projectPath and metadata.environment.workingDirectory; include launchProfile, protocol, applicationUrl, arguments, or environmentName only when known.");
        lines.Add("For a Run tests Script node, set metadataJson with metadata.script.command, metadata.script.arguments, and metadata.script.workingDirectory. A failed node-create receipt for missing runtime metadata is a concrete repair input; retry with the corrected payload before finalizing.");
    }

    private static bool ShouldAddDotNetCreateProjectGuidance(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics)
        => diagnostics.Any(diagnostic =>
            IsRequiredToolReceiptDiagnostic(diagnostic.Code) ||
            string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal)) &&
           !ShouldSelectQaDefectBranch(assignment, diagnostics);

    private static void AddPrimaryArtifactGuidance(
        List<string> lines,
        ProcessStepRecoveryInstructionBuildRequest request,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics,
        bool addedDotNetGuidance)
    {
        var primaryArtifactRef = BuildPrimaryArtifactRef(request);
        if (ShouldSelectQaDefectBranch(request.Assignment, diagnostics))
        {
            var defectBranchOutcomeKey = ResolveQaDefectBranchOutcomeKey(request.Assignment.StepKey);
            lines.Add($"Rewrite {primaryArtifactRef} with the QA defect disposition and submit a completed process-step outcome with branchOutcomeKey '{defectBranchOutcomeKey}'.");
            return;
        }

        if (ShouldPreserveQaBranchOutcomeAfterReceiptRepair(request.Assignment, diagnostics))
        {
            var defectBranchOutcomeKey = ResolveQaDefectBranchOutcomeKey(request.Assignment.StepKey);
            lines.Add($"Only after the current-run receipt contract is satisfied, rewrite {primaryArtifactRef} and submit a completed process-step outcome with branchOutcomeKey '{QualityAcceptedBranchOutcomeKey}' or '{defectBranchOutcomeKey}' based on the validation evidence.");
            return;
        }

        if (addedDotNetGuidance)
        {
            lines.Add("Read back the solution or product output after the helper runs and verify the required membership/content check passes.");
        }
        else if (diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal)))
        {
            lines.Add("Read back the affected product files after mutation and verify the configured content checks pass.");
        }

        lines.Add($"Only then rewrite {primaryArtifactRef} and submit Completed.");
    }

    private static string BuildPrimaryArtifactRef(ProcessStepRecoveryInstructionBuildRequest request)
        => $"artifacts/process-runs/{request.RunId.Value:D}/steps/{request.StepKey}.md";

    private static bool ShouldSelectQaDefectBranch(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics)
        => IsQaBranchDecisionStep(assignment.StepKey) &&
           diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal));

    private static bool IsQaBranchDecisionStep(string stepKey)
        => string.Equals(stepKey, QaValidationStepKey, StringComparison.Ordinal) ||
           string.Equals(stepKey, QaRecheckStepKey, StringComparison.Ordinal);

    private static bool ShouldPreserveQaBranchOutcomeAfterReceiptRepair(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics)
        => IsQaBranchDecisionStep(assignment.StepKey) &&
           diagnostics.Any(diagnostic => IsRequiredToolReceiptDiagnostic(diagnostic.Code));

    private static string ResolveQaDefectBranchOutcomeKey(string stepKey)
        => string.Equals(stepKey, QaRecheckStepKey, StringComparison.Ordinal)
            ? RepairEscalationBranchOutcomeKey
            : RepairRequiredBranchOutcomeKey;

    private static void AddQaValidationReceiptGuidance(
        List<string> lines,
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> requiredReceipts)
    {
        if (!IsQaBranchDecisionStep(assignment.StepKey))
        {
            return;
        }

        lines.Add("QA current-run validation receipt repair:");
        lines.Add("Invoke the missing validation and browser tools in this retry before finalizing; do not satisfy this diagnostic by rewriting only the managed artifact or summary.");
        lines.Add("Current-run means this exact retry. Prior restore/build/test/runtime/browser/stop receipts, artifacts, screenshots, or summaries do not satisfy this completion gate.");
        lines.Add("Do not write the QA artifact or submit a branch outcome until the listed tool receipts exist in this retry; a quality-accepted branch without them will be rejected again.");
        AddReceiptToolTargetGuidance(
            lines,
            requiredReceipts,
            "workspace_dotnet_restore",
            ResolveFirstLaunchVariable(assignment.LaunchVariables, "DotNetSolutionFileAlias", "DotNetSolutionFile"),
            "restore target");
        AddReceiptToolTargetGuidance(
            lines,
            requiredReceipts,
            "workspace_dotnet_build",
            ResolveFirstLaunchVariable(assignment.LaunchVariables, "DotNetSolutionFileAlias", "DotNetSolutionFile"),
            "build target");
        AddReceiptToolTargetGuidance(
            lines,
            requiredReceipts,
            "workspace_dotnet_test",
            ResolveFirstLaunchVariable(assignment.LaunchVariables, "DotNetTestProjectFileAlias", "DotNetTestProjectFile"),
            "test target");
        AddReceiptToolTargetGuidance(
            lines,
            requiredReceipts,
            "workspace_dotnet_run",
            ResolveFirstLaunchVariable(assignment.LaunchVariables, "DotNetAppProjectFileAlias", "DotNetAppProjectFile"),
            "run target");

        if (requiredReceipts.Any(IsBrowserValidationReceipt))
        {
            lines.Add("After workspace_dotnet_run starts the repaired app, navigate the browser to the concrete product route, then call browser_snapshot, browser_take_screenshot, and browser_console_messages in the same retry.");
        }

        if (requiredReceipts.Any(receipt => string.Equals(receipt, "workspace_dotnet_stop", StringComparison.OrdinalIgnoreCase)))
        {
            lines.Add("Call workspace_dotnet_stop for the app process started in this retry before finalizing, even when validation finds a repair/escalation defect.");
            lines.Add("Use the startup receipt returned by this retry's workspace_dotnet_run when stopping the app; runtime start, browser proof, and stop cleanup must all describe the same host lifecycle.");
        }

        var defectBranchOutcomeKey = ResolveQaDefectBranchOutcomeKey(assignment.StepKey);
        lines.Add($"If a required tool is unavailable or denied, return Blocked with that exact tool/capability diagnostic. Otherwise select branchOutcomeKey '{QualityAcceptedBranchOutcomeKey}' or '{defectBranchOutcomeKey}' from the evidence; missing receipts alone are not a branch disposition.");
    }

    private static bool IsBrowserValidationReceipt(string receipt)
        => string.Equals(receipt, "browser_navigate", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(receipt, "browser_snapshot", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(receipt, "browser_take_screenshot", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(receipt, "browser_console_messages", StringComparison.OrdinalIgnoreCase);

    private static void AddReceiptToolTargetGuidance(
        List<string> lines,
        IReadOnlyList<string> requiredReceipts,
        string toolName,
        string? target,
        string targetLabel)
    {
        if (!requiredReceipts.Any(receipt => string.Equals(receipt, toolName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var targetText = string.IsNullOrWhiteSpace(target)
            ? $"the launch-variable {targetLabel}"
            : target;
        lines.Add($"- Invoke {toolName} with {targetLabel} {targetText}.");
    }

    private static string? ResolveFirstLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryGetResolvedVariable(launchVariables, key) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryResolveScriptVariables(
        ProcessRuntimeStepAssignment assignment,
        out string scriptVariableName,
        out string scriptRefVariableName,
        out string manifestVariableName)
    {
        var prefix = assignment.StepKey switch
        {
            "create-dotnet-project" => DotNetCreateProjectPrefix,
            "repair-solution-setup" when assignment.LaunchVariables.ContainsKey("DotNetAddTestProjectScriptRef") => "DotNetAddTestProject",
            "repair-solution-setup" => DotNetCreateProjectPrefix,
            "add-test-project" => "DotNetAddTestProject",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(prefix))
        {
            scriptVariableName = string.Empty;
            scriptRefVariableName = string.Empty;
            manifestVariableName = string.Empty;
            return false;
        }

        scriptVariableName = $"{prefix}Script";
        scriptRefVariableName = $"{prefix}ScriptRef";
        manifestVariableName = $"{prefix}SideEffectManifest";
        return assignment.LaunchVariables.ContainsKey(scriptVariableName) ||
               assignment.LaunchVariables.ContainsKey(scriptRefVariableName);
    }

    private static IReadOnlyList<string> ResolveStepStringList(
        IReadOnlyDictionary<string, string> launchVariables,
        string directKey,
        string byStepKey,
        string stepKey)
    {
        if (TryGetResolvedVariable(launchVariables, directKey) is { } direct)
        {
            return ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(direct);
        }

        if (TryGetResolvedVariable(launchVariables, byStepKey) is not { } byStep)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(byStep);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(property.Value);
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static IReadOnlyList<string> ParseStringList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return ParseStringList(document.RootElement);
        }
        catch (JsonException)
        {
            return SplitStringList(value);
        }
    }

    private static IReadOnlyList<string> ParseStringList(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => SplitStringList(element.GetString() ?? string.Empty),
            JsonValueKind.Array => element
                .EnumerateArray()
                .SelectMany(ParseStringList)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => []
        };
    }

    private static IReadOnlyList<string> SplitStringList(string value)
        => value
            .Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !ContainsUnresolvedPlaceholder(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<ProductReadbackCheck> ResolveFileContentChecks(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        var raw = TryGetResolvedVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks);
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = TryGetStepScopedJson(
                launchVariables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep,
                stepKey);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return ParseProductReadbackChecks(document.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? TryGetStepScopedJson(
        IReadOnlyDictionary<string, string> launchVariables,
        string byStepKey,
        string stepKey)
    {
        var byStep = TryGetResolvedVariable(launchVariables, byStepKey);
        if (string.IsNullOrWhiteSpace(byStep))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(byStep);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.GetRawText();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static IReadOnlyList<ProductReadbackCheck> ParseProductReadbackChecks(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => element
                .EnumerateArray()
                .Select(ParseProductReadbackCheck)
                .Where(check => check is not null)
                .Cast<ProductReadbackCheck>()
                .ToArray(),
            JsonValueKind.Object => ParseProductReadbackCheck(element) is { } check ? [check] : [],
            _ => []
        };
    }

    private static ProductReadbackCheck? ParseProductReadbackCheck(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var pathCandidates = element.TryGetProperty("pathCandidates", out var paths)
            ? ParseStringList(paths)
            : [];
        var description = element.TryGetProperty("description", out var descriptionElement) &&
                          descriptionElement.ValueKind == JsonValueKind.String
            ? descriptionElement.GetString() ?? string.Empty
            : string.Empty;
        var requiredTextAnyGroups = new List<IReadOnlyList<string>>();
        if (element.TryGetProperty("requiredTextAnyGroups", out var groups))
        {
            if (groups.ValueKind == JsonValueKind.Array)
            {
                foreach (var group in groups.EnumerateArray())
                {
                    requiredTextAnyGroups.Add(ParseStringList(group));
                }
            }
            else
            {
                requiredTextAnyGroups.Add(ParseStringList(groups));
            }
        }

        var forbiddenTextAny = element.TryGetProperty("forbiddenTextAny", out var forbiddenText)
            ? ParseStringList(forbiddenText)
            : [];

        return new ProductReadbackCheck(pathCandidates, description, requiredTextAnyGroups, forbiddenTextAny);
    }

    private static IReadOnlyList<string> ResolveGroundedPathCandidates(
        IReadOnlyList<string> pathCandidates,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        if (pathCandidates.Count == 0)
        {
            return [];
        }

        var nativeRoot = ResolveProductRoot(launchVariables);
        var productAlias = ResolveProductRootAlias(launchVariables);
        return pathCandidates
            .Select(candidate => ToGroundedPathCandidate(candidate, nativeRoot, productAlias))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ResolveProductRoot(IReadOnlyDictionary<string, string> launchVariables)
        => TryGetResolvedVariable(launchVariables, ProductRootVariableName) ??
           TryGetResolvedVariable(launchVariables, OutputRootVariableName);

    private static string? ResolveProductRootAlias(IReadOnlyDictionary<string, string> launchVariables)
        => TryGetResolvedVariable(launchVariables, ProductRootAliasVariableName) ??
           TryGetResolvedVariable(launchVariables, OutputRootAliasVariableName) ??
           TryGetResolvedVariable(launchVariables, WorkspaceAliasVariableName) ??
           TryGetResolvedVariable(launchVariables, ExternalTargetRootVariableName);

    private static string ToGroundedPathCandidate(
        string candidate,
        string? nativeRoot,
        string? productAlias)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        var normalizedCandidate = NormalizePathForInstruction(candidate);
        if (!string.IsNullOrWhiteSpace(productAlias))
        {
            var normalizedAlias = NormalizePathForInstruction(productAlias).TrimEnd('/');
            if (normalizedCandidate.StartsWith(normalizedAlias, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedCandidate;
            }

            if (!string.IsNullOrWhiteSpace(nativeRoot))
            {
                var normalizedRoot = NormalizePathForInstruction(nativeRoot).TrimEnd('/');
                if (normalizedCandidate.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{normalizedAlias}/{normalizedCandidate[(normalizedRoot.Length + 1)..]}";
                }
            }
        }

        return IsNativeAbsolutePath(normalizedCandidate)
            ? LastPathSegments(normalizedCandidate, 3)
            : normalizedCandidate;
    }

    private static string NormalizePathForInstruction(string value)
        => value.Trim().Replace('\\', '/');

    private static bool IsNativeAbsolutePath(string normalizedPath)
        => normalizedPath.StartsWith("/", StringComparison.Ordinal) ||
           normalizedPath.Length >= 3 &&
           char.IsLetter(normalizedPath[0]) &&
           normalizedPath[1] == ':' &&
           normalizedPath[2] == '/';

    private static string LastPathSegments(string normalizedPath, int segmentCount)
    {
        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(segmentCount)
            .ToArray();
        return segments.Length == 0
            ? normalizedPath
            : string.Join("/", segments);
    }

    private static string? TryGetResolvedVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
    {
        if (!launchVariables.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value) ||
            ContainsUnresolvedPlaceholder(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool ContainsUnresolvedPlaceholder(string value)
        => UnresolvedPlaceholderRegex.IsMatch(value);

    private sealed record ProductReadbackCheck(
        IReadOnlyList<string> PathCandidates,
        string Description,
        IReadOnlyList<IReadOnlyList<string>> RequiredTextAnyGroups,
        IReadOnlyList<string> ForbiddenTextAny);
}
