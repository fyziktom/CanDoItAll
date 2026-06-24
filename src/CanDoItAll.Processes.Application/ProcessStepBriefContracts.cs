using System.Text;
using CanDoItAll.Processes.Abstractions;
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
    IReadOnlyDictionary<string, string> LaunchVariables);

public sealed class GenericProcessStepBriefBuilder : IProcessStepBriefBuilder
{
    public string Build(ProcessStepBriefBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var step = request.Step;
        var variables = FormatLaunchVariables(request.LaunchVariables);
        var branchOutcomes = FormatBranchOutcomes(step);
        var requiredArtifacts = BuildRequiredArtifactContext(request);
        var producedArtifacts = BuildProducedArtifactContext(request);
        var stepKind = string.IsNullOrWhiteSpace(step.StepKind)
            ? "Work"
            : step.StepKind.Trim();
        var subprocessGuidance = BuildSubprocessGuidance(step);

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

        Return the executor-specific structured step result configured by the selected process driver.
        If branch outcomes are listed, select exactly one listed outcome key when the step result determines a branch.
        """;
    }

    private static string FormatLaunchVariables(IReadOnlyDictionary<string, string> variables)
    {
        return variables.Count == 0
            ? "No launch variables were supplied."
            : string.Join(Environment.NewLine, variables.OrderBy(item => item.Key).Select(item => $"- {item.Key}: {item.Value}"));
    }

    private static string FormatBranchOutcomes(ProcessTemplateDefinitionStepDocument step)
    {
        return step.BranchOutcomes.Count == 0
            ? "No branch outcomes."
            : string.Join(
                Environment.NewLine,
                step.BranchOutcomes.Select(outcome => $"- {outcome.Key}: {outcome.Title} - {outcome.Description}"));
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

        return $"""
        - Slot {slotId}
          Producer step: {sourceStep.Key} - {sourceStep.Title}
          Artifact expectation: {expectationKey} - {expectationTitle} ({artifactKind})
          Artifact refs to inspect (alternatives for this same slot): {BuildStepArtifactPath(request.ManagedArtifactRoot, sourceStep.Key)}; {BuildSlotArtifactRoot(request.ManagedArtifactRoot, slotId)}; {BuildStepArtifactRoot(request.ManagedArtifactRoot, sourceStep.Key)}
          Runtime rule: this slot is available only after the producer completed. Use workspace_stat_path or workspace_read_file on the listed refs and use the first existing readable ref for this slot; do not block only because one alternative ref is missing when another listed ref exists. Project structure is supplemental context, not a substitute for probing these managed artifact refs. If every listed ref is unreadable, cite the failed workspace file-tool receipt before returning Blocked.
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
              Completion rule: consolidate this slot into the primary managed ref first and include that exact primary ref in evidenceRefs before returning Completed. Do not invent sibling output files for this slot unless the step contract explicitly lists them here.
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

        return $"""
        - Slot {slotId}
          Artifact expectation: {expectation.Key} - {title} ({artifactKind})
          Primary write ref: {BuildStepArtifactPath(request.ManagedArtifactRoot, request.Step.Key)}
          Additional write roots: {BuildSlotArtifactRoot(request.ManagedArtifactRoot, slotId)}; {BuildStepArtifactRoot(request.ManagedArtifactRoot, request.Step.Key)}
          Runtime rule: this is your own output, so your first workspace mutation for this slot must create the primary write ref with workspace_write_file or workspace_append_file. Do not list, search, stat, or read this run's managed artifact root to discover your own missing output before that write. Absence of your own output before you write it is expected and is not a blocker.
          Completion rule: consolidate this slot into the primary managed ref first and include that exact primary ref in evidenceRefs before returning Completed. Do not invent sibling output files for this slot unless the step contract explicitly lists them here.
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
            : "Complete only after the child process result and required child artifacts are available through the configured subprocess driver. A stopped child run is historical evidence, not an active wait; inspect it, then complete from valid evidence or relaunch the child when evidence is missing and launch is allowed.";

        return $"""
        - Child process definition key: {subprocessKey}
        - Child definition snapshot name: {snapshotName}
        - Scope rule: use the parent step's assigned project node. Leave ParentProjectNodeId empty unless the parent launch context has no project node. Do not pass ProcessRunNodeId as ParentProjectNodeId.
        - Completion rule: {completionRule}
        - Stopped-child rule: do not return blocked only because a previous child run is Completed, Failed, Cancelled, or Blocked.
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
    public const string Subprocess = "Subprocess";
}
