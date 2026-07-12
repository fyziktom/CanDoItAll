using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public interface IProcessStepBriefBuilder
{
    string Build(ProcessStepBriefBuildRequest request);
}

public sealed record ProcessStepBriefBuildRequest(
    ProcessLaunchRequest LaunchRequest,
    ProcessTemplateDefinitionDocument Definition,
    ProcessTemplateDefinitionStepDocument Step,
    ProcessLaunchExecutorBinding? ExecutorBinding,
    IReadOnlyList<ArtifactSlotId> RequiredSlots,
    IReadOnlyList<ArtifactSlotId> ProducedSlots,
    IReadOnlyDictionary<(string StepKey, string ExpectationKey), ArtifactSlotId> ArtifactSlotByStepExpectation,
    ProcessRunId RunId,
    string ManagedArtifactRoot,
    IReadOnlyDictionary<string, string> LaunchVariables)
{
    public IReadOnlySet<string>? AgentSelectableBranchOutcomeKeys { get; init; }
}

public sealed class GenericProcessStepBriefBuilder : IProcessStepBriefBuilder
{
    private const int MaxLaunchVariableValueCharacters = 2400;
    private const int MaxAcceptanceCriteriaContractCharacters = 12000;
    private const int MaxExecutionGuidanceCharacters = 16000;
    private const int LaunchVariableHeadCharacters = 1600;
    private const int LaunchVariableTailCharacters = 500;

    public string Build(ProcessStepBriefBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var step = request.Step;
        var variables = FormatLaunchVariables(request.LaunchVariables);
        var branchOutcomes = FormatBranchOutcomes(step, request.AgentSelectableBranchOutcomeKeys);
        var requiredArtifacts = BuildRequiredArtifactContext(request);
        var producedArtifacts = BuildProducedArtifactContext(request);
        var stepKind = string.IsNullOrWhiteSpace(step.StepKind)
            ? "Work"
            : step.StepKind.Trim();
        var subprocessGuidance = BuildSubprocessGuidance(step);
        var acceptanceCriteriaGuidance = BuildAcceptanceCriteriaGuidance(request);
        var executionGuidance = BuildExecutionGuidance(step);

        return $"""
        Process step execution brief

        Process: {request.Definition.DisplayName}
        Step key: {step.Key}
        Step title: {step.Title}
        Step kind: {stepKind}
        Role key: {request.ExecutorBinding?.RoleKey ?? ResolvePrimaryRoleKey(step)}
        Requested by: {request.LaunchRequest.RequestedBy}
        Process run id: {request.RunId}
        Managed artifact root: {request.ManagedArtifactRoot}
        Managed artifact path rule: paths under the managed artifact root are workspace-managed relative refs. Use them exactly as shown for managed evidence reads and writes; never prefix them with external-target aliases or absolute output roots.

        Launch variables:
        {variables}

        Step instructions:
        {step.Notes}

        Input contract:
        {step.InputContractSummary}

        Output contract:
        {step.OutputContractSummary}

        Evidence contract:
        {step.EvidenceContractSummary}

        Decision rights:
        {FormatOptionalContractSummary(step.DecisionRightsSummary, "No additional decision rights were declared for this step.")}

        Exception policy:
        {FormatOptionalContractSummary(step.ExceptionPolicySummary, "No additional exception policy was declared for this step.")}

        Template execution guidance:
        {executionGuidance}

        Allowed operations:
        {string.Join(", ", step.AllowedOperations)}

        Operation target scope:
        {NormalizeOperationTargetScope(step.OperationTargetScope)}

        Subprocess mapping:
        {subprocessGuidance}

        Required upstream artifact slots:
        {requiredArtifacts}

        Produced artifact slots:
        {producedArtifacts}

        Available branch outcomes:
        {branchOutcomes}

        Acceptance-criteria completion rule:
        {acceptanceCriteriaGuidance}

        Return the executor-specific structured step result configured by the selected process driver.
        If branch outcomes are listed, every Completed result must select exactly one listed outcome key. Preserve that key when rewriting an artifact or result during recovery.
        """;
    }

    private static string FormatOptionalContractSummary(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string BuildExecutionGuidance(ProcessTemplateDefinitionStepDocument step)
    {
        if (step.ResolvedExecutionGuidance.Count == 0)
        {
            return "No additional template execution guidance was supplied for this step.";
        }

        var builder = new StringBuilder();
        var remainingCharacters = MaxExecutionGuidanceCharacters;
        foreach (var guidance in step.ResolvedExecutionGuidance)
        {
            if (remainingCharacters <= 0)
            {
                break;
            }

            var content = guidance.Content.Trim();
            var boundedContent = content.Length <= remainingCharacters
                ? content
                : string.Concat(
                    content[..Math.Max(0, remainingCharacters - 80)],
                    Environment.NewLine,
                    "[... remaining template execution guidance omitted by the prompt budget ...]");
            builder.AppendLine($"### {guidance.Reference} ({guidance.ContentHash})");
            builder.AppendLine(boundedContent);
            builder.AppendLine();
            remainingCharacters -= boundedContent.Length;
        }

        return builder.Length == 0
            ? "Template execution guidance exceeded the prompt budget. Follow the structured step contracts and escalate a concrete execution boundary."
            : builder.ToString().TrimEnd();
    }

    private static string BuildAcceptanceCriteriaGuidance(ProcessStepBriefBuildRequest request)
    {
        if (!request.LaunchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract,
                out var acceptanceCriteriaContract) ||
            string.IsNullOrWhiteSpace(acceptanceCriteriaContract))
        {
            return "No acceptance-criteria contract was supplied for this step.";
        }

        if (!request.LaunchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys,
                out var rawAcceptanceBranchKeys) ||
            string.IsNullOrWhiteSpace(rawAcceptanceBranchKeys))
        {
            return "This step contributes evidence to a later acceptance owner. Preserve criterion-relevant proof in its managed artifact, but do not claim end-to-end acceptance solely from this step.";
        }

        var acceptanceBranchKeys = SplitLaunchVariableList(rawAcceptanceBranchKeys);
        const string failedCriterionGuidance = "For a non-acceptance branch that reports an observed product or deliverable defect, populate acceptanceCriteriaEvidence for every criterion you directly found to fail. Each failed entry must use the exact criterion id, Status: Failed, a concise observed-failure summary, and at least one grounded current-run proof ref. Do not mark a criterion Failed merely because a later parent-owned proof has not run.";
        return acceptanceBranchKeys.Count == 0
            ? $"This step contributes evidence to a later acceptance owner. Preserve criterion-relevant proof in its managed artifact, but do not claim end-to-end acceptance solely from this step. {failedCriterionGuidance}"
            : $"For these final-acceptance branches only: {string.Join(", ", acceptanceBranchKeys)}. Populate acceptanceCriteriaEvidence in the final structured result for every criterion id in ProductAcceptanceCriteriaContract. Each entry must use the exact id, Status: Passed, a concise criterion-specific summary, and at least one current-run proof ref. Do not select one of those branches when any required criterion cannot be recorded this way; select the template's applicable non-acceptance branch instead. {failedCriterionGuidance}";
    }

    private static IReadOnlyList<string> SplitLaunchVariableList(string value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string FormatLaunchVariables(IReadOnlyDictionary<string, string> variables)
    {
        var visibleVariables = variables
            .Where(pair => ProcessAgentVisibleLaunchVariablePolicy.IsVisible(pair.Key))
            .OrderBy(pair => pair.Key)
            .ToArray();
        return visibleVariables.Length == 0
            ? "No launch variables were supplied."
            : string.Join(Environment.NewLine, visibleVariables.Select(item => $"- {item.Key}: {FormatLaunchVariableValue(item.Key, item.Value)}"));
    }

    private static string FormatLaunchVariableValue(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        var maxCharacters = string.Equals(key, ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract, StringComparison.OrdinalIgnoreCase)
            ? MaxAcceptanceCriteriaContractCharacters
            : MaxLaunchVariableValueCharacters;
        if (normalized.Length <= maxCharacters)
        {
            return normalized;
        }

        var omittedCharacters = normalized.Length - LaunchVariableHeadCharacters - LaunchVariableTailCharacters;
        return string.Concat(
            normalized[..LaunchVariableHeadCharacters],
            Environment.NewLine,
            $"[... launch variable truncated; {omittedCharacters} character(s) omitted ...]",
            Environment.NewLine,
            normalized[^LaunchVariableTailCharacters..]);
    }

    private static string FormatBranchOutcomes(
        ProcessTemplateDefinitionStepDocument step,
        IReadOnlySet<string>? agentSelectableBranchOutcomeKeys)
    {
        var outcomes = agentSelectableBranchOutcomeKeys is null
            ? step.BranchOutcomes
            : step.BranchOutcomes
                .Where(outcome => agentSelectableBranchOutcomeKeys.Contains(outcome.Key))
                .ToList();
        return outcomes.Count == 0
            ? "No branch outcomes."
            : string.Join(
                Environment.NewLine,
                outcomes.Select(outcome => $"- {outcome.Key}: {outcome.Title} - {outcome.Description}"));
    }

    private static string BuildRequiredArtifactContext(ProcessStepBriefBuildRequest request)
    {
        if (request.RequiredSlots.Count == 0)
        {
            return "No required upstream artifact slots.";
        }

        var requiredSlotSet = request.RequiredSlots.ToHashSet();
        var describedSlots = new HashSet<ArtifactSlotId>();
        var stepsByKey = request.Definition.Steps.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();

        foreach (var input in request.Step.ArtifactInputs)
        {
            var sourceStepKey = NormalizeOptional(input.SourceStepKey, string.Empty);
            var expectationKey = NormalizeOptional(input.ArtifactExpectationKey, string.Empty);
            if (string.IsNullOrWhiteSpace(sourceStepKey) ||
                string.IsNullOrWhiteSpace(expectationKey) ||
                !request.ArtifactSlotByStepExpectation.TryGetValue((sourceStepKey, expectationKey), out var slotId) ||
                !requiredSlotSet.Contains(slotId) ||
                !stepsByKey.TryGetValue(sourceStepKey, out var sourceStep))
            {
                continue;
            }

            var expectation = sourceStep.ArtifactExpectations.FirstOrDefault(item =>
                string.Equals(item.Key, expectationKey, StringComparison.OrdinalIgnoreCase));
            lines.Add(FormatRequiredArtifactContext(request, slotId, sourceStep, expectationKey, expectation));
            describedSlots.Add(slotId);
        }

        foreach (var slotId in request.RequiredSlots.Where(slotId => !describedSlots.Contains(slotId)))
        {
            lines.Add($"""
            - Slot {slotId}
              Producer context: unresolved from template artifact input mapping.
              Runtime rule: this slot was available before scheduling this step. Inspect upstream step summaries and managed process artifacts first.
            """);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatRequiredArtifactContext(
        ProcessStepBriefBuildRequest request,
        ArtifactSlotId slotId,
        ProcessTemplateDefinitionStepDocument sourceStep,
        string expectationKey,
        ProcessTemplateDefinitionArtifactExpectationDocument? expectation)
    {
        var expectationTitle = string.IsNullOrWhiteSpace(expectation?.Title)
            ? expectationKey
            : expectation.Title.Trim();
        var artifactKind = string.IsNullOrWhiteSpace(expectation?.ArtifactKind)
            ? "Artifact"
            : expectation.ArtifactKind.Trim();
        var validation = string.IsNullOrWhiteSpace(expectation?.ValidationRequirementSummary)
            ? "Use the producer step output contract and evidence contract."
            : expectation.ValidationRequirementSummary.Trim();
        var payloadSchema = string.IsNullOrWhiteSpace(expectation?.PayloadSchema)
            ? "No payload schema is declared."
            : expectation.PayloadSchema.Trim();

        return $"""
        - Slot {slotId}
          Producer step: {sourceStep.Key} - {sourceStep.Title}
          Artifact expectation: {expectationKey} - {expectationTitle} ({artifactKind})
          Payload schema: {payloadSchema}
          Artifact refs to inspect (alternatives for this same slot): {BuildStepArtifactPath(request.ManagedArtifactRoot, sourceStep.Key)}; {BuildSlotArtifactRoot(request.ManagedArtifactRoot, slotId)}; {BuildStepArtifactRoot(request.ManagedArtifactRoot, sourceStep.Key)}
          Expectation key rule: the artifact expectation key is a contract label, not a filename. Do not invent a managed file named {BuildStepArtifactPath(request.ManagedArtifactRoot, expectationKey)} unless that exact ref is listed as an artifact ref above. When one producer step creates multiple slots, its primary completed-step artifact ref can satisfy each slot when it is readable.
          Runtime rule: this slot is available only after the producer completed. Use workspace_stat_path or workspace_read_file on the listed refs and use the first existing readable ref for this slot; do not block only because one alternative ref is missing when another listed ref exists. A successful stat or read of a listed current-run ref is process evidence for this step; do not return Blocked claiming no prior assistant text, tool result, or process artifact evidence after that. Project structure is supplemental context, not a substitute for probing these managed artifact refs. If every listed ref is unreadable, cite the failed workspace file-tool receipt before returning Blocked.
          Validation: {validation}
        """;
    }

    private static string BuildProducedArtifactContext(ProcessStepBriefBuildRequest request)
    {
        if (request.ProducedSlots.Count == 0)
        {
            return "No produced artifact slots.";
        }

        var producedSlotSet = request.ProducedSlots.ToHashSet();
        var describedSlots = new HashSet<ArtifactSlotId>();
        var lines = new List<string>();

        foreach (var expectation in request.Step.ArtifactExpectations)
        {
            if (!request.ArtifactSlotByStepExpectation.TryGetValue((request.Step.Key, expectation.Key), out var slotId) ||
                !producedSlotSet.Contains(slotId))
            {
                continue;
            }

            lines.Add(FormatProducedArtifactContext(request, slotId, expectation));
            describedSlots.Add(slotId);
        }

        foreach (var slotId in request.ProducedSlots.Where(slotId => !describedSlots.Contains(slotId)))
        {
            lines.Add($"""
            - Slot {slotId}
              Primary write ref: {BuildStepArtifactPath(request.ManagedArtifactRoot, request.Step.Key)}
              Additional write root: {BuildSlotArtifactRoot(request.ManagedArtifactRoot, slotId)}
              Runtime rule: this is your own output, so your first workspace mutation for this slot must create the primary write ref with workspace_write_file or workspace_append_file. Do not list, search, stat, or read this run's managed artifact root to discover your own missing output before that write. Absence of your own output before you write it is expected and is not a blocker.
              Completion rule: consolidate this slot into the primary managed ref first and include that exact primary ref in evidenceRefs before returning Completed. If you read or observed any required current-run artifact, write this primary managed ref next instead of returning a generic no-prior-evidence blocker. Do not invent sibling output files for this slot unless the step contract explicitly lists them here.
            """);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatProducedArtifactContext(
        ProcessStepBriefBuildRequest request,
        ArtifactSlotId slotId,
        ProcessTemplateDefinitionArtifactExpectationDocument expectation)
    {
        var title = string.IsNullOrWhiteSpace(expectation.Title)
            ? expectation.Key
            : expectation.Title.Trim();
        var artifactKind = string.IsNullOrWhiteSpace(expectation.ArtifactKind)
            ? "Artifact"
            : expectation.ArtifactKind.Trim();
        var validation = string.IsNullOrWhiteSpace(expectation.ValidationRequirementSummary)
            ? "Use this step's output contract and evidence contract."
            : expectation.ValidationRequirementSummary.Trim();
        var payloadSchema = string.IsNullOrWhiteSpace(expectation.PayloadSchema)
            ? "No payload schema is declared."
            : expectation.PayloadSchema.Trim();

        return $"""
        - Slot {slotId}
          Artifact expectation: {expectation.Key} - {title} ({artifactKind})
          Payload schema: {payloadSchema}
          Primary write ref: {BuildStepArtifactPath(request.ManagedArtifactRoot, request.Step.Key)}
          Additional write roots: {BuildSlotArtifactRoot(request.ManagedArtifactRoot, slotId)}; {BuildStepArtifactRoot(request.ManagedArtifactRoot, request.Step.Key)}
          Runtime rule: this is your own output, so your first workspace mutation for this slot must create the primary write ref with workspace_write_file or workspace_append_file. Do not list, search, stat, or read this run's managed artifact root to discover your own missing output before that write. Absence of your own output before you write it is expected and is not a blocker.
          Completion rule: consolidate this slot into the primary managed ref first and include that exact primary ref in evidenceRefs before returning Completed. If you read or observed any required current-run artifact, write this primary managed ref next instead of returning a generic no-prior-evidence blocker. Do not invent sibling output files for this slot unless the step contract explicitly lists them here.
          Validation: {validation}
        """;
    }

    private static string BuildSubprocessGuidance(ProcessTemplateDefinitionStepDocument step)
    {
        var isSubprocessStep = string.Equals(step.StepKind, ProcessTemplateStepKinds.Subprocess, StringComparison.OrdinalIgnoreCase) ||
                               !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
        if (!isSubprocessStep)
        {
            return "No subprocess mapping.";
        }

        var subprocessKey = string.IsNullOrWhiteSpace(step.SubprocessProcessKey)
            ? "not mapped"
            : step.SubprocessProcessKey.Trim();
        var snapshotName = string.IsNullOrWhiteSpace(step.SubprocessDefinitionSnapshotName)
            ? "not supplied"
            : step.SubprocessDefinitionSnapshotName.Trim();
        var completionRule = string.IsNullOrWhiteSpace(step.SubprocessProcessKey)
            ? "This step is marked as a subprocess but has no child process definition key. Return a blocked result unless upstream evidence already supplies the missing child run."
            : "Complete only after the child process result and required child artifacts are available through the configured subprocess driver. A stopped child run is historical evidence, not an active wait; inspect it, then complete from valid evidence or propagate a concrete child blocker. Relaunch only when the stopped child has no blocker/escalation evidence and the missing evidence is recoverable by another child attempt.";

        return $"""
        - Child process definition key: {subprocessKey}
        - Child definition snapshot name: {snapshotName}
        - Scope rule: use the parent step's assigned project node. Leave ParentProjectNodeId empty unless the parent launch context has no project node. Do not pass ProcessRunNodeId as ParentProjectNodeId.
        - Completion rule: {completionRule}
        - Child-outcome rule: when the subprocess launch tool returns ParentDeferredOutcomeJson, submit that JSON exactly. Running children defer the parent; Completed children complete the parent from child evidence; stopped children propagate their concrete blocker. Do not relaunch a Blocked child or a child with escalation/no-go evidence.
        """;
    }

    private static string BuildStepArtifactPath(string artifactRoot, string stepKey)
        => $"{artifactRoot}/steps/{SanitizePathSegment(stepKey)}.md";

    private static string BuildSlotArtifactRoot(string artifactRoot, ArtifactSlotId slotId)
        => $"{artifactRoot}/{slotId}";

    private static string BuildStepArtifactRoot(string artifactRoot, string stepKey)
        => $"{artifactRoot}/{SanitizePathSegment(stepKey)}/";

    private static string SanitizePathSegment(string value)
    {
        var normalized = NormalizeOptional(value, "step");
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.Length == 0 ? "step" : builder.ToString();
    }

    private static string ResolvePrimaryRoleKey(ProcessTemplateDefinitionStepDocument step)
    {
        return step.RoleAssignments
            .OrderBy(assignment => assignment.FallbackOrder)
            .Select(assignment => assignment.RoleKey)
            .FirstOrDefault(roleKey => !string.IsNullOrWhiteSpace(roleKey)) ?? string.Empty;
    }

    private static string NormalizeOperationTargetScope(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string NormalizeOptional(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
}

public static class ProcessTemplateStepKinds
{
    public const string Activity = "Activity";
    public const string End = "End";
    public const string Subprocess = "Subprocess";
    public const string Work = "Work";
}
