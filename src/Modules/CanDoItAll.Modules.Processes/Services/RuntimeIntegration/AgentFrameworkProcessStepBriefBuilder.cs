using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;


internal sealed class AgentFrameworkProcessStepBriefBuilder : IProcessStepBriefBuilder, IProcessPromptCompositionDriver
{
    private const string SubprocessLaunchToolName = "project_structure_process_subprocess_launch";
    private readonly GenericProcessStepBriefBuilder genericBuilder = new();

    public DriverId DriverId => StandardProcessAdapterDriverIds.Workflow;

    public bool CanCompose(ProcessStepBriefBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return true;
    }

    public string Compose(ProcessStepBriefBuildRequest request)
        => Build(request);

    public string Build(ProcessStepBriefBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var genericBrief = genericBuilder.Build(request);
        var subprocessGuidance = BuildSubprocessGuidance(request.Step);
        var dependencyArtifactGuidance = BuildDependencyArtifactGuidance(request);
        var ownOutputBootstrapGuidance = BuildOwnOutputBootstrapGuidance(request);
        var projectStructureContextGuidance = BuildProjectStructureContextGuidance(request);
        var productMutationGuidance = BuildProductMutationGuidance(request);
        var scopedInstructionGuidance = BuildScopedInstructionGuidance(request.Step.CapabilityScope);

        return $"""
        {genericBrief}

        AgentFramework execution contract:
        This is a tool-backed process step, not a chat-only response. First use the available workspace, project-structure, subprocess, runtime, validation, or browser tools needed by the step contract. Only after the required evidence exists, submit the final process_step_outcome_result through the required finalizer tool. If the runtime explicitly asks for a JSON fallback, return one JSON object matching that same contract.
        Use Status Completed when the step is done, Blocked when required input or tools are missing, Failed for unrecoverable execution failure, or WaitingApproval when a human approval is required.
        If branch outcomes are listed, set BranchOutcomeKey to exactly one listed outcome key. Selecting a branch outcome means the step completed its decision work, even when the selected branch sends downstream repair, recheck, escalation, rejection, or no-go work. Use Status Completed with BranchOutcomeKey for those evidence-backed branch decisions; use Status Blocked only when no branch can be selected because a concrete tool, input, permission, policy, approval, or environment boundary prevents the decision.

        AgentFramework manager escalation rule:
        If you are blocked by a missing or denied tool, permission, right, capability, workspace boundary, approval path, or policy contract, make the first nextActions entry a manager action request. Include the assigned agent name, step key, process run id, denied tool or right, allowed operations, operation target scope, and whether the manager should grant the right to this agent or reassign the step to an agent that already has it. Include the exact policy or tool denial text in Reason and in HumanReadableSummaryMarkdown; do not only say that you cannot proceed.
        Do not return Blocked only because the required work has not been attempted yet. When the step has the needed operations and tools are available, use the tools now and return Blocked only after a current denied-tool receipt, missing mandatory input, explicit approval wait, policy boundary, or unrecoverable environment failure proves the step cannot proceed.
        Current-run helper script ordering rule: before invoking workspace_pwsh_run_script or workspace_python_run_file for a managed helper script, create or overwrite that helper with workspace_write_file or workspace_append_file, verify the exact helper path with workspace_stat_path or workspace_read_file, then invoke the script execution tool. If a prior script invocation was denied because the helper path did not exist or could not be inspected, create or verify the helper and retry the script execution tool before returning Blocked.

        AgentFramework evidence citation rule:
        Cite a managed artifact ref, external-target alias, project-structure node id, source document id, or current-run tool receipt ref only when it is present in the current step brief, launch variables, required upstream artifact refs, current project-structure tool output, or a current-run tool receipt/readback. Do not put native absolute filesystem paths, scoped storage paths under artifacts/scopes/..., project-media file paths, managed-files paths, tool-runs stdout/stderr paths, or SourceDocLink values in managed artifact bodies, reason, summary, next actions, or evidenceRefs. A native or storage path-like value remains non-citable final evidence even when it appears in launch variables, project-structure context, source-document metadata, retry diagnostics, or previous failed attempts. Translate current product paths to external-target aliases. Cite source documents by stable document id, node id, or title instead of path-like storage refs. Do not say a source document was provided, inherited, or inspected unless one of those current-run sources contains the exact non-path source id or node id.

        Project-scoped launch context:
        Project id: {request.LaunchRequest.ProjectId?.ToString("D") ?? "not scoped"}
        Project node id: {request.LaunchRequest.ProjectNodeId ?? "not scoped"}

        AgentFramework project-structure context source:
        {projectStructureContextGuidance}

        AgentFramework product mutation gate:
        {productMutationGuidance}

        AgentFramework process-scoped instructions:
        {scopedInstructionGuidance}

        AgentFramework evidence write rule:
        Write process step summaries, proof, screenshots, logs, and handoff notes under the managed artifact root or a child path. Managed artifact refs are workspace-managed relative paths; use them exactly as shown and never convert them to external-target paths. Include the written managed artifact paths from this brief in evidenceRefs; if a workspace tool echoes a longer scoped storage path for the same artifact, ignore that scoped echo in artifact prose and evidenceRefs. Do not write evidence under output/ unless this step is explicitly mutating a managed product output path.
        Every primary managed Markdown artifact must include exactly one Status line near the top before step-specific sections: `Status: Completed`, `Status: Blocked`, `Status: Failed`, `Status: WaitingApproval`, or `Status: Refused`. Use the same status in the final process_step_outcome_result. When a branch outcome is selected, the artifact status and finalizer status must be `Completed`, with the branch key carrying the disposition. Include an exact `Branch outcome key: <listed-key>` line in the artifact for every selected branch outcome. These lines are part of the runtime recovery contract if the provider stream fails after writing evidence.
        If Produced artifact slots are listed, the first workspace mutation for that produced output must be workspace_write_file or workspace_append_file to the listed Primary write ref. Do not list, search, stat, or read this run's managed artifact root to discover your own missing output before that write. For intake, planning, scope, architecture, review, governance, or summary steps with no required upstream slot, write a managed Markdown artifact with assumptions and known gaps instead of blocking on optional context. Do not finalize Completed with an empty evidenceRefs array.

        AgentFramework own-output bootstrap:
        {ownOutputBootstrapGuidance}

        AgentFramework dependency artifact refs:
        {dependencyArtifactGuidance}

        AgentFramework upstream artifact read rule:
        When Required upstream artifact slots or AgentFramework dependency artifact refs list managed refs, call workspace_stat_path or workspace_read_file on those exact refs before using project-structure hierarchy as fallback context. Project-structure nodes may summarize a run, but upstream process artifacts are read through workspace file tools. Do not abbreviate, ellipsize, shorten, or guess managed refs; copy the full ref from this brief into the workspace tool call. Do not return Blocked for missing intake, design, implementation, QA, screenshot, runtime, or release evidence until every listed managed ref for the needed slot has a current failed workspace file-tool receipt.
        Artifact expectation keys are contract labels, not managed filenames. Do not invent files named after expectation keys, such as feature-acceptance-criteria.md, when the brief lists a producer step artifact like feature-slice-intake.md. If launch variables contain acceptance criteria or the producer step artifact is readable, use that evidence and write this step's own managed artifact instead of blocking on an invented sibling file.

        Project-structure evidence hygiene:
        Do not create project-structure nodes for every subprocess, intermediate screenshot, log, or step detail. Keep subprocess detail in managed artifacts and live-process history. For multi-team app delivery, the visible project structure should contain one root process run plus only the durable handoff nodes the process asks for: the final accepted screenshot ImageAsset, one run-app proof node, one run-tests proof node, and one manager summary node describing what was built, how it works, and current validation state.

        AgentFramework subprocess adapter guidance:
        {subprocessGuidance}
        """;
    }

    private static string BuildScopedInstructionGuidance(ProcessCapabilityScope capabilityScope)
    {
        var normalized = ProcessCapabilityScope.Normalize(capabilityScope);
        if (normalized.InstructionFragments.Count == 0)
        {
            return "No additional process-scoped instructions.";
        }

        var lines = new List<string>
        {
            "These instructions are supplied by the current process step and apply only to this run:"
        };
        foreach (var fragment in normalized.InstructionFragments)
        {
            var title = string.IsNullOrWhiteSpace(fragment.Title)
                ? NormalizeScopedInstructionTitle(fragment.Key)
                : fragment.Title.Trim();
            lines.Add($"- {title}: {fragment.Content.Trim()}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeScopedInstructionTitle(string key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? "Scoped instruction"
            : key.Trim();
    }

    private static string BuildProjectStructureContextGuidance(ProcessStepBriefBuildRequest request)
    {
        var lines = new List<string>();
        var canReadProjectStructure = request.Step.AllowedOperations.Contains(
            ProcessOperationContractNames.ReadProjectStructure,
            StringComparer.OrdinalIgnoreCase);

        if (TryResolveLaunchVariable(request.LaunchVariables, "ProjectStructureContextSummary", out _))
        {
            lines.Add("ProjectStructureContextSummary in Launch variables is the current project-structure context for this run; treat it as authoritative project-structure evidence when no richer project-structure tool result is required by the step.");
            lines.Add("When using project-structure as source context, treat authored requirement, brief, spec, target, and explicitly current-run managed artifacts as source evidence. Ignore generated process evidence from prior runs such as proof files, screenshots, logs, execution reports, validation reports, summaries, and handoff packets unless the current step names the exact current-run artifact ref. Listed visual target ImageAsset nodes remain binding design inputs.");
            lines.Add("Path-like storage details in ProjectStructureContextSummary are lookup context only. Do not copy native absolute paths, scoped storage paths, project-media paths, managed-files paths, tool-runs paths, or SourceDocLink values from that summary into managed artifacts or final outcome fields.");
        }
        else if (canReadProjectStructure)
        {
            lines.Add("This step may read project structure, but no ProjectStructureContextSummary launch variable was supplied; use available project-structure tools or the supplied launch variables instead of inventing a managed file path.");
        }

        if (request.LaunchVariables.Keys.Any(IsTypedLaunchContractVariableName))
        {
            lines.Add("Launch variables whose names end with Contract are typed project-structure facts for this process; use them for the specific scaffold, output, validation, and handoff decisions described by those variables.");
        }

        if (TryResolveLaunchVariable(request.LaunchVariables, "ProjectStructureContextSummary", out var contextSummary) &&
            ContainsVisualTargetAssetSummary(contextSummary))
        {
            lines.Add("The project-structure context lists visual target assets. For visible UI implementation, preserve the listed ImageAsset node ids and media paths as source design inputs. For QA or repair validation, fetch or analyze the relevant image asset content and compare the delivered screenshot against that visual target before accepting visual alignment. Do not accept visual quality from generated app screenshots in isolation when a source target image is listed.");
            lines.Add("Exact visual target media path rule: when ProjectStructureContextSummary lists a visual target with media=managed-files/project-media/..., copy that exact media value into workspace_inspect_image, workspace_analyze_image, or workspace_analyze_images. Do not replace the project-media directory segment with the agent id, process id, project title, or a guessed folder. If an image tool fails for a different project-media path, retry once with the exact media= value from ProjectStructureContextSummary or resolve the ImageAsset content with project-structure tools before returning Blocked.");
        }

        if (TryResolveLaunchVariable(request.LaunchVariables, "ProductRoot", out _) ||
            TryResolveLaunchVariable(request.LaunchVariables, "OutputRoot", out _) ||
            TryResolveLaunchVariable(request.LaunchVariables, "ExternalTargetRoot", out _))
        {
            var aliases = ResolveLaunchExternalTargetAliases(request.LaunchVariables);
            var aliasSummary = aliases.Count == 0
                ? "No normalized external-target alias was resolved from launch variables; use only managed artifact refs unless a tool result supplies a grounded alias."
                : $"Grounded external-target aliases for structured workspace tool path arguments: {string.Join("; ", aliases)}.";
            lines.Add($"ProductRoot, OutputRoot, and ExternalTargetRoot launch variables identify the product target. {aliasSummary} Do not call workspace_read_file, workspace_stat_path, workspace_list_files, workspace_search, workspace_copy_path, workspace_analyze_image, or other structured workspace path tools with native absolute ProductRoot or OutputRoot paths. If a workspace-tool denial supplies a replacement external-target alias, retry the same structured workspace tool with that alias before returning Blocked.");
        }

        if (TryResolveLaunchVariable(request.LaunchVariables, "ParentProcessRunId", out _) ||
            TryResolveLaunchVariable(request.LaunchVariables, "SubprocessDefinitionKey", out _))
        {
            lines.Add("For subprocess runs, parent launch variables are copied into the child run; ParentProcessRunId, ParentProcessStepKey, and SubprocessDefinitionKey are metadata, not managed artifact refs.");
        }

        if (lines.Count == 0)
        {
            lines.Add("No project-structure launch summary was supplied for this step.");
        }

        lines.Add($"Do not call workspace_read_file on artifacts/process-runs/{request.RunId}/project-structure.json or any other invented project-structure snapshot path unless that exact file is listed in Required upstream artifact slots or AgentFramework dependency artifact refs. Project-structure context is not materialized as a managed JSON file by default.");
        lines.Add("If a durable project-structure summary is useful for this step, write the relevant facts into the step's primary managed artifact instead of treating a missing snapshot file as a blocker.");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildProductMutationGuidance(ProcessStepBriefBuildRequest request)
    {
        var allowedOperations = request.Step.AllowedOperations;
        var targetScope = request.Step.OperationTargetScope;
        if (!StepAllowsProductMutation(allowedOperations, targetScope))
        {
            return "This step is not allowed to mutate product files. Use read, validation, subprocess, browser, or managed-artifact tools according to its allowed operations.";
        }

        var aliases = ResolveLaunchExternalTargetAliases(request.LaunchVariables);
        var aliasSummary = aliases.Count == 0
            ? "No grounded external-target alias was resolved; use a tool-provided grounded product alias or return Blocked with the concrete missing target evidence."
            : $"Use one of these grounded external-target aliases for structured product path arguments: {string.Join("; ", aliases)}.";
        return $"""
        This step is product-mutating. Before writing the final managed artifact or submitting Completed, produce a current-run successful product-target mutation receipt unless an earlier attempt for this same step already produced one and the product readback verifies the requested state.
        {aliasSummary}
        Product mutation receipts come from registered product-target tools when the request path or workingDirectory targets the grounded product alias. Writing only artifacts/process-runs/... is managed evidence, not product mutation.
        After mutating, read or stat the changed product files and cite the concrete product refs and mutation/validation receipt refs in the primary managed artifact. Do not claim changed product files until those files exist under the grounded product target.
        If a product-mutation tool is denied, missing, or cannot target the grounded product alias, return Blocked with that exact current-run tool receipt and manager action request instead of writing a status-only or false completion artifact.
        """;
    }

    private static bool StepAllowsProductMutation(
        IReadOnlyList<string> allowedOperations,
        string targetScope)
    {
        return allowedOperations.Contains(ProcessOperationContractNames.MutateProductTarget, StringComparer.OrdinalIgnoreCase) ||
               string.Equals(targetScope, ProcessOperationContractNames.ExternalProductTargetMutable, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(targetScope, ProcessOperationContractNames.ManagedOutputProduct, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsVisualTargetAssetSummary(string contextSummary)
        => contextSummary.Contains("Visual target assets:", StringComparison.OrdinalIgnoreCase) ||
           contextSummary.Contains("Visual target rule:", StringComparison.OrdinalIgnoreCase);

    private static bool IsTypedLaunchContractVariableName(string variableName)
        => variableName.EndsWith("Contract", StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!launchVariables.TryGetValue(key, out var candidate) ||
            string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        value = candidate.Trim();
        return true;
    }

    private static IReadOnlyList<string> ResolveLaunchExternalTargetAliases(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return launchVariables
            .Where(item => TrustedExternalTargetVariableNames.Contains(item.Key))
            .Select(item => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(item.Value))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item => item.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static readonly HashSet<string> TrustedExternalTargetVariableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExternalTargetAlias",
        "ExternalTargetRoot",
        "OutputFolder",
        "OutputRoot",
        "OutputRootAlias",
        "ProductRoot",
        "ProductRootAlias",
        "WorkspaceAlias"
    };

    private static string BuildOwnOutputBootstrapGuidance(ProcessStepBriefBuildRequest request)
    {
        if (request.ProducedSlots.Count == 0)
        {
            return "No produced artifact slots; no own-output bootstrap is required.";
        }

        var primaryWriteRef = BuildManagedStepArtifactPath(request.ManagedArtifactRoot, request.Step.Key);
        if (request.RequiredSlots.Count == 0)
        {
            return $"""
            This step has produced artifact slots and no required upstream artifact slots. It is an evidence producer. Do not return Blocked for missing upstream artifacts, insufficient evidence, missing prior logs, or absent screenshots before creating your own managed artifact. Your first evidence action must be workspace_write_file or workspace_append_file to the exact primary write ref below.
            Primary own-output write ref: {primaryWriteRef}
            Completion rule: after writing that artifact, return Completed with evidenceRefs containing the exact primary own-output write ref. If optional project context is missing, include assumptions and known gaps inside the artifact instead of blocking. Do not read or stat ProductRoot, OutputRoot, ExternalTargetRoot, or their external-target aliases looking for a same-named own-output packet before writing this managed artifact; own process outputs are generated under managed artifact refs, not discovered from the product target. Do not require build, test, runtime, screenshot, deployment, approval, or downstream handoff evidence that belongs to later steps before completing this producer step. Blocked is valid only when you cannot create the primary managed artifact or the step contract's own immediate inputs are contradictory.
            """;
        }

        return $"""
        This step has required upstream artifact slots and produced artifact slots. Read required upstream refs first, then create or update your own primary managed artifact before returning Completed. If at least one required upstream producer ref is readable and launch variables cover the remaining optional context, proceed with explicit assumptions instead of blocking on missing sibling files.
        Primary own-output write ref: {primaryWriteRef}
        """;
    }

    private static string BuildDependencyArtifactGuidance(ProcessStepBriefBuildRequest request)
    {
        var dependencyStepKeys = ResolveDependencyStepKeys(request.Step);
        if (dependencyStepKeys.Count == 0)
        {
            return "No direct dependency step artifact refs.";
        }

        var stepsByKey = request.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var dependencyStepKey in dependencyStepKeys)
        {
            stepsByKey.TryGetValue(dependencyStepKey, out var dependencyStep);
            var title = string.IsNullOrWhiteSpace(dependencyStep?.Title)
                ? dependencyStepKey
                : dependencyStep.Title.Trim();
            lines.Add($"""
            - Dependency step: {dependencyStepKey} - {title}
              Primary completed-step artifact ref: {BuildManagedStepArtifactPath(request.ManagedArtifactRoot, dependencyStepKey)}
              Dependency step artifact root: {BuildManagedStepArtifactRoot(request.ManagedArtifactRoot, dependencyStepKey)}
              Runtime rule: before listing, searching, or using project-structure fallback context, call workspace_stat_path or workspace_read_file on the exact primary ref above. If the primary ref is missing, inspect the listed dependency step artifact root before blocking.
            """);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<string> ResolveDependencyStepKeys(ProcessTemplateDefinitionStepDocument step)
    {
        var keys = new List<string>();
        foreach (var dependency in step.Dependencies)
        {
            if (!string.IsNullOrWhiteSpace(dependency.DependsOnStepKey))
            {
                keys.Add(dependency.DependsOnStepKey.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(step.DependsOnStepKey))
        {
            keys.Add(step.DependsOnStepKey.Trim());
        }

        return keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildManagedStepArtifactPath(string artifactRoot, string stepKey)
        => $"{artifactRoot}/steps/{SanitizeManagedArtifactPathSegment(stepKey)}.md";

    private static string BuildManagedStepArtifactRoot(string artifactRoot, string stepKey)
        => $"{artifactRoot}/{SanitizeManagedArtifactPathSegment(stepKey)}/";

    private static string SanitizeManagedArtifactPathSegment(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "step"
            : value.Trim();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.Length == 0 ? "step" : builder.ToString();
    }

    private static string BuildSubprocessGuidance(ProcessTemplateDefinitionStepDocument step)
    {
        var isSubprocessStep = string.Equals(step.StepKind, ProcessTemplateStepKinds.Subprocess, StringComparison.OrdinalIgnoreCase) ||
                               !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
        if (!isSubprocessStep)
        {
            return "No subprocess mapping.";
        }

        var hasSubprocessKey = !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);
        var subprocessKey = hasSubprocessKey
            ? step.SubprocessProcessKey.Trim()
            : "not mapped";
        var snapshotName = string.IsNullOrWhiteSpace(step.SubprocessDefinitionSnapshotName)
            ? "not supplied"
            : step.SubprocessDefinitionSnapshotName.Trim();
        if (step.SubprocessContract?.LaunchMode == ProcessSubprocessLaunchMode.RuntimeOwned)
        {
            return $"""
            - Child process definition key: {subprocessKey}
            - Child definition snapshot name: {snapshotName}
            - Launch ownership: process runtime owned
            - Completion rule: the process runtime launches, defers, and completes this parent step from typed child evidence. Do not call {SubprocessLaunchToolName} and do not hand-author a parent handoff from a generic child folder.
            - Accepted evidence rule: the runtime accepts only the typed child output rows listed in the subprocess parent bridge contract. Repaired accepted outputs and no-go outputs are machine-readable contract rows, not prose-only guidance.
            - No-go rule: if a completed child has a typed no-go output, the runtime propagates that concrete blocker to the parent step and does not retry blindly.
            - Parent artifact rule: the parent artifact is runtime-synthesized from accepted child evidence using the materialization mode in the typed contract.
            """;
        }

        var launchInstruction = !hasSubprocessKey
            ? "This step is marked as a subprocess but has no child process definition key. Return Blocked unless upstream evidence already supplies the missing child run."
            : $"Use {SubprocessLaunchToolName} with DefinitionKey \"{subprocessKey}\" when {ProcessOperationContractNames.ExecuteExternalAction} is allowed. Do not mark Completed until the child run receipt and required child evidence are available. If a stopped child run has Blocked status or escalation/no-go evidence, propagate that concrete blocker with child run and artifact refs instead of launching another child. Relaunch only when the stopped child has no blocker/escalation evidence, required evidence is recoverable by another child attempt, and launch is allowed. Return Blocked only for a concrete missing tool, input, policy, environment, or irrecoverable evidence problem.";

        return $"""
        - Child process definition key: {subprocessKey}
        - Child definition snapshot name: {snapshotName}
        - Governed launch tool: {SubprocessLaunchToolName}
        - Completion rule: {launchInstruction}
        - Mandatory-launch rule: for a mapped subprocess step with {ProcessOperationContractNames.ExecuteExternalAction}, do not write only a parent artifact and return Blocked because the child was not launched. If no active or stopped child run evidence is already available, your first non-read external action for this step must be {SubprocessLaunchToolName}.
        - Parent-tool boundary rule: direct child-work tools are not required in the parent subprocess step. If {SubprocessLaunchToolName} is available, launch the child even when direct implementation, scaffold, validation, browser, or runtime tools are absent from the parent toolset; those tools belong to the child run.
        - Live-run profile rule: leave LiveRunProfileKey empty unless the launch variables explicitly provide a valid process live-run profile key for this child definition. BranchName, RepositoryRoot, SessionId, parent DefinitionKey, and child DefinitionKey are not live-run profile keys.
        - Scope rule: use the parent step's assigned project node. Leave ParentProjectNodeId empty unless the parent launch context has no project node. Do not pass ProcessRunNodeId as ParentProjectNodeId.
        - Retry rule: repeated launch-tool calls for the same parent run, parent step, project node, and child definition return the existing child run instead of creating another child.
        - Stopped-child rule: a Completed, Failed, Cancelled, or Blocked child run is not an active wait. Inspect stopped-child evidence, then complete from valid evidence, propagate concrete blocker/escalation evidence, or relaunch only when missing evidence is recoverable by another child attempt and the child did not stop Blocked. Do not return Blocked only because a stopped child run exists.
        - Parent child-outcome rule: when the launch tool result has RunId and ParentDeferredOutcomeJson, call submit_process_step_outcome with that JSON exactly. For Stage Running this defers the parent step until the child run stops; for Stage Completed it completes the parent from child evidence; for stopped-child stages it propagates the stopped child status. Do not hand-author a different finalizer for the same child run.
        - Evidence rule: the launch tool result includes ChildManagedArtifactRoot, ChildStepsArtifactRoot, ChildLiveProcessesRoute, ExpectedChildEvidenceRefs, ParentDeferredOutcomeInstruction, and ParentDeferredOutcomeJson. Treat artifacts under ChildManagedArtifactRoot as the child evidence bundle; do not require child evidence to be copied into the parent run root. ExpectedChildEvidenceRefs are preferred lookup candidates after the child run is stopped, not an all-or-nothing checklist while it is still active; if one expected ref is missing after the child stops, inspect sibling files under ChildManagedArtifactRoot and child step directories before blocking.
        """;
    }
}

