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
    private const string WorkspacePwshRunScriptToolName = "workspace_pwsh_run_script";
    private const string WorkspaceDotNetNewToolName = "workspace_dotnet_new";
    private const string WorkspaceAliasVariableName = "WorkspaceAlias";
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
        AddDotNetCreateProjectGuidance(lines, request.Assignment);
        AddPrimaryArtifactGuidance(lines, request);

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
            lines.Add($"- {diagnostic.Code}: {diagnostic.Summary}");
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
        foreach (var diagnostic in readbackDiagnostics)
        {
            lines.Add($"- {diagnostic.Summary}");
        }

        var checks = ResolveFileContentChecks(assignment.LaunchVariables, assignment.StepKey);
        foreach (var check in checks)
        {
            var pathText = check.PathCandidates.Count == 0
                ? "configured product file"
                : string.Join(" | ", check.PathCandidates);
            foreach (var requiredGroup in check.RequiredTextAnyGroups)
            {
                if (requiredGroup.Count == 0)
                {
                    continue;
                }

                lines.Add($"Verify readback for {pathText} contains one of: {string.Join(" | ", requiredGroup)}.");
            }
        }
    }

    private static void AddDotNetCreateProjectGuidance(
        List<string> lines,
        ProcessRuntimeStepAssignment assignment)
    {
        if (!TryResolveScriptVariables(
                assignment,
                out var scriptVariableName,
                out var scriptRefVariableName,
                out var manifestVariableName))
        {
            return;
        }

        var scriptRef = TryGetResolvedVariable(assignment.LaunchVariables, scriptRefVariableName);
        if (string.IsNullOrWhiteSpace(scriptRef))
        {
            lines.Add($"Resolved {scriptRefVariableName} is unavailable; fix launch variable resolution before retrying this diagnostic.");
            return;
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
    }

    private static void AddPrimaryArtifactGuidance(
        List<string> lines,
        ProcessStepRecoveryInstructionBuildRequest request)
    {
        var primaryArtifactRef = $"artifacts/process-runs/{request.RunId.Value:D}/steps/{request.StepKey}.md";
        lines.Add("Read back the solution or product output after the helper runs and verify the required membership/content check passes.");
        lines.Add($"Only then rewrite {primaryArtifactRef} and submit Completed.");
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

        return new ProductReadbackCheck(pathCandidates, requiredTextAnyGroups);
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
        IReadOnlyList<IReadOnlyList<string>> RequiredTextAnyGroups);
}
