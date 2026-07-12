using System.Text.Json;
using System.Text.RegularExpressions;

namespace CanDoItAll.Processes.Templates;

public static partial class ProcessTemplateCompatibilityScanner
{
    private static IReadOnlyList<ProcessTemplateContractDiagnostic> AnalyzeExecutionContracts(
        string processKey,
        JsonElement definition,
        IReadOnlyDictionary<string, JsonElement> definitionsByKey)
    {
        if (!TryGetProperty(definition, "steps", out var steps) ||
            steps.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var diagnostics = new List<ProcessTemplateContractDiagnostic>();
        foreach (var step in steps.EnumerateArray())
        {
            if (!TryGetString(step, "key", out var stepKey))
            {
                continue;
            }

            var executionContract = TryGetProperty(step, "executionContract", out var contract) &&
                                    contract.ValueKind == JsonValueKind.Object
                ? contract
                : (JsonElement?)null;
            var executionClass = ResolveExecutionClass(step, executionContract);
            var hasHardGateProse = LooksLikeHardGateProse(step);
            if (string.IsNullOrWhiteSpace(executionClass))
            {
                if (hasHardGateProse)
                {
                    AddDiagnostic(
                        diagnostics,
                        processKey,
                        stepKey,
                        ProcessTemplateContractDiagnosticKind.ProseOnlyHardGate,
                        "Step contains hard runtime/tool/subprocess gate prose but no typed executionClass or executionContract.");
                }
            }
            else if (!ProcessTemplateStepExecutionClasses.IsKnown(executionClass))
            {
                AddDiagnostic(
                    diagnostics,
                    processKey,
                    stepKey,
                    ProcessTemplateContractDiagnosticKind.InvalidExecutionClass,
                    $"Step declares unknown executionClass '{executionClass}'.");
            }

            ValidateDeterministicToolPlan(processKey, stepKey, executionClass, executionContract, hasHardGateProse, diagnostics);
            ValidateRuntimeOwnedExecutor(processKey, stepKey, executionClass, executionContract, diagnostics);
            ValidateSubprocessContract(processKey, stepKey, step, executionClass, definitionsByKey, diagnostics);
            ValidateBranchContract(processKey, stepKey, step, executionClass, diagnostics);
            ValidateProducedArtifactSlots(processKey, stepKey, step, executionContract, diagnostics);
        }

        return diagnostics;
    }

    private static void ValidateRuntimeOwnedExecutor(
        string processKey,
        string stepKey,
        string executionClass,
        JsonElement? executionContract,
        List<ProcessTemplateContractDiagnostic> diagnostics)
    {
        if (!ProcessTemplateStepExecutionClasses.IsRuntimeOwnedToolPlan(executionClass))
        {
            return;
        }

        if (executionContract is null ||
            !TryGetString(executionContract.Value, "runtimeOwnedExecutorKey", out _))
        {
            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.MissingRuntimeOwnedExecutorKey,
                "RuntimeOwnedToolPlan requires executionContract.runtimeOwnedExecutorKey.");
        }
    }

    private static void ValidateDeterministicToolPlan(
        string processKey,
        string stepKey,
        string executionClass,
        JsonElement? executionContract,
        bool hasHardGateProse,
        List<ProcessTemplateContractDiagnostic> diagnostics)
    {
        var requiresPlan = ProcessTemplateStepExecutionClasses.RequiresDeterministicToolPlan(executionClass);
        if (!requiresPlan && !hasHardGateProse)
        {
            return;
        }

        if (executionContract is null)
        {
            if (requiresPlan || hasHardGateProse)
            {
                AddDiagnostic(
                    diagnostics,
                    processKey,
                    stepKey,
                    ProcessTemplateContractDiagnosticKind.MissingExecutionContract,
                    string.IsNullOrWhiteSpace(executionClass)
                        ? "Hard runtime/tool/subprocess gate prose requires a typed executionContract."
                        : $"Execution class '{executionClass}' requires a typed executionContract.");
            }

            return;
        }

        var hasPlan = TryGetProperty(executionContract.Value, "deterministicToolPlan", out var plan) &&
                      plan.ValueKind == JsonValueKind.Object;
        if (requiresPlan && !hasPlan)
        {
            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.MissingDeterministicToolPlan,
                $"Execution class '{executionClass}' requires deterministicToolPlan metadata.");
            return;
        }

        var contractReceiptCount = CountRequiredReceipts(executionContract.Value);
        var planReceiptCount = hasPlan ? CountRequiredReceipts(plan) : 0;
        if ((requiresPlan || hasHardGateProse) && contractReceiptCount + planReceiptCount == 0)
        {
            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.MissingRequiredReceiptMetadata,
                "Hard runtime tool gates require typed required receipt metadata.");
        }

        if (!hasPlan)
        {
            if (hasHardGateProse && !HasTypedHardGateMetadata(executionContract.Value))
            {
                AddDiagnostic(
                    diagnostics,
                    processKey,
                    stepKey,
                    ProcessTemplateContractDiagnosticKind.ProseOnlyHardGate,
                    "Step mentions hard runtime behavior in prose but lacks typed hard-gate metadata.");
            }

            return;
        }

        ValidateDeterministicToolPlanShape(processKey, stepKey, plan, diagnostics);
    }

    private static void ValidateDeterministicToolPlanShape(
        string processKey,
        string stepKey,
        JsonElement plan,
        List<ProcessTemplateContractDiagnostic> diagnostics)
    {
        var requiresScriptRef = PlanRequiresScriptRef(plan);
        var requiresReadbackChecks = PlanRequiresReadbackChecks(plan);
        var scriptRef = TryGetString(plan, "scriptRef", out var directScriptRef)
            ? directScriptRef
            : string.Empty;
        var scriptRefLaunchVariable = TryGetString(plan, "scriptRefLaunchVariable", out var launchVariable)
            ? launchVariable
            : string.Empty;
        if (requiresScriptRef &&
            string.IsNullOrWhiteSpace(scriptRef) &&
            string.IsNullOrWhiteSpace(scriptRefLaunchVariable))
        {
            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.InvalidDeterministicToolPlan,
                "deterministicToolPlan must declare scriptRef or scriptRefLaunchVariable.");
        }

        if (!string.IsNullOrWhiteSpace(scriptRef) && ContainsUnresolvedTemplateToken(scriptRef))
        {
            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.InvalidDeterministicToolPlan,
                $"deterministicToolPlan scriptRef contains unresolved template placeholder '{scriptRef}'.");
        }

        if (!TryGetProperty(plan, "operations", out var operations) ||
            operations.ValueKind != JsonValueKind.Array ||
            operations.GetArrayLength() == 0)
        {
            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.InvalidDeterministicToolPlan,
                "deterministicToolPlan must declare at least one tool operation.");
            return;
        }

        foreach (var operation in operations.EnumerateArray())
        {
            if (operation.ValueKind != JsonValueKind.Object ||
                !TryGetString(operation, "toolName", out _))
            {
                AddDiagnostic(
                    diagnostics,
                    processKey,
                    stepKey,
                    ProcessTemplateContractDiagnosticKind.InvalidDeterministicToolPlan,
                    "Each deterministicToolPlan operation must declare toolName.");
                return;
            }
        }

        if (requiresReadbackChecks &&
            (!TryGetProperty(plan, "readbackChecks", out var readbackChecks) ||
            readbackChecks.ValueKind != JsonValueKind.Array ||
            readbackChecks.GetArrayLength() == 0))
        {
            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.MissingReadbackChecks,
                "deterministicToolPlan must declare readbackChecks for product state verification.");
        }
    }

    private static bool HasTypedHardGateMetadata(JsonElement executionContract)
        => CountRequiredReceipts(executionContract) > 0 ||
           CountStringArray(executionContract, "requiredRuntimeToolNames") > 0 ||
           CountObjectArray(executionContract, "producedArtifactSlots") > 0 ||
           (TryGetProperty(executionContract, "deterministicToolPlan", out var plan) &&
            plan.ValueKind == JsonValueKind.Object);

    private static bool PlanRequiresScriptRef(JsonElement plan)
    {
        if (TryGetString(plan, "planKind", out var planKind) &&
            planKind.Contains("Script", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return PlanContainsTool(plan, "workspace_pwsh_run_script");
    }

    private static bool PlanRequiresReadbackChecks(JsonElement plan)
        => TryGetProperty(plan, "requiresReadbackChecks", out var value) &&
           value.ValueKind == JsonValueKind.True;

    private static bool PlanContainsTool(JsonElement plan, string toolName)
    {
        if (!TryGetProperty(plan, "operations", out var operations) ||
            operations.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var operation in operations.EnumerateArray())
        {
            if (TryGetString(operation, "toolName", out var candidateToolName) &&
                string.Equals(candidateToolName, toolName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountStringArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountObjectArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                count++;
            }
        }

        return count;
    }

    private static void ValidateSubprocessContract(
        string processKey,
        string stepKey,
        JsonElement step,
        string executionClass,
        IReadOnlyDictionary<string, JsonElement> definitionsByKey,
        List<ProcessTemplateContractDiagnostic> diagnostics)
    {
        var isSubprocess = ProcessTemplateStepExecutionClasses.IsRuntimeOwnedSubprocess(executionClass) ||
                           (TryGetString(step, "stepKind", out var stepKind) &&
                            string.Equals(stepKind, "Subprocess", StringComparison.OrdinalIgnoreCase)) ||
                           TryGetString(step, "subprocessProcessKey", out _);
        if (!isSubprocess)
        {
            return;
        }

        if (!TryGetProperty(step, "subprocessContract", out var contract) ||
            contract.ValueKind != JsonValueKind.Object ||
            !TryGetString(contract, "definitionKey", out var childDefinitionKey))
        {
            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.MissingExecutionContract,
                "Runtime-owned subprocess step must declare subprocessContract.definitionKey.");
            return;
        }

        if (!definitionsByKey.TryGetValue(childDefinitionKey, out var childDefinition))
        {
            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.UnknownSubprocessDefinition,
                $"Subprocess child definition '{childDefinitionKey}' is not present in the template pack.");
            return;
        }

        ValidateChildOutputs(processKey, stepKey, step, childDefinition, contract, "acceptedChildOutputs", diagnostics);
        ValidateChildOutputs(processKey, stepKey, step, childDefinition, contract, "noGoChildOutputs", diagnostics);
    }

    private static void ValidateChildOutputs(
        string processKey,
        string parentStepKey,
        JsonElement parentStep,
        JsonElement childDefinition,
        JsonElement subprocessContract,
        string propertyName,
        List<ProcessTemplateContractDiagnostic> diagnostics)
    {
        if (!TryGetProperty(subprocessContract, propertyName, out var outputs) ||
            outputs.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var output in outputs.EnumerateArray())
        {
            if (output.ValueKind != JsonValueKind.Object ||
                !TryGetString(output, "stepKey", out var childStepKey))
            {
                continue;
            }

            if (!TryFindStep(childDefinition, childStepKey, out var childStep))
            {
                AddDiagnostic(
                    diagnostics,
                    processKey,
                    parentStepKey,
                    ProcessTemplateContractDiagnosticKind.UnknownSubprocessChildOutputStep,
                    $"{propertyName} references unknown child step '{childStepKey}'.");
                continue;
            }

            if (TryGetString(output, "artifactExpectationKey", out var artifactExpectationKey) &&
                !StepHasArtifactExpectation(childStep, artifactExpectationKey))
            {
                AddDiagnostic(
                    diagnostics,
                    processKey,
                    parentStepKey,
                    ProcessTemplateContractDiagnosticKind.UnknownSubprocessChildArtifactExpectation,
                    $"{propertyName} references child step '{childStepKey}' artifact expectation '{artifactExpectationKey}', but the child step does not declare it.");
            }

            if (TryGetString(output, "parentBranchOutcomeKey", out var parentBranchOutcomeKey) &&
                !StepHasBranchOutcome(parentStep, parentBranchOutcomeKey))
            {
                AddDiagnostic(
                    diagnostics,
                    processKey,
                    parentStepKey,
                    ProcessTemplateContractDiagnosticKind.InvalidBranchOutcomeKey,
                    $"{propertyName} routes to unknown parent branch outcome '{parentBranchOutcomeKey}'.");
            }
        }
    }

    private static void ValidateBranchContract(
        string processKey,
        string stepKey,
        JsonElement step,
        string executionClass,
        List<ProcessTemplateContractDiagnostic> diagnostics)
    {
        if (!ProcessTemplateStepExecutionClasses.IsBranchDecision(executionClass) &&
            !TryGetProperty(step, "branchOutcomes", out _))
        {
            return;
        }

        if (!TryGetProperty(step, "branchOutcomes", out var outcomes) ||
            outcomes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var outcome in outcomes.EnumerateArray())
        {
            if (!TryGetString(outcome, "key", out var outcomeKey) ||
                !IsStableIdentifier(outcomeKey))
            {
                AddDiagnostic(
                    diagnostics,
                    processKey,
                    stepKey,
                    ProcessTemplateContractDiagnosticKind.InvalidBranchOutcomeKey,
                    $"Branch outcome key '{outcomeKey}' is not a stable typed identifier.");
            }
        }
    }

    private static void ValidateProducedArtifactSlots(
        string processKey,
        string stepKey,
        JsonElement step,
        JsonElement? executionContract,
        List<ProcessTemplateContractDiagnostic> diagnostics)
    {
        if (executionContract is null ||
            !TryGetProperty(executionContract.Value, "producedArtifactSlots", out var producedSlots) ||
            producedSlots.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var slot in producedSlots.EnumerateArray())
        {
            if (slot.ValueKind != JsonValueKind.Object ||
                !TryGetString(slot, "artifactExpectationKey", out var artifactExpectationKey) ||
                StepHasArtifactExpectation(step, artifactExpectationKey))
            {
                continue;
            }

            AddDiagnostic(
                diagnostics,
                processKey,
                stepKey,
                ProcessTemplateContractDiagnosticKind.MissingProducedArtifactSlot,
                $"Produced artifact slot references missing artifact expectation '{artifactExpectationKey}'.");
        }
    }

    private static string ResolveExecutionClass(JsonElement step, JsonElement? executionContract)
    {
        if (TryGetString(step, "executionClass", out var stepExecutionClass))
        {
            return stepExecutionClass;
        }

        return executionContract is not null &&
               TryGetString(executionContract.Value, "executionClass", out var contractExecutionClass)
            ? contractExecutionClass
            : string.Empty;
    }

    private static bool LooksLikeHardGateProse(JsonElement step)
    {
        var text = string.Join(
            " ",
            ReadOptionalString(step, "title"),
            ReadOptionalString(step, "notes"),
            ReadOptionalString(step, "inputContractSummary"),
            ReadOptionalString(step, "outputContractSummary"),
            ReadOptionalString(step, "evidenceContractSummary"));
        return text.Contains("workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ProductCompletionRequiredToolReceipts", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ProductCompletionRequiredFileContentChecks", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("sideEffectManifest", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("required runtime tool", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("runtime-owned subprocess", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountRequiredReceipts(JsonElement element)
    {
        if (!TryGetProperty(element, "requiredReceipts", out var receipts) ||
            receipts.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return receipts.GetArrayLength();
    }

    private static bool TryFindStep(JsonElement definition, string stepKey, out JsonElement step)
    {
        if (TryGetProperty(definition, "steps", out var steps) &&
            steps.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in steps.EnumerateArray())
            {
                if (TryGetString(candidate, "key", out var candidateKey) &&
                    string.Equals(candidateKey, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    step = candidate;
                    return true;
                }
            }
        }

        step = default;
        return false;
    }

    private static bool StepHasArtifactExpectation(JsonElement step, string artifactExpectationKey)
    {
        if (!TryGetProperty(step, "artifactExpectations", out var expectations) ||
            expectations.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var expectation in expectations.EnumerateArray())
        {
            if (TryGetString(expectation, "key", out var key) &&
                string.Equals(key, artifactExpectationKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StepHasBranchOutcome(JsonElement step, string branchOutcomeKey)
    {
        if (!TryGetProperty(step, "branchOutcomes", out var outcomes) ||
            outcomes.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return outcomes.EnumerateArray().Any(outcome =>
            outcome.ValueKind == JsonValueKind.Object &&
            TryGetString(outcome, "key", out var key) &&
            string.Equals(key, branchOutcomeKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadOptionalString(JsonElement element, string propertyName)
        => TryGetString(element, propertyName, out var value) ? value : string.Empty;

    private static bool ContainsUnresolvedTemplateToken(string value)
        => value.Contains("${", StringComparison.Ordinal) ||
           Regex.IsMatch(value, @"\{[A-Za-z0-9_.:-]+\}", RegexOptions.CultureInvariant);

    private static bool IsStableIdentifier(string value)
        => Regex.IsMatch(value, @"^[a-z][a-z0-9.-]*$", RegexOptions.CultureInvariant);

    private static void AddDiagnostic(
        List<ProcessTemplateContractDiagnostic> diagnostics,
        string processKey,
        string stepKey,
        ProcessTemplateContractDiagnosticKind kind,
        string message)
    {
        diagnostics.Add(new ProcessTemplateContractDiagnostic(processKey, stepKey, kind, message));
    }
}
