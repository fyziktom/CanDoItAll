using System.Text.RegularExpressions;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public static class ProcessRuntimeRecoveryInstructionHeadings
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

public sealed record ProcessRecoveryDiagnosticFact(
    string Code,
    string EvidenceHash,
    string Summary,
    ProcessDiagnosticRetrySafety RetrySafety,
    ProcessDiagnosticIdempotencyClassification Idempotency);

public sealed record ProcessStepRecoveryAdviceContext(
    ProcessStepRecoveryInstructionBuildRequest Request,
    IReadOnlyList<ProcessRecoveryDiagnosticFact> Diagnostics);

public interface IProcessRecoveryAdviceProvider
{
    bool CanHandle(ProcessStepRecoveryAdviceContext context);

    IReadOnlyList<string> BuildAdvice(ProcessStepRecoveryAdviceContext context);
}

public sealed class GenericProcessRecoveryAdviceProvider : IProcessRecoveryAdviceProvider
{
    private const string ProductRequiredFileContentMissingCode = "process.adapter.product_required_file_content_missing";

    public bool CanHandle(ProcessStepRecoveryAdviceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Diagnostics.Any(diagnostic =>
            IsRequiredToolReceiptDiagnostic(diagnostic.Code) ||
            string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal));
    }

    public IReadOnlyList<string> BuildAdvice(ProcessStepRecoveryAdviceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lines = new List<string>();
        if (context.Diagnostics.Any(diagnostic => IsRequiredToolReceiptDiagnostic(diagnostic.Code)))
        {
            lines.Add("Generic receipt recovery: satisfy the missing current-run receipt contract before rewriting the managed artifact or returning a final outcome.");
        }

        if (context.Diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal)))
        {
            lines.Add("Generic product readback recovery: inspect the configured product checks and use process route metadata to decide whether the evidence is a product defect, same-step omission, or blocker.");
        }

        return lines;
    }

    private static bool IsRequiredToolReceiptDiagnostic(string code)
        => string.Equals(code, "process.adapter.product_required_tool_receipt_missing", StringComparison.Ordinal) ||
           string.Equals(code, "process.adapter.product_required_tool_receipt_blocked_retry", StringComparison.Ordinal) ||
           string.Equals(code, "process.adapter.required_tool_receipt_missing", StringComparison.Ordinal) ||
           string.Equals(code, "process.adapter.required_tool_receipt_blocked_retry", StringComparison.Ordinal);
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
    private static readonly Regex UnresolvedPlaceholderRegex = new(@"\{[A-Za-z][A-Za-z0-9_.:-]*\}", RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<IProcessRecoveryAdviceProvider> adviceProviders;

    public ProcessStepRecoveryInstructionBuilder()
        : this([new GenericProcessRecoveryAdviceProvider()])
    {
    }

    public ProcessStepRecoveryInstructionBuilder(IEnumerable<IProcessRecoveryAdviceProvider> adviceProviders)
    {
        ArgumentNullException.ThrowIfNull(adviceProviders);

        this.adviceProviders = adviceProviders.ToArray();
    }

    public static ProcessStepRecoveryInstructionBuilder Instance { get; } = new();

    public ProcessStepRecoveryInstruction Build(ProcessStepRecoveryInstructionBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Assignment);

        var diagnostics = CollectDiagnostics(request).ToArray();
        var adviceContext = new ProcessStepRecoveryAdviceContext(request, diagnostics);
        if (diagnostics.Length == 0 ||
            !diagnostics.Any(IsDiagnosticRecoveryCandidate) &&
            !adviceProviders.Any(provider => provider.CanHandle(adviceContext)))
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
        var providerAddedAdvice = AddProviderAdvice(lines, adviceContext);
        AddPrimaryArtifactGuidance(lines, request, diagnostics, providerAddedAdvice);

        var text = string.Join(
            Environment.NewLine,
            lines
                .Select(SanitizeInstructionLine)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        return string.IsNullOrWhiteSpace(text)
            ? ProcessStepRecoveryInstruction.Empty
            : new ProcessStepRecoveryInstruction(text);
    }

    private static IEnumerable<ProcessRecoveryDiagnosticFact> CollectDiagnostics(ProcessStepRecoveryInstructionBuildRequest request)
    {
        if (request.StrategyResult is not null)
        {
            foreach (var diagnostic in request.StrategyResult.Diagnostics)
            {
                yield return new ProcessRecoveryDiagnosticFact(
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
            yield return new ProcessRecoveryDiagnosticFact(
                diagnostic.Code,
                diagnostic.EvidenceHash,
                diagnostic.SafeSummary,
                diagnostic.RetrySafety,
                diagnostic.Idempotency);
        }
    }

    private static bool IsDiagnosticRecoveryCandidate(ProcessRecoveryDiagnosticFact diagnostic)
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
            lines.Add($"Retry budget: automatic {decision.AutomaticRetryAttempt}/{decision.MaximumAutomaticRetryAttempts}; persistent diagnostic identity {decision.SameDiagnosticFingerprintAttempt}/{decision.MaximumSameDiagnosticFingerprintAttempts}; identity {decision.DiagnosticFingerprint}.");
        }

        if (decision.DecisionKind == ProcessRecoveryDecisionKind.ManagerRequired &&
            string.Equals(decision.Policy, "process.current-step-safe-retry-budget-exhausted", StringComparison.Ordinal))
        {
            lines.Add("Safe retry budget is exhausted; keep this attempted repair plan attached for manager review instead of dispatching a blind retry.");
        }
    }

    private static void AddDiagnosticCodes(List<string> lines, IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics)
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
            lines.Add("Missing current-run receipt(s): the completion gate reported required receipt evidence, but the assignment did not expose exact receipt names.");
            return;
        }

        lines.Add("Missing current-run receipt(s):");
        foreach (var receipt in requiredReceipts)
        {
            lines.Add($"- {receipt}");
        }

        lines.Add("Invoke each listed tool now in this exact execution attempt before finalizing. The gate accepts only receipts whose execution id belongs to this attempt; upstream receipts and receipts from prior attempts of this step do not count. Artifacts, summaries, planned actions, or text claiming verification are not current-execution tool receipts.");
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

        lines.Add("Product readback failure(s):");
        lines.Add("Every listed product readback failure is authoritative for this retry because the product content/readback check failed:");
        var failures = readbackDiagnostics
            .SelectMany(diagnostic => ProcessProductReadbackFailureParser.Parse(
                diagnostic.Summary,
                assignment.LaunchVariables))
            .DistinctBy(failure => failure.Description, StringComparer.Ordinal)
            .ToArray();
        foreach (var failure in failures)
        {
            lines.Add($"- {failure.Description}");
        }

        if (failures.Any(failure => failure.Kind == ProcessProductReadbackFailureKind.ForbiddenTextPresent))
        {
            lines.Add("Forbidden-text repair obligation: remove every listed forbidden alternative from its file, or delete the file when the product contract allows it. Do not preserve or rewrite the same forbidden text. Re-read each listed path and search for every listed alternative; if any listed alternative remains, do not submit Completed.");
        }

        if (failures.Any(failure => failure.Kind == ProcessProductReadbackFailureKind.RequiredTextMissing))
        {
            lines.Add("Required-text repair obligation: add at least one listed required alternative to its file. Re-read each listed path and verify the required text is present before submitting Completed.");
        }

        lines.Add("Mutate or remove every failing product file/content marker, read each affected file back, and rerun required validation. Do not complete after changing only visible or linked files while dormant product files still fail configured checks.");
    }

    private static void AddUngroundedReferenceGuidance(
        List<string> lines,
        ProcessStepRecoveryInstructionBuildRequest request,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics)
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

    private bool AddProviderAdvice(List<string> lines, ProcessStepRecoveryAdviceContext context)
    {
        var added = false;
        foreach (var provider in adviceProviders.Where(provider => provider.CanHandle(context)))
        {
            foreach (var line in provider.BuildAdvice(context))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                lines.Add(line);
                added = true;
            }
        }

        return added;
    }

    private static void AddPrimaryArtifactGuidance(
        List<string> lines,
        ProcessStepRecoveryInstructionBuildRequest request,
        IReadOnlyList<ProcessRecoveryDiagnosticFact> diagnostics,
        bool providerAddedAdvice)
    {
        if (providerAddedAdvice)
        {
            return;
        }

        if (diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, ProductRequiredFileContentMissingCode, StringComparison.Ordinal)))
        {
            lines.Add("Read back the affected product output and verify the configured completion gate passes.");
        }

        lines.Add($"Only then rewrite {BuildPrimaryArtifactRef(request)} and submit Completed.");
    }

    private static string BuildPrimaryArtifactRef(ProcessStepRecoveryInstructionBuildRequest request)
        => $"artifacts/process-runs/{request.RunId.Value:D}/steps/{request.StepKey}.md";

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
}
