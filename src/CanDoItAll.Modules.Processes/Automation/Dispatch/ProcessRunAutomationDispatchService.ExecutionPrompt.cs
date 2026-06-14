using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string BuildExecutionPrompt(DispatchCandidate candidate)
    {
        return BuildExecutionPromptCore(candidate, null, null, null);
    }

    private static string BuildExecutionPromptCore(
        DispatchCandidate candidate,
        string? recoveryDirective,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
    {
        var workBrief = candidate.WorkBrief;
        var browserProofGroundingText = string.Join(
            ' ',
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary);
        var requiresConcreteBrowserProof = RequiresConcreteBrowserProof(candidate, browserProofGroundingText);
        var requiresConcreteProductProof = RequiresConcreteImplementationProof(candidate) ||
            RequiresConcreteImplementationReview(candidate) ||
            requiresConcreteBrowserProof;
        ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out var projectStructureContext);
        var projectStructureProjectId = projectStructureContext?.ProjectId ?? candidate.Run.ProjectId;
        var hasProjectStructureExecutionContext = projectStructureContext is not null || !string.IsNullOrWhiteSpace(projectStructureGroundingSummary);
        var groundedExternalTarget = ProcessExternalTargetGroundingService.ResolveProjectStructureGroundingTarget(projectStructureGroundingSummary);
        var hasGroundedExternalTarget = groundedExternalTarget.HasTarget;
        var groundedExternalAbsolutePath = groundedExternalTarget.AbsolutePath;
        var groundedExternalMappedAlias = groundedExternalTarget.MappedAlias;
        var hasGroundedExternalScaffoldTarget = groundedExternalTarget.ScaffoldTarget is not null;
        var groundedExternalParentAlias = groundedExternalTarget.ScaffoldTarget?.ParentAlias ?? string.Empty;
        var groundedExternalLeafName = groundedExternalTarget.ScaffoldTarget?.LeafName ?? string.Empty;
        var operationContract = ResolveProcessStepOperationContract(candidate);
        var executionBoundary = ResolveProcessStepExecutionBoundary(candidate, operationContract);
        var effectiveCooperationMetadata = ResolveBoundaryAwareCooperationMetadata(candidate.CooperationMetadata, executionBoundary);
        var allowsExternalTargetMutation = AllowsExternalTargetMutation(candidate, executionBoundary, operationContract, projectStructureGroundingSummary);
        var allowsProjectStructureMutation = operationContract.AllowedOperations.Contains(ProcessStepOperation.ExecuteExternalAction);
        var currentRunManagedArtifactRoot = BuildCurrentRunManagedArtifactRoot(candidate);
        var currentRunManagedOutputRoot = BuildCurrentRunManagedOutputRoot(candidate);
        var usesGroundedExternalArtifactDestination = hasGroundedExternalTarget &&
            !requiresConcreteProductProof &&
            LooksLikeExternalArtifactDestination(candidate, projectStructureGroundingSummary);
        var requiredArtifactDefaultRoot = usesGroundedExternalArtifactDestination
            ? groundedExternalMappedAlias
            : currentRunManagedArtifactRoot;
        var softwareDeliveryGuidance = SoftwareDeliveryGuidancePolicy.CreateExecutionGuidance(
            new SoftwareDeliveryExecutionGuidanceRequest(
                CreateSoftwareDeliveryImplementationContractSnapshot(
                    candidate,
                    browserProofGroundingText,
                    requiresConcreteBrowserProof),
                hasProjectStructureExecutionContext,
                hasGroundedExternalTarget,
                groundedExternalAbsolutePath,
                groundedExternalMappedAlias,
                usesGroundedExternalArtifactDestination,
                allowsExternalTargetMutation,
                hasGroundedExternalScaffoldTarget,
                groundedExternalParentAlias,
                groundedExternalLeafName,
                !requiresConcreteBrowserProof && ContainsExplicitBrowserSurfaceSignal(browserProofGroundingText),
                currentRunManagedArtifactRoot,
                currentRunManagedOutputRoot));
        var summarizedTriggerReason = ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason);
        var builder = new StringBuilder();
        builder.AppendLine("You are executing a CanDoItAll process step.");
        builder.AppendLine();
        builder.AppendLine($"Process: {candidate.Definition.Name}");
        builder.AppendLine($"Run: {candidate.Run.Name}");
        builder.AppendLine($"Step: {candidate.StepRun.Title}");
        builder.AppendLine($"Executor: {candidate.StepRun.CurrentExecutorName}");
        builder.AppendLine();
        builder.AppendLine("Process cooperation plan:");
        builder.AppendLine($"- Mode: {effectiveCooperationMetadata.CooperationMode}");
        builder.AppendLine($"- Workspace tool profile: {AgentWorkspaceToolAccessProfiles.GetProfileKey(effectiveCooperationMetadata.WorkspaceToolProfile)}");
        builder.AppendLine($"- Basis: {effectiveCooperationMetadata.Summary}");
        builder.AppendLine("- Use upstream artifacts, MAF handoff participants, or A2A tools only when they are explicitly provided by this run or attached to the selected agent. Do not invent hidden background collaboration.");
        builder.AppendLine();
        builder.AppendLine("Current-run managed artifact root:");
        builder.AppendLine($"- `{currentRunManagedArtifactRoot}`");
        builder.AppendLine("- Use this root for discretionary evidence, notes, logs, and required text artifacts that do not have an explicit governed path or grounded external artifact destination.");
        builder.AppendLine();
        builder.AppendLine("Current-run managed output root:");
        builder.AppendLine($"- `{currentRunManagedOutputRoot}`");
        builder.AppendLine("- Use this root only for concrete generated deliverables when no grounded external product root is provided by the current run.");
        builder.AppendLine();
        builder.AppendLine("Run objective:");
        builder.AppendLine(string.IsNullOrWhiteSpace(summarizedTriggerReason)
            ? string.IsNullOrWhiteSpace(candidate.Definition.Summary)
                ? candidate.Definition.ValueStatement
                : candidate.Definition.Summary
            : summarizedTriggerReason);
        builder.AppendLine();
        if (hasProjectStructureExecutionContext)
        {
            builder.AppendLine("Project structure context:");
            if (projectStructureContext is not null)
            {
                builder.AppendLine(ProcessProjectStructureContextFormatter.BuildPromptSummary(projectStructureContext));
            }
            else if (projectStructureProjectId.HasValue)
            {
                builder.AppendLine($"- Project id: {projectStructureProjectId.Value:D}");
                builder.AppendLine("- Selected process node: not serialized on launch; use the live project-structure grounding below as the current run target.");
                builder.AppendLine("- Target work node: inferred from current project structure, not from prior runs or sibling folders.");
            }
            else
            {
                builder.AppendLine("- Selected process node: not serialized on launch; use the live project-structure grounding below as the current run target.");
            }

            builder.AppendLine();
            builder.AppendLine("Project structure execution rules:");
            var projectStructureReadTarget = projectStructureProjectId.HasValue
                ? $"project `{projectStructureProjectId.Value:D}`"
                : "the current project";
            builder.AppendLine(string.IsNullOrWhiteSpace(projectStructureGroundingSummary)
                ? $"- Use `project_structure_read` early in this step for {projectStructureReadTarget} so you inspect the live project graph instead of relying only on the selected node label."
                : $"- The dispatcher already fetched a live project-structure snapshot for this run and included it below. Treat that grounding as a starting point, not a substitute for tool execution. You must still call `project_structure_read` early in this step for {projectStructureReadTarget} before you conclude.");
            builder.AppendLine("- Do not assume the selected task node contains every requirement. Carry forward only concrete stack choices, output directories, UI expectations, and acceptance notes explicitly attached to the selected work branch, ancestor nodes, included project-level planning context, upstream artifacts, or dependency links grounded for this run.");
            builder.AppendLine("- Explicit project-structure requirements are source-of-truth constraints. Do not weaken them into optional items, exclusions, follow-up work, assumptions, or non-acceptance criteria unless the same project-structure source says they are optional or deferred.");
            builder.AppendLine("- If the project structure names a concrete output directory outside the managed workspace, do not silently relocate the deliverable. Use a controlled local execution path when necessary, and record the exact external target in the artifacts you write.");
            builder.AppendLine("- Paths mentioned inside no-go, prohibited, out-of-scope, or exclusion language are constraints, not grounded targets. Do not stat, list, read, write, launch, or validate those paths unless the same current-run artifact also names them as the accepted product root.");
            builder.AppendLine("- Do not cite a file, path, tool result, example, or source artifact as evidence unless it was grounded by the current-run project structure, inspected by a current execution tool call, provided by an upstream artifact, or loaded from an attached skill/template resource. If source inspection was not performed in this run, say that instead of naming remembered files.");
            builder.AppendLine("- Do not include a `provided context`, `source-document context`, `ignored context`, or similar note that lists out-of-scope paths. If a path is unrelated to the current grounded product root, omit it from final artifacts entirely.");
            builder.AppendLine($"- In governed process steps, every `workspace_pwsh_run_script` or `workspace_python_run_file` call must include a `{GovernedScriptSideEffectManifest.ArgumentName}` string containing a `GovernedScriptSideEffectManifest` JSON document. Use `NoMutation` for read-only scripts, `ManagedProcessArtifacts` with declared current-run artifact write paths for evidence writers, `ExternalArtifactDestination` for governed external artifact destinations, and `ProductMutation` only when this step explicitly permits product mutation.");
            AppendPromptLines(builder, softwareDeliveryGuidance.ProjectStructureExecutionRuleLines);
            builder.AppendLine("- Treat missing project-structure inspection as incomplete work for this step.");
            if (allowsProjectStructureMutation)
            {
                builder.AppendLine("- Project-structure mutation tools are allowed only for the exact writeback this step requires. Create or update process/result nodes only when this step asks for durable project-structure receipts.");
            }
            else
            {
                builder.AppendLine("- This step does not allow `ExecuteExternalAction`. Do not call project-structure mutation tools such as `project_structure_node_create`, `project_structure_node_update`, `project_structure_asset_create`, or `project_structure_asset_create_revision`. If later writeback is needed, write the planned node data into the required managed artifact and leave project-structure mutation to a later writeback step.");
            }

            builder.AppendLine("- If project_structure_read reveals an exact external output directory for the selected work node, keep that directory as the authoritative product boundary for this run. Create, bootstrap, or implement there only when the current step contract explicitly requires concrete delivery work.");
            builder.AppendLine("- A greenfield external product root can be absent during scope, architecture, research, or planning steps. If the current step does not require concrete implementation or validation, record the intended root as the boundary and leave creation, source listing, build, run, or test proof to the implementation and validation steps.");
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(projectStructureGroundingSummary))
        {
            builder.AppendLine("Live project structure grounding:");
            builder.AppendLine(projectStructureGroundingSummary.Trim());
            builder.AppendLine();
        }

        if (softwareDeliveryGuidance.SetupBoundaryLines.Count > 0)
        {
            AppendPromptLines(builder, softwareDeliveryGuidance.SetupBoundaryLines);
            builder.AppendLine();
        }

        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            builder.AppendLine(".NET scaffold tool rules:");
            builder.AppendLine($"- Use `{ToolContractCatalog.WorkspaceDotNetNew}` for solution, app project, and test project scaffold creation.");
            builder.AppendLine($"- Before any prose conclusion or `{AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName}` call, follow one current-run branch: either call `{ToolContractCatalog.WorkspaceDotNetNew}` for the missing requested scaffold, or prove the existing scaffold with `workspace_stat_path` and `workspace_read_file` receipts for the concrete `.slnx`, `.sln`, or `.csproj` files.");
            builder.AppendLine($"- If the product root directory exists but the requested solution or project files are absent or unverified, the verified-existing-scaffold exception is unavailable. Call `{ToolContractCatalog.WorkspaceDotNetNew}` instead of writing only a scaffold contract, plan, or summary artifact.");
            builder.AppendLine("- The upstream scaffold contract artifact is input only. It does not satisfy the required solution/test change-set artifact, and it does not replace scaffold creation or current-run existing-scaffold proof.");
            builder.AppendLine($"- `{ToolContractCatalog.WorkspaceDotNetNew}` is a creation tool, not a receipt-only command. If the target already contains the requested `.slnx`, `.sln`, or `.csproj` files and `{ToolContractCatalog.WorkspaceDotNetNew}` is denied because re-scaffolding would overwrite files or run inside an existing project, inspect the existing scaffold with `workspace_stat_path` and `workspace_read_file`, verify target-framework and package compatibility for the requested stack, write the required current-run managed change-set artifact, and return `Completed` only when the scaffold satisfies the step contract.");
            builder.AppendLine($"- Treat stale scaffold metadata as invalid. For example, a project targeting `net8.0` with .NET `10.x` ASP.NET or Blazor package references does not satisfy a .NET 10 scaffold contract; repair the project file with `{ToolContractCatalog.WorkspacePowerShellRunScript}` and `{GovernedScriptSideEffectManifest.ArgumentName}` of `ProductMutation`, reread it, then write the change-set artifact.");
            builder.AppendLine($"- Use `{ToolContractCatalog.WorkspacePowerShellRunScript}` with a `{GovernedScriptSideEffectManifest.ArgumentName}` of `ProductMutation` for surgical dotnet CLI operations or project-file repairs that scaffold tools cannot express.");
            builder.AppendLine($"- If the existing scaffold is missing or invalid, use `{ToolContractCatalog.WorkspaceDotNetNew}` or `{ToolContractCatalog.WorkspacePowerShellRunScript}` with `{GovernedScriptSideEffectManifest.ArgumentName}` of `ProductMutation` to create or repair it. Return `Blocked` or `Failed` only when no safe scaffold or repair path exists.");
            builder.AppendLine($"- Do not call `{ToolContractCatalog.WorkspaceWriteFile}`, `{ToolContractCatalog.WorkspaceAppendFile}`, `{ToolContractCatalog.WorkspaceCopyPath}`, `{ToolContractCatalog.WorkspaceMovePath}`, or `{ToolContractCatalog.WorkspaceDeletePath}` against product files under `external-target/` during scaffold steps; those direct file mutation tools are only for current-run artifacts here.");
            builder.AppendLine($"- If `{ToolContractCatalog.WorkspaceWriteFile}` is denied for an `external-target/` product path, do not retry it with a temporary, probe, or alternate product path. Use the allowed scaffold or script tool, or return `Failed` with the exact policy reason.");
            builder.AppendLine();
        }

        builder.AppendLine("Work brief:");
        builder.AppendLine(workBrief?.WorkBriefText ?? "No work brief was captured for this step.");
        builder.AppendLine();
        builder.AppendLine("Handoff summary:");
        builder.AppendLine(workBrief?.HandoffSummary ?? "None");
        builder.AppendLine();
        builder.AppendLine("Expected outcome:");
        builder.AppendLine(workBrief?.ExpectedOutcome ?? "Complete the step and produce durable evidence artifacts.");
        builder.AppendLine();
        builder.AppendLine("Assumption-forward execution rule:");
        builder.AppendLine("- When the run objective, project structure, work brief, upstream artifacts, or tool outputs identify a concrete deliverable and target boundary, proceed with bounded assumptions instead of stopping for optional preferences.");
        builder.AppendLine("- For intake, scope, planning, architecture, and implementation-preparation steps, missing preferences such as target date, stakeholder labels, branding, persistence choice, hosting choice, document format details, sample data, or rollout calendar are non-blocking unless the step contract makes that item mandatory for safety, legality, credentials, access, or an irreversible external action.");
        builder.AppendLine("- Missing preferences are different from stated requirements. If project structure, upstream artifacts, or the run objective explicitly require persistence, hosting, controls, output location, validation, platform behavior, or another acceptance constraint, preserve it as required in the current artifact.");
        builder.AppendLine("- Record assumptions, exclusions, unresolved follow-up questions, and validation hooks in the required artifact. Use `Blocked` only when the core deliverable, writable target, mandatory upstream artifact, required authority, required credentials, or safe execution boundary is genuinely missing and cannot be inferred or deferred to a modeled review or repair step.");
        builder.AppendLine("- Do not return `Blocked` only because implementation details remain for a later implementation, QA, security, release, or repair step. Complete the current governed disposition when the current step can produce its required artifact or decision.");
        builder.AppendLine("- If this step itself is an escalation, no-go, scope-reset, replan, or unresolved-repair decision step, unresolved product defects are the decision payload. Write the required escalation/no-go record and return `Completed`; do not return `Blocked` merely because the product is not release-ready.");
        builder.AppendLine();
        builder.AppendLine("Evidence expectation:");
        builder.AppendLine(workBrief?.EvidenceExpectationSummary ?? "Save any relevant evidence artifacts inside the workspace.");
        builder.AppendLine();
        builder.AppendLine("Required output artifacts:");
        builder.AppendLine(BuildExpectedArtifactSummary(candidate, projectStructureGroundingSummary));
        builder.AppendLine();
        var requiredToolNames = ResolveRequiredToolNamesCore(candidate, browserProofGroundingText);
        if (requiredToolNames.Count > 0)
        {
            var hasVerifiedExistingDotNetScaffoldException =
                requiredToolNames.Contains(ToolContractCatalog.WorkspaceDotNetNew, StringComparer.Ordinal) &&
                IsDotNetSolutionSetupScaffoldMutationStep(candidate);
            builder.AppendLine("Required tool execution checklist:");
            builder.AppendLine($"- Completion of this step is gated on successful current-run tool receipts for: {string.Join(", ", requiredToolNames.Select(toolName => $"`{toolName}`"))}.");
            if (hasVerifiedExistingDotNetScaffoldException)
            {
                builder.AppendLine($"- Verified existing scaffold exception: do not return `Blocked` solely because there is no successful current-run `{ToolContractCatalog.WorkspaceDotNetNew}` receipt when the target already contains the requested solution or project files, `{ToolContractCatalog.WorkspaceDotNetNew}` was denied because the scaffold already exists, `workspace_stat_path` and `workspace_read_file` verified those files, and the current attempt wrote the required managed change-set artifact.");
                builder.AppendLine("- The verified existing scaffold exception applies only when the inspected scaffold is semantically compatible with the requested .NET stack. Do not use it for stale target-framework/package metadata such as `net8.0` with .NET `10.x` Blazor packages.");
                builder.AppendLine("- These are not optional hints. Except for the scoped verified-existing-scaffold rule above, if a required tool is unavailable, denied, or cannot be run against the concrete target, return Blocked or Failed with the exact tool and reason.");
            }
            else
            {
                builder.AppendLine("- These are not optional hints. If a required tool is unavailable, denied, or cannot be run against the concrete target, return Blocked or Failed with the exact tool and reason.");
            }

            if (RequiresConcreteImplementationProof(candidate))
            {
                builder.AppendLine("- For implementation steps, `workspace_write_file` is required for durable handoff evidence after concrete product mutation and validation. It does not replace creating, scaffolding, editing, reading, and validating real product files.");
            }

            builder.AppendLine();
        }

        if (softwareDeliveryGuidance.BrowserProofBoundaryLines.Count > 0)
        {
            AppendPromptLines(builder, softwareDeliveryGuidance.BrowserProofBoundaryLines);
            builder.AppendLine();
        }

        if (softwareDeliveryGuidance.MandatoryBrowserProofPlanLines.Count > 0)
        {
            AppendPromptLines(builder, softwareDeliveryGuidance.MandatoryBrowserProofPlanLines);
            builder.AppendLine();
        }

        AppendRequiredArtifactResponseContract(builder, candidate.ExpectedArtifacts, requiredArtifactDefaultRoot);
        builder.AppendLine("Upstream artifacts:");
        builder.AppendLine(BuildArtifactInputSummary(candidate.ArtifactInputs));
        builder.AppendLine();
        builder.AppendLine("Context preservation rules:");
        builder.AppendLine("- Treat the run objective, current-run managed artifact root, required output artifact paths, upstream artifact paths, prefetched governed artifact grounding, and successful validation receipts as required context for this run.");
        builder.AppendLine("- Do not replace concrete artifact paths, tool receipts, or direct file inspections with memory summaries during implementation, handoff, QA, security, repair, or release decisions.");
        builder.AppendLine("- If an upstream artifact excerpt is truncated or insufficient, use workspace_read_file with a larger maxCharacters value on the concrete artifact path before deciding.");
        builder.AppendLine();
        var missingUpstreamArtifactInputSummary = ResolveMissingUpstreamArtifactInputSummary(candidate);
        if (!string.IsNullOrWhiteSpace(missingUpstreamArtifactInputSummary))
        {
            builder.AppendLine("Upstream artifact gate:");
            builder.AppendLine(missingUpstreamArtifactInputSummary);
            builder.AppendLine("- Do not fabricate an upstream artifact in this step and do not spend validation/build attempts trying to compensate for it. Return `Blocked` and name the upstream step and artifact that must be rerun or supplied.");
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(artifactInspectionGroundingSummary))
        {
            builder.AppendLine("Prefetched governed artifact grounding:");
            builder.AppendLine(artifactInspectionGroundingSummary.Trim());
            builder.AppendLine();
        }

        if (candidate.BranchOutcomes.Count > 0)
        {
            builder.AppendLine("Available branch outcomes:");
            builder.AppendLine(BuildBranchOutcomePromptSummary(candidate.BranchOutcomes));
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(recoveryDirective))
        {
            builder.AppendLine("Recovery directive:");
            builder.AppendLine(recoveryDirective.Trim());
            builder.AppendLine();
        }

        var governedInspectionPaths = ResolveGovernedInspectionPaths(candidate.ExpectedArtifacts);
        var artifactInputInspectionPaths = ResolveArtifactInputInspectionPaths(candidate.ArtifactInputs);

        if (RequiresGovernedInspection(candidate.StepRun) || RequiresDurableTextArtifactWrite(candidate))
        {
            builder.AppendLine("Governed evidence rules:");
            if (RequiresGovernedInspection(candidate.StepRun))
            {
                if (!string.IsNullOrWhiteSpace(artifactInspectionGroundingSummary))
                {
                    builder.AppendLine("- The dispatcher already inspected upstream governed artifact files and included verified paths and excerpts below as orientation. If upstream artifact input paths are listed in this prompt, direct workspace_stat_path and workspace_read_file inspection of those paths is still required before a review step approves or rejects the handoff.");
                }

                builder.AppendLine("- Use workspace_stat_path and workspace_read_file on the concrete workspace files or durable artifacts you cite as evidence. Do not rely only on summaries, RAG snippets, or prior notes.");
                builder.AppendLine("- For greenfield scope, architecture, research, or planning steps, a planned product root may not exist yet. Satisfy inspection through current-run project-structure reads and upstream durable artifacts; do not stat, list, read, or fail on an absent product root unless this step explicitly requires implementation, validation, or review of existing product files.");
                builder.AppendLine("- EvidenceRefs must name only current-run tool-backed paths, durable artifacts, or attached skill/template resources. Do not invent or carry forward source paths from memory.");
                if (governedInspectionPaths.StatPaths.Count > 0)
                {
                    builder.AppendLine($"- Before you conclude, use workspace_stat_path on these governed output paths after they exist: {FormatPromptPathList(governedInspectionPaths.StatPaths)}.");
                }

                if (governedInspectionPaths.ReadPaths.Count > 0)
                {
                    builder.AppendLine($"- Before you conclude, use workspace_read_file on these text-based governed artifacts after they exist: {FormatPromptPathList(governedInspectionPaths.ReadPaths)}.");
                }

                if (governedInspectionPaths.StatPaths.Count > 0 && governedInspectionPaths.ReadPaths.Count == 0)
                {
                    builder.AppendLine("- If the governed artifacts are binary-only, stat the binary files and read the nearest durable markdown, log, JSON, YAML, or text artifact that explains or imports them before you conclude.");
                }

                if (artifactInputInspectionPaths.StatPaths.Count > 0)
                {
                    builder.AppendLine($"- Use workspace_stat_path on these upstream durable artifact paths while you review the inherited evidence: {FormatPromptPathList(artifactInputInspectionPaths.StatPaths)}.");
                }

                if (artifactInputInspectionPaths.ReadPaths.Count > 0)
                {
                    builder.AppendLine($"- Use workspace_read_file on these upstream durable text artifacts before you conclude: {FormatPromptPathList(artifactInputInspectionPaths.ReadPaths)}.");
                }
            }

            if (RequiresDurableTextArtifactWrite(candidate))
            {
                if (usesGroundedExternalArtifactDestination)
                {
                    builder.AppendLine($"- Use workspace_write_file to write required markdown or text artifacts under `{requiredArtifactDefaultRoot}` when no narrower path is listed. If the artifact expectation also lists a managed path, create or reference a managed handoff copy so process artifact history still has the deliverable.");
                }
                else
                {
                    builder.AppendLine("- Use workspace_write_file to write required markdown or text artifacts at their governed managed paths instead of relying on response projection. If the artifact expectation does not list an exact path, use the suggested current-run path under the current-run managed artifact root.");
                }

                builder.AppendLine($"- If a pathless required artifact can be produced from current context, create it under `{requiredArtifactDefaultRoot}` with a filename derived from the artifact title instead of writing it to source, test, or other product paths.");
                builder.AppendLine("- Do not write required evidence artifacts to `src/...`, `tests/...`, or other product/source paths unless the required artifact contract lists that exact grounded artifact path. A denied product/source write for required evidence should be retried as a managed artifact, not converted into `Blocked`.");
            }

            builder.AppendLine();
        }

        builder.AppendLine("Execution rules:");
        builder.AppendLine("- Complete the actual work described in the work brief and expected outcome before writing summary artifacts.");
        builder.AppendLine("- Treat the run objective, work brief, required artifacts, grounded project-structure nodes, upstream artifacts, and current-run tool outputs as the scope boundary. Do not add optional features, extra documents, new workflows, new agent roles, visual flourishes, or technology changes only because they seem useful.");
        builder.AppendLine("- Escalate with `Blocked` or `Failed` when the requested result cannot be built inside that boundary because of missing architecture, unavailable credentials or access, tool policy denial, paid or licensed dependency requirements, destructive migration risk, security constraints, or unexpectedly large development outside the step contract. Name the exact blocker and the smallest decision or input needed.");
        builder.AppendLine("- Required output artifacts are evidence of completed work. They do not replace code changes, runnable outputs, tests, screenshots, or other concrete deliverables.");
        builder.AppendLine("- Do not execute helper scripts, app launches, browser proof, release rollout, or other side actions unless the current step contract or required artifacts explicitly call for them.");
            builder.AppendLine($"- Use `{requiredArtifactDefaultRoot}` for required text artifacts that do not have an explicit governed path. Use `{currentRunManagedArtifactRoot}` for current-run managed evidence, drafts, and logs.");
            builder.AppendLine($"- Use `{currentRunManagedOutputRoot}` for generated product files only when this run has no grounded external product root. Keep generated source, tests, scripts, and project files under that run-specific output root.");
            builder.AppendLine("- Paths under `artifacts/`, `output/`, `integration-map/`, and `data/` are managed workspace aliases. Do not write shallow shared scope files directly under `artifacts/scopes/<scope>/<id>/`, `output/scopes/<scope>/<id>/`, `integration-map/scopes/<scope>/<id>/`, or `data/scopes/<scope>/<id>/`; concurrent runs can overwrite those files. Use the current-run root unless a required artifact input or output gives an exact deeper managed path.");
        builder.AppendLine("- Treat run-level paths and planned solution targets as context unless the current step contract explicitly tells you to create, inspect, build, test, launch, or review them. Only then must that concrete output exist before you conclude.");
        builder.AppendLine("- For scope, architecture, research, planning, and classification steps, lack of existing product files is not a blocker when project_structure_read and upstream durable artifacts provide enough context to record assumptions, boundaries, decisions, and downstream validation hooks.");
        builder.AppendLine("- If the current step contract describes greenfield implementation or gives you a bootstrap or init script, missing solution or project files are expected pre-bootstrap state, not a blocker. Run the bootstrap or init step first, then inspect the scaffolded files and continue.");
        builder.AppendLine("- Do not claim that planned scaffold targets are missing deliverables when the current step contract explicitly tells you to create, bootstrap, or scaffold them in this step.");
            builder.AppendLine("- If a required build, test, launch, browser check, or artifact import fails, inspect the real diagnostics, fix the underlying problem, and rerun the same required validation before you conclude. Do not treat the first failed validation as acceptable end-state evidence.");
            builder.AppendLine("- After a failed validation tool call, the next tool call must inspect the failing diagnostics or mutate files that directly address the failure. Repeating the same failed build/test/run command without an intervening cause-directed change is no-progress behavior.");
            builder.AppendLine("- Do not make validation pass by writing fake package, framework, runtime, browser, or test-tool shims. Fix real dependencies and project references, or return Blocked with the exact missing dependency or environment issue.");
            builder.AppendLine("- Do not stop after inspection, reconnaissance, bootstrap confirmation, or a next-steps summary if required tools, concrete deliverables required by this step, or required artifacts are still missing.");
        if (RequiresDotNetValidationRepairGuidance(candidate))
        {
            builder.AppendLine(".NET validation repair rules:");
            builder.AppendLine($"- Use `{ToolContractCatalog.WorkspacePowerShellRunScript}` as an executed validation or repair harness for this .NET validation step. Writing a `.ps1` file without running it does not satisfy the required tool receipt or the validation contract.");
            builder.AppendLine($"- When calling `{ToolContractCatalog.WorkspacePowerShellRunScript}`, pass `{GovernedScriptSideEffectManifest.ArgumentName}` as serialized JSON text for a `GovernedScriptSideEffectManifest`, not as a nested object. For validation-only scripts that write logs under current-run managed artifacts, the string content should be `{{ \"version\": 1, \"mode\": \"ManagedProcessArtifacts\", \"declaredWritePaths\": [ \"artifacts/process-runs/<run-id>/...\" ] }}`. For scoped project-file repairs, use string content `{{ \"version\": 1, \"mode\": \"ProductMutation\", \"declaredReadPaths\": [ \"external-target/...\" ], \"declaredWritePaths\": [ \"external-target/...\" ] }}`.");
            builder.AppendLine($"- Do not use `{GovernedScriptSideEffectManifest.ArgumentName}` keys such as `kind` or `writes`; they are ignored by the governed script contract. Set the script `path` to the workspace-root-relative `.ps1` path, and either omit `workingDirectory` or set it to the script directory so relative log paths resolve predictably.");
            builder.AppendLine($"- For this repair-guided validation boundary, put restore, build, and test discovery inside the `{ToolContractCatalog.WorkspacePowerShellRunScript}` harness. Do not call `{ToolContractCatalog.WorkspaceDotNetRestore}`, `{ToolContractCatalog.WorkspaceDotNetBuild}`, or `{ToolContractCatalog.WorkspaceDotNetTest}` directly after the harness; direct MSBuild-client timeout receipts are diagnostic inputs, not the authoritative validation path.");
            builder.AppendLine("- Every PowerShell validation harness that runs `dotnet restore`, `dotnet build`, or `dotnet test` must execute `dotnet build-server shutdown` before validation and must pass `--disable-build-servers` on every restore, build, and test command. Do not omit these flags on first validation attempts.");
            builder.AppendLine("- PowerShell `dotnet` commands must use real host filesystem paths such as `C:\\...` or paths relative to the host working directory. Workspace aliases such as `external-target/...` are tool aliases, not valid PowerShell or MSBuild project paths.");
            builder.AppendLine("- When writing the PowerShell harness, keep process exit-code capture separate from logging. Do not assign the output of a helper function that writes to the pipeline into an exit-code variable; write logs with `Add-Content` or `Write-Host`, capture `$LASTEXITCODE` immediately after each `dotnet` invocation, and exit non-zero only when the final validation result is actually failed.");
            builder.AppendLine($"- If `{ToolContractCatalog.WorkspaceDotNetRestore}`, `{ToolContractCatalog.WorkspaceDotNetBuild}`, or `{ToolContractCatalog.WorkspaceDotNetTest}` fails before project diagnostics with an MSBuild client, server, or named-pipe `System.TimeoutException`, run a governed `{ToolContractCatalog.WorkspacePowerShellRunScript}` validation script that executes `dotnet build-server shutdown`, then rerun the same restore, build, or test command with `--disable-build-servers`.");
            builder.AppendLine($"- If a direct `{ToolContractCatalog.WorkspaceDotNetRestore}`, `{ToolContractCatalog.WorkspaceDotNetBuild}`, or `{ToolContractCatalog.WorkspaceDotNetTest}` receipt fails after a validation script has run, the timeout recovery still requires a fresh `{ToolContractCatalog.WorkspacePowerShellRunScript}` receipt after that failure. Do not select `Error`, `Blocked`, or `Failed` from stale timeout receipts.");
            builder.AppendLine("- Treat `MSB5021`, `build was canceled`, and `Terminating the task executable \"csc\"` as validation-infrastructure cancellation signals, not unresolved product warnings. Rerun the harness from a clean build-server state and do not select `Error`, `Blocked`, or `Failed` only because a canceled build emitted `MSB5021`.");
            builder.AppendLine($"- After a `--disable-build-servers` rerun exposes concrete diagnostics such as `NU1202`, stop rewriting or rerunning the build-server retry script. The next tool call must inspect or repair the affected project file with `{ToolContractCatalog.WorkspacePowerShellRunScript}` using `{GovernedScriptSideEffectManifest.ArgumentName}` of `ProductMutation`.");
            builder.AppendLine($"- If the rerun exposes project, package, or framework diagnostics such as `NU1202`, `NETSDK`, `CS`, or target-framework/package incompatibility, treat that as a stale scaffold repair. Inspect the affected `.csproj` or props files, repair them with `{ToolContractCatalog.WorkspacePowerShellRunScript}` using `{GovernedScriptSideEffectManifest.ArgumentName}` of `ProductMutation`, reread the changed files, and rerun restore, build, and test discovery before selecting a branch outcome.");
            builder.AppendLine("- Do not select `Error`, `Blocked`, or `Failed` while a safe project-file repair can address stale existing scaffold metadata such as a target-framework/package-version mismatch.");
        }

        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("- Because this is an implementation step, create the real deliverable now. A markdown change set alone is not completed implementation.");
            builder.AppendLine("- For application, API, service, UI, host, or startup work, required implementation proof must include real source, project, configuration, or runtime files. Markdown artifacts, checklists, notes, and summaries are evidence only.");
            builder.AppendLine("- Do not return Completed only because existing implementation-summary, change-set, migration, rollout, checklist, or notes files are already present. Existing markdown evidence does not satisfy this step until the current attempt has created or repaired the concrete product files and validated them.");
            builder.AppendLine("- Follow the current step contract, assigned agent instructions, available skills, and upstream artifacts to choose the correct project shape, folder structure, tools, and validation path.");
            builder.AppendLine("- Inspect existing files before creating or replacing scaffolds. Repair an existing deliverable in place when that is safer than recreating it.");
            builder.AppendLine("- Do not delete the grounded product root, source directories, UI directories, or test directories to make scaffolding or validation easier. Delete only explicit generated scratch files when the diagnostic requires that exact file removal.");
            builder.AppendLine("- Do not write implementation change-set or rollout artifacts until after concrete source, content, configuration, analysis, or deliverable mutations and required validation in the same attempt.");
            builder.AppendLine("- Concrete feature and constraint nodes from the live project structure are required scope for this implementation step. Treat them as mandatory deliverables now, not as later backlog or rollout notes.");
            builder.AppendLine("- Do not defer grounded features, UI behavior, acceptance notes, or output constraints into `future steps`, follow-up work, or QA-only cleanup while still returning `Completed`.");
            builder.AppendLine("- Before you conclude this implementation step, use available workspace tools to inspect the concrete files, records, or artifacts you created or changed.");
            builder.AppendLine("- Required proof must happen after the last mutation in the same attempt. Previous attempt receipts do not prove the current mutated output.");
            builder.AppendLine("- After the final concrete product mutation, read at least one representative changed source, project, document, workbook, deck, or deliverable file before writing final evidence artifacts or submitting the outcome. If you mutate another product file after that read, repeat the read and rerun the required validation before concluding.");
            builder.AppendLine("- If you repair product files after a failed build, test, run, browser, lint, or validation tool, rerun that same validation against the same concrete target after the repair. Returning Blocked only because you did not rerun validation after your own repair is an incomplete attempt.");
            builder.AppendLine("- If you start from a template, replace placeholder output with the requested product, document, analysis, workflow, or other concrete deliverable before you conclude.");
            builder.AppendLine("- If the requested deliverable is interactive, such as an application, game, workflow UI, editor, or keyboard/control-driven screen, a static layout preview, screenshot, or mockup is not an implementation. Implement representative input-driven state changes before you conclude.");
            builder.AppendLine("- Do not write implementation artifacts that say the requested behavior, analysis, artifacts, tests, rollout preparation, or operational changes will happen in a later step while this implementation step still returns `Completed`.");
            if (artifactInputInspectionPaths.StatPaths.Count > 0 || artifactInputInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine("- Before you implement against inherited requirements, architecture notes, research notes, analysis, or approvals, inspect the upstream durable artifacts directly instead of relying only on their summaries.");
                if (artifactInputInspectionPaths.StatPaths.Count > 0)
                {
                    builder.AppendLine($"- Use `workspace_stat_path` on these upstream durable artifact paths before you code against them: {FormatPromptPathList(artifactInputInspectionPaths.StatPaths)}.");
                }

                if (artifactInputInspectionPaths.ReadPaths.Count > 0)
                {
                    builder.AppendLine($"- Use `workspace_read_file` on these upstream durable text artifacts before you code or conclude: {FormatPromptPathList(artifactInputInspectionPaths.ReadPaths)}.");
                }
            }

            builder.AppendLine("- If the concrete deliverable does not exist yet, create the correct working structure now using the tools and folder conventions that fit this step, its assigned agent, and its domain.");
            builder.AppendLine("- If the inherited requirements describe browser-visible UI, leave a runnable or reviewable browser surface for downstream QA instead of concluding with only service, library, or text output.");
            builder.AppendLine("- For Blazor deliverables, audit every component `@inject`, `[Inject]`, and constructor-injected application service against the app startup service collection such as `Program.cs`. Register or remove mismatched services before build/run proof; a component activation error from a missing DI registration is an incomplete implementation, even when compile-time tests pass.");
            builder.AppendLine("- For Blazor WebAssembly deliverables, verify the static asset mode matches the target framework and package versions. Do not leave `#[.{fingerprint}]` HTML placeholders, an empty import map, or `OverrideHtmlAssetPlaceholders` in a net8.0 or ASP.NET Core 8.x app unless browser proof confirms every fingerprinted asset and imported JavaScript module URL returns 200. Prefer stable dev-server paths such as `_framework/blazor.webassembly.js` and direct `wwwroot` module paths for net8.0 apps.");
            builder.AppendLine("- If the requested deliverable starts a local host, API, service, interactive UI, or executable workflow, perform a startup smoke with the appropriate run or launch tool after the latest build/test validation before writing final evidence.");
            AppendPromptLines(builder, softwareDeliveryGuidance.ImplementationProofLines);

            builder.AppendLine("- If no concrete deliverable exists yet, do not return Completed.");
            builder.AppendLine("- If no concrete deliverable exists yet and mutation or scaffold tools are available, do not return Blocked before attempting a concrete product mutation or scaffold and inspecting the resulting files or failure diagnostics.");
        }

        if (RequiresConcreteImplementationReview(candidate))
        {
            builder.AppendLine("- Because this review step depends on real implementation, inspect actual changed files, durable artifacts, records, or outputs in addition to managed summaries before you conclude.");
            builder.AppendLine("- Directly inspect every inherited implementation artifact path listed in the upstream artifact rules with workspace_stat_path and workspace_read_file before approving or rejecting the handoff. Summaries, prefetched excerpts, RAG snippets, and EvidenceRefs alone are not enough.");
            builder.AppendLine("- If an inherited implementation artifact path cannot be inspected, return Blocked and name the missing artifact path instead of approving readiness from memory or summaries.");
            builder.AppendLine("- If the implementation artifacts describe concrete required paths or records that the workspace does not contain, return Blocked with the missing concrete items instead of approving readiness.");
            builder.AppendLine("- Successful upstream validation receipts for the same unchanged deliverable count as evidence for this review step. Do not require fresh transient outputs unless the current step contract explicitly requires a rerun or those exact files.");
            builder.AppendLine("- When the implementation lives under a grounded external target, review the concrete deliverable in that target instead of blocking only because managed artifact folders do not contain product outputs.");
        }

        if (requiresConcreteBrowserProof)
        {
            AppendPromptLines(builder, softwareDeliveryGuidance.BrowserProofLines);
        }

        builder.AppendLine("- Produce the final machine-readable result as a ProcessStepOutcomeResult through the configured structured output format.");
        builder.AppendLine("- If the runtime exposes `submit_process_step_outcome`, call that finalizer tool exactly once with the same ProcessStepOutcomeResult before concluding.");
        builder.AppendLine("- When calling `submit_process_step_outcome`, pass exactly one `result` object argument shaped like `{ \"status\": \"Completed\", \"reason\": \"...\", \"evidenceRefs\": [\"artifacts/process-runs/...\"], \"nextActions\": [], \"humanReadableSummaryMarkdown\": \"...\" }`; do not pass scalar `result`, `status`, or `reason` sibling arguments.");
        builder.AppendLine("- Set Status to one of Completed, Blocked, Failed, WaitingApproval, or Refused. This Status field is the only source of truth for workflow continuation.");
        builder.AppendLine("- Put display-only markdown in HumanReadableSummaryMarkdown. Do not encode the workflow decision in markdown or an HTML comment.");
        builder.AppendLine("- Include a concrete Reason, EvidenceRefs for files/artifacts/tool outputs you relied on when available, and NextActions when the step is not completed.");
        if (candidate.BranchOutcomes.Count > 0)
        {
            builder.AppendLine("- If this step completes onto a specific downstream branch, set BranchOutcomeKey to the exact branchOutcomeKey from the available branch outcomes.");
            builder.AppendLine("- Branch outcomes are governed dispositions. If available evidence shows the deliverable needs repair, remediation, rework, changes required, or rejected validation, complete the review with the matching branch instead of returning Blocked.");
            builder.AppendLine("- Use status Blocked on a branched review only when you cannot make a valid branch disposition because required inputs, tools, environment, or decision authority are unavailable.");
        }

        builder.AppendLine("- Use status Completed only when the actual work of this step is done and the modeled next branch or next step may proceed.");
        builder.AppendLine("- For branched review or decision steps, Completed means the review disposition is complete; select the accepted branch only when validation passed, and select the repair/remediation/rework branch when the next modeled step is repair.");
        builder.AppendLine("- Use status Blocked when unresolved defects, missing proof, rejected approval, or required remediation mean the next step must not proceed and no available branch outcome represents the needed repair, remediation, rework, rejection, or escalation.");
        builder.AppendLine("- For an escalation/no-go/replan step with no downstream release branch, `Completed` means the escalation decision was recorded, not that the product became release-ready.");
        builder.AppendLine("- Do not use status Blocked for ambiguity that can be handled by explicit assumptions, `not applicable` entries, exclusions, or later modeled validation while the current step's required artifact or decision can still be completed.");
        builder.AppendLine("- Use status Failed only when tool, execution, or environment failure prevented you from producing a governed step result.");
        builder.Append($"Before concluding, create one durable workspace artifact for every required output listed above. Do not ask for confirmation, permission, or a follow-up reply before writing required artifacts. If a required artifact is a text or markdown file you can produce now, write it yourself with workspace tools instead of drafting it in chat. If no exact path is listed for that artifact, write it under `{requiredArtifactDefaultRoot}`. If required upstream artifacts are missing, stop and say so explicitly.");
        if (requiresConcreteProductProof)
        {
            builder.Append(" If the concrete deliverable required by this step does not exist, stop and say so explicitly.");
        }
        else
        {
            builder.Append(" For scope, architecture, research, planning, and other non-delivery steps, an absent greenfield deliverable is not a blocker by itself when the current step can record the intended boundary, assumptions, and downstream validation hooks.");
        }

        builder.Append(" Keep the response concise and mention what you completed.");
        return builder.ToString();
    }

    private static void AppendPromptLines(
        StringBuilder builder,
        IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(lines);

        foreach (var line in lines)
        {
            builder.AppendLine(line);
        }
    }

    private static void AppendRequiredArtifactResponseContract(
        StringBuilder builder,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        string requiredArtifactDefaultRoot)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(expectedArtifacts);

        var requiredArtifacts = expectedArtifacts
            .Where(item => item.IsRequired && !string.IsNullOrWhiteSpace(item.Title))
            .ToList();
        if (requiredArtifacts.Count == 0)
        {
            return;
        }

        builder.AppendLine("Required display summary structure:");
        builder.AppendLine("- In HumanReadableSummaryMarkdown, keep the display summary artifact-first. Use a dedicated markdown heading with the exact artifact title for every required output artifact.");
        builder.AppendLine("- Fill each required section with concrete content that satisfies its validation expectation. Do not leave headings empty, and do not replace the sections with a generic status summary.");

        foreach (var expectedArtifact in requiredArtifacts)
        {
            builder.Append("- `## ");
            builder.Append(expectedArtifact.Title.Trim());
            builder.Append('`');
            if (!string.IsNullOrWhiteSpace(expectedArtifact.ValidationRequirementSummary))
            {
                builder.Append(": ");
                builder.AppendLine(expectedArtifact.ValidationRequirementSummary.Trim());
            }
            else
            {
                builder.AppendLine();
            }
        }

        if (requiredArtifacts.Any(IsMigrationRolloutPreparationArtifact))
        {
            builder.AppendLine("- The migration/rollout checklist is required even when the implemented app has no database or persistent data. If no data migration is needed, say `No data migration required` and still name data changes, operational preconditions, validation evidence, and rollback steps.");
            builder.AppendLine("- A DB-free checklist is valid only when it explicitly says no schema migration, seed update, backfill, or data rollback is required, then lists rollout preconditions and code rollback steps.");
        }

        if (!string.IsNullOrWhiteSpace(requiredArtifactDefaultRoot))
        {
            builder.AppendLine($"- For pathless required text or markdown artifacts, create it under `{requiredArtifactDefaultRoot}` with a filename derived from the artifact title before finalizing.");
            builder.AppendLine("- Do not write pathless required evidence artifacts to `src/...`, `tests/...`, or other product/source paths unless the required artifact contract lists that exact grounded artifact path.");
            builder.AppendLine("A denied product/source write for required evidence should be retried as a managed artifact, not converted into `Blocked`.");
        }

        builder.AppendLine("- If you finish the step successfully, keep those exact section titles in HumanReadableSummaryMarkdown.");
        builder.AppendLine();
    }

    private static bool IsMigrationRolloutPreparationArtifact(DispatchArtifactExpectation expectedArtifact)
    {
        var text = string.Join(
            ' ',
            expectedArtifact.Title,
            expectedArtifact.ValidationRequirementSummary);
        return text.Contains("migration", StringComparison.OrdinalIgnoreCase) &&
               (text.Contains("rollout", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("rollback", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildCorrelationId(Guid stepRunId)
    {
        return $"process-step:{stepRunId:D}";
    }

}
