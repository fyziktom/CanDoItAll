using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal static class ProcessRuntimeRecoveryInstructionHeadings
{
    public const string OperatorRework = "Operator rework instruction";
    public const string ManagerRecovery = "Runtime manager recovery instruction";
    public const string RuntimeDiagnosticRecovery = "Runtime diagnostic rework instruction";
}

public sealed record ProcessStepRecoveryInstructionBuildRequest(
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    string StepKey,
    ProcessRuntimeStepAssignment Assignment,
    StrategyResultEnvelope? StrategyResult,
    StrategyResultReceipt? Receipt,
    string OperatorReason);

public sealed record ProcessStepRecoveryInstruction(string Text)
{
    public static ProcessStepRecoveryInstruction Empty { get; } = new(string.Empty);

    public bool HasInstruction => !string.IsNullOrWhiteSpace(Text);
}

public interface IProcessStepRecoveryInstructionBuilder
{
    ProcessStepRecoveryInstruction Build(ProcessStepRecoveryInstructionBuildRequest request);
}

public sealed class ProcessStepRecoveryInstructionBuilder : IProcessStepRecoveryInstructionBuilder
{
    private const string ProductRequiredToolReceiptMissingCode = "process.adapter.product_required_tool_receipt_missing";
    private const string ProductRequiredToolReceiptBlockedRetryCode = "process.adapter.product_required_tool_receipt_blocked_retry";
    private const string RequiredToolReceiptMissingCode = "process.adapter.required_tool_receipt_missing";
    private const string RequiredToolReceiptBlockedRetryCode = "process.adapter.required_tool_receipt_blocked_retry";
    private const string ProductRequiredFileContentMissingCode = "process.adapter.product_required_file_content_missing";
    private const string UngroundedOutcomeReferenceCode = "process.adapter.ungrounded_outcome_reference";
    private const string UngroundedManagedArtifactReferenceCode = "process.adapter.ungrounded_managed_artifact_reference";
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

    public static ProcessStepRecoveryInstructionBuilder Instance { get; } = new();

    public ProcessStepRecoveryInstruction Build(ProcessStepRecoveryInstructionBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Assignment);

        var diagnostics = CollectDiagnostics(request).ToArray();
        if (diagnostics.Length == 0 ||
            !diagnostics.Any(IsDiagnosticRecoveryCandidate))
        {
            return ProcessStepRecoveryInstruction.Empty;
        }

        var lines = new List<string>
        {
            $"Previous attempt was rejected by runtime completion gates for step '{request.StepKey}'."
        };
        AddRecoveryDecision(lines, request.Receipt?.RecoveryDecision);
        AddDiagnosticCodes(lines, diagnostics);
        AddRequiredReceiptGuidance(lines, request.Assignment, diagnostics);
        AddProductReadbackGuidance(lines, request.Assignment, diagnostics);
        AddUngroundedReferenceGuidance(lines, request, diagnostics);
        var addedDotNetGuidance = AddDotNetCreateProjectGuidance(lines, request.Assignment, diagnostics);
        AddPrimaryArtifactGuidance(lines, request, diagnostics, addedDotNetGuidance);

        var text = string.Join(
            Environment.NewLine,
            lines
                .Select(SanitizeInstructionLine)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        return string.IsNullOrWhiteSpace(text)
            ? ProcessStepRecoveryInstruction.Empty
            : new ProcessStepRecoveryInstruction(text);
    }

    private static IEnumerable<RecoveryDiagnosticFact> CollectDiagnostics(ProcessStepRecoveryInstructionBuildRequest request)
    {
        if (request.StrategyResult is not null)
        {
            foreach (var diagnostic in request.StrategyResult.Diagnostics)
            {
                yield return new RecoveryDiagnosticFact(
                    diagnostic.Code.Value,
                    diagnostic.EvidenceHash,
                    diagnostic.SafeSummary,
                    diagnostic.RetrySafety,
                    diagnostic.Idempotency);
            }
        }

        if (request.Receipt is null)
        {
            yield break;
        }

        foreach (var diagnostic in request.Receipt.Diagnostics)
        {
            yield return new RecoveryDiagnosticFact(
                diagnostic.Code,
                diagnostic.EvidenceHash,
                diagnostic.SafeSummary,
                diagnostic.RetrySafety,
                diagnostic.Idempotency);
        }
    }

    private static bool IsDiagnosticRecoveryCandidate(RecoveryDiagnosticFact diagnostic)
        => IsRequiredToolReceiptDiagnostic(diagnostic.Code) ||
           string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal) ||
           diagnostic.Code.StartsWith("process.adapter.product_", StringComparison.Ordinal) ||
           diagnostic.Code.StartsWith("process.adapter.produced_artifact_", StringComparison.Ordinal) ||
           diagnostic.Code.StartsWith("process.adapter.ungrounded_", StringComparison.Ordinal);

    private static bool IsRequiredToolReceiptDiagnostic(string code)
        => string.Equals(code, ProductRequiredToolReceiptMissingCode, StringComparison.Ordinal) ||
           string.Equals(code, ProductRequiredToolReceiptBlockedRetryCode, StringComparison.Ordinal) ||
           string.Equals(code, RequiredToolReceiptMissingCode, StringComparison.Ordinal) ||
           string.Equals(code, RequiredToolReceiptBlockedRetryCode, StringComparison.Ordinal);

    private static bool IsUngroundedReferenceDiagnostic(string code)
        => string.Equals(code, UngroundedOutcomeReferenceCode, StringComparison.Ordinal) ||
           string.Equals(code, UngroundedManagedArtifactReferenceCode, StringComparison.Ordinal);

    private static void AddRecoveryDecision(List<string> lines, ProcessRecoveryDecisionReceipt? decision)
    {
        if (decision is null)
        {
            return;
        }

        lines.Add($"Recovery route: {decision.DecisionKind}/{decision.RouteKind}; policy: {decision.Policy}; source diagnostic: {decision.SourceDiagnosticCode}.");
        if (!string.IsNullOrWhiteSpace(decision.DiagnosticFingerprint))
        {
            lines.Add($"Retry budget: automatic {decision.AutomaticRetryAttempt}/{decision.MaximumAutomaticRetryAttempts}; same diagnostic fingerprint {decision.SameDiagnosticFingerprintAttempt}/{decision.MaximumSameDiagnosticFingerprintAttempts}; fingerprint {decision.DiagnosticFingerprint}.");
        }

        if (decision.DecisionKind == ProcessRecoveryDecisionKind.ManagerRequired &&
            string.Equals(decision.Policy, "process.current-step-safe-retry-budget-exhausted", StringComparison.Ordinal))
        {
            lines.Add("Safe retry budget is exhausted; keep this attempted repair plan attached for manager review instead of dispatching a blind retry.");
        }
    }

    private static void AddDiagnosticCodes(List<string> lines, IReadOnlyList<RecoveryDiagnosticFact> diagnostics)
    {
        lines.Add("Diagnostic codes:");
        foreach (var diagnostic in diagnostics.DistinctBy(diagnostic => diagnostic.Code))
        {
            var summary = string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal)
                ? "product file content/readback check failed; see the product readback failure section for grounded retry instructions."
                : diagnostic.Summary;
            lines.Add($"- {diagnostic.Code}: {summary}");
        }
    }

    private static void AddRequiredReceiptGuidance(
        List<string> lines,
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<RecoveryDiagnosticFact> diagnostics)
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
            lines.Add("Missing current-run receipt(s): the completion gate reported required receipt evidence, but the assignment did not expose exact receipt names.");
            return;
        }

        lines.Add("Missing current-run receipt(s):");
        foreach (var receipt in requiredReceipts)
        {
            lines.Add($"- {receipt}");
        }

        AddQaValidationReceiptGuidance(lines, assignment, requiredReceipts);

        if (requiredReceipts.Any(receipt => string.Equals(receipt, WorkspacePwshRunScriptToolName, StringComparison.OrdinalIgnoreCase)))
        {
            lines.Add($"Observed scaffold receipts such as {WorkspaceDotNetNewToolName} are not proof of solution membership; the retry must produce the missing {WorkspacePwshRunScriptToolName} receipt in the current run.");
        }
    }

    private static void AddProductReadbackGuidance(
        List<string> lines,
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<RecoveryDiagnosticFact> diagnostics)
    {
        var readbackDiagnostics = diagnostics
            .Where(diagnostic => string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal))
            .ToArray();
        if (readbackDiagnostics.Length == 0)
        {
            return;
        }

        lines.Add("Product readback failure(s):");
        var shouldSelectRepairBranch = ShouldSelectQaDefectBranch(assignment, diagnostics);
        foreach (var diagnostic in readbackDiagnostics)
        {
            lines.Add(shouldSelectRepairBranch
                ? $"- {diagnostic.Code}: product content/readback check failed; use the configured checks below and do not copy native diagnostic paths into evidence."
                : $"- {diagnostic.Summary}");
        }

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

    private static void AddUngroundedReferenceGuidance(
        List<string> lines,
        ProcessStepRecoveryInstructionBuildRequest request,
        IReadOnlyList<RecoveryDiagnosticFact> diagnostics)
    {
        if (!diagnostics.Any(diagnostic => IsUngroundedReferenceDiagnostic(diagnostic.Code)))
        {
            return;
        }

        var primaryArtifactRef = BuildPrimaryArtifactRef(request);
        lines.Add("Ungrounded path-like reference repair:");
        lines.Add("The rejected literal ref is intentionally withheld. Do not copy path-like refs from earlier attempts, diagnostics, source metadata, project-structure summaries, or product file names into reason, summary, next actions, or evidenceRefs.");
        lines.Add($"Use {primaryArtifactRef} as the managed evidence ref after rewriting it. Add exact current-run workspace tool receipt refs only for tools actually read, validated, or wrote evidence during this retry.");
        lines.Add("If the review needs to discuss a product file, describe the component or behavior without a path-like string, or first create a current-run tool receipt that grounds the exact ref and cite that receipt.");
        lines.Add("Do not include native absolute paths, scoped storage paths, managed-files paths, project-media paths, tool-runs paths, SourceDocLink values, or unverified external-target child paths in final outcome fields.");
        lines.Add("Overwrite the managed artifact too if it repeats the rejected path-like strings.");
    }

    private static bool AddDotNetCreateProjectGuidance(
        List<string> lines,
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<RecoveryDiagnosticFact> diagnostics)
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

    private static bool ShouldAddDotNetCreateProjectGuidance(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<RecoveryDiagnosticFact> diagnostics)
        => diagnostics.Any(diagnostic =>
            IsRequiredToolReceiptDiagnostic(diagnostic.Code) ||
            string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal)) &&
           !ShouldSelectQaDefectBranch(assignment, diagnostics);

    private static void AddPrimaryArtifactGuidance(
        List<string> lines,
        ProcessStepRecoveryInstructionBuildRequest request,
        IReadOnlyList<RecoveryDiagnosticFact> diagnostics,
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

        if (addedDotNetGuidance ||
            diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal)))
        {
            lines.Add("Read back the solution or product output after the helper runs and verify the required membership/content check passes.");
        }

        lines.Add($"Only then rewrite {primaryArtifactRef} and submit Completed.");
    }

    private static string BuildPrimaryArtifactRef(ProcessStepRecoveryInstructionBuildRequest request)
        => $"artifacts/process-runs/{request.RunId.Value:D}/steps/{request.StepKey}.md";

    private static bool ShouldSelectQaDefectBranch(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<RecoveryDiagnosticFact> diagnostics)
        => IsQaBranchDecisionStep(assignment.StepKey) &&
           diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal));

    private static bool IsQaBranchDecisionStep(string stepKey)
        => string.Equals(stepKey, QaValidationStepKey, StringComparison.Ordinal) ||
           string.Equals(stepKey, QaRecheckStepKey, StringComparison.Ordinal);

    private static bool ShouldPreserveQaBranchOutcomeAfterReceiptRepair(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<RecoveryDiagnosticFact> diagnostics)
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
            return ParseStringList(direct);
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
                    return ParseStringList(property.Value);
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

    private static string SanitizeInstructionLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var normalized = line.ReplaceLineEndings(" ").Trim();
        if (normalized.Length > 900)
        {
            normalized = normalized[..900] + "...";
        }

        return UnresolvedPlaceholderRegex.Replace(normalized, "[unresolved-placeholder omitted]");
    }

    private sealed record RecoveryDiagnosticFact(
        string Code,
        string EvidenceHash,
        string Summary,
        ProcessDiagnosticRetrySafety RetrySafety,
        ProcessDiagnosticIdempotencyClassification Idempotency);

    private sealed record ProductReadbackCheck(
        IReadOnlyList<string> PathCandidates,
        string Description,
        IReadOnlyList<IReadOnlyList<string>> RequiredTextAnyGroups,
        IReadOnlyList<string> ForbiddenTextAny);
}
