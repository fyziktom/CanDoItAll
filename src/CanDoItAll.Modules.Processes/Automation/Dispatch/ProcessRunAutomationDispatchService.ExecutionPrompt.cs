using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
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
        var implementationMentionsTests = ImplementationContractMentionsTests(candidate);
        var implementationMentionsDotNet = ImplementationContractMentionsDotNet(candidate, projectStructureGroundingSummary);
        var implementationMentionsJavaScript = ImplementationContractMentionsJavaScript(candidate, projectStructureGroundingSummary);
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
        var usesScaffoldContractDrivenSetup = UsesScaffoldContractDrivenSetup(candidate);
        var isDotNetSolutionSetupScaffoldMutationStep = IsDotNetSolutionSetupScaffoldMutationStep(candidate);
        var operationContract = ResolveProcessStepOperationContract(candidate);
        var executionBoundary = ResolveProcessStepExecutionBoundary(candidate, operationContract);
        var effectiveCooperationMetadata = ResolveBoundaryAwareCooperationMetadata(candidate.CooperationMetadata, executionBoundary);
        var allowsExternalTargetMutation = AllowsExternalTargetMutation(candidate, executionBoundary, operationContract, projectStructureGroundingSummary);
        var currentRunManagedArtifactRoot = BuildCurrentRunManagedArtifactRoot(candidate);
        var currentRunManagedOutputRoot = BuildCurrentRunManagedOutputRoot(candidate);
        var usesGroundedExternalArtifactDestination = hasGroundedExternalTarget &&
            !requiresConcreteProductProof &&
            LooksLikeExternalArtifactDestination(candidate, projectStructureGroundingSummary);
        var requiredArtifactDefaultRoot = usesGroundedExternalArtifactDestination
            ? groundedExternalMappedAlias
            : currentRunManagedArtifactRoot;
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
            builder.AppendLine("- Workspace file and execution tools cannot use a raw absolute external path like `C:\\target\\app` directly. Convert it to the mapped alias `external-target/C/target/app` when you call workspace tools that read, write, inspect, validate, or launch files.");
            builder.AppendLine("- Only inspect or modify `external-target/...` paths that are explicitly named by this run's project-structure grounding, work brief, upstream step artifacts, or tool outputs from this run. Do not reuse remembered prior-example paths or external targets from prior runs.");
            builder.AppendLine("- Paths mentioned inside no-go, prohibited, out-of-scope, or exclusion language are constraints, not grounded targets. Do not stat, list, read, write, launch, or validate those paths unless the same current-run artifact also names them as the accepted product root.");
            builder.AppendLine("- Do not cite a file, path, tool result, example, or source artifact as evidence unless it was grounded by the current-run project structure, inspected by a current execution tool call, provided by an upstream artifact, or loaded from an attached skill/template resource. If source inspection was not performed in this run, say that instead of naming remembered files.");
            builder.AppendLine("- Do not include a `provided context`, `source-document context`, `ignored context`, or similar note that lists out-of-scope paths. If a path is unrelated to the current grounded product root, omit it from final artifacts entirely.");
            builder.AppendLine("- If tool policy denies an `external-target/...` path, treat that denied path as invalid for this run. Abandon it immediately and switch to the current grounded product root or current-run artifacts; do not retry or reason from the denied sample path.");
            builder.AppendLine("- `workspace_pwsh_run_script` executes a script file from the managed workspace. If that script invokes native tools against an external target, convert `external-target/<drive>/...` back to a native path such as `C:\\target\\app` inside the script before passing it to native commands like `Start-Process`, `Test-Path`, or `Resolve-Path`.");
            builder.AppendLine($"- In governed process steps, every `workspace_pwsh_run_script` or `workspace_python_run_file` call must include a `{GovernedScriptSideEffectManifest.ArgumentName}` JSON value. Use `NoMutation` for read-only scripts, `ManagedProcessArtifacts` with declared current-run artifact write paths for evidence writers, `ExternalArtifactDestination` for governed external artifact destinations, and `ProductMutation` only when this step explicitly permits product mutation.");
            builder.AppendLine("- The mapped `external-target/<drive>/...` alias resolves to the real external target. Do not create a shadow copy in a different workspace folder.");
            builder.AppendLine("- Treat missing project-structure inspection as incomplete work for this step.");
            builder.AppendLine("- If project_structure_read reveals an exact external output directory for the selected work node, keep that directory as the authoritative product boundary for this run. Create, bootstrap, or implement there only when the current step contract explicitly requires concrete delivery work.");
            builder.AppendLine("- A greenfield external product root can be absent during scope, architecture, research, or planning steps. If the current step does not require concrete implementation or validation, record the intended root as the boundary and leave creation, source listing, build, run, or test proof to the implementation and validation steps.");
            if (hasGroundedExternalTarget)
            {
                builder.AppendLine($"- The grounded project structure already identifies the external output root `{groundedExternalAbsolutePath}` mapped to `{groundedExternalMappedAlias}`. Treat that mapped alias as the product root for this run, not as an optional example.");
                builder.AppendLine("- If a temporary managed workspace is used for greenfield scaffolding or validation, the final runnable product must be delivered into the grounded external target before the step can be considered complete.");
                builder.AppendLine("- Completion evidence must cite build, run, or browser proof against the grounded external target after final delivery. Workspace-only proof is not sufficient when an external target is grounded.");
                builder.AppendLine($"- With a grounded external product root, treat the managed workspace as evidence and artifact scratch space only, preferably under `{currentRunManagedArtifactRoot}`. Do not inspect managed workspace source, test, tool, or script roots such as `src/`, `tests/`, `tools/`, or `scripts/` unless the current run's project structure, work brief, upstream artifacts, or current-run tool outputs explicitly name those paths.");
                if (usesGroundedExternalArtifactDestination)
                {
                    builder.AppendLine($"- This grounded external root is described as an artifact, report, plan, document, or handoff destination for non-implementation work. Write required generated deliverable artifacts under `{groundedExternalMappedAlias}` when no narrower artifact path is listed, and keep `{currentRunManagedArtifactRoot}` for scratch evidence, logs, or managed handoff copies.");
                }
                else if (!allowsExternalTargetMutation)
                {
                    builder.AppendLine($"- This step is non-mutating. Do not create directories or write files under `{groundedExternalMappedAlias}`. Write required architecture, scope, review, readiness, or planning artifacts under `{currentRunManagedArtifactRoot}` unless an exact governed artifact path is listed.");
                    builder.AppendLine("- Read product files only when they already exist and the current review step needs them. For a missing greenfield product root, record the intended boundary and leave creation to the modeled setup or implementation step.");
                }

                builder.AppendLine($"- In helper scripts that call native commands, use the native path `{groundedExternalAbsolutePath}` or convert `{groundedExternalMappedAlias}` to that native path before `Resolve-Path`, `Test-Path`, `Set-Location`, `Start-Process`, `cmd.exe`, `node`, `npm`, or similar native calls. Never pass an `external-target/...` alias directly to native PowerShell or process APIs.");
                builder.AppendLine($"- Do not use broad managed-root workspace listing or search to discover launch helpers, source code, or requirements for this external-target run. List or search the grounded external-target alias and `{currentRunManagedArtifactRoot}` instead.");
                builder.AppendLine("- Do not use files discovered only from broad managed workspace browsing as product requirements, app source, launch scripts, or validation helpers for this run.");
                builder.AppendLine("- Do not list, read, cite, copy, or infer implementation patterns from sibling external-target applications on the same host. Framework examples must come from loaded skills, tool descriptions, official templates, or current-run artifacts, not from unrelated local apps.");
                builder.AppendLine("- Never write `contextual example files`, `source files reviewed`, or similar evidence claims unless the exact files were inspected by current-run tool calls and are inside the grounded product root, current-run artifact root, or an explicitly grounded upstream input.");
            }
            else
            {
                builder.AppendLine($"- The dispatcher did not ground an external product root for this run. Do not invent, create, retry, or cite any `external-target/...` path unless a current-run project_structure_read result names an exact absolute local path.");
                builder.AppendLine($"- If the current step must create a concrete greenfield deliverable and no external product root is found after project_structure_read, use `{currentRunManagedOutputRoot}` as the product root and `{currentRunManagedArtifactRoot}` for evidence.");
                if (implementationMentionsDotNet)
                {
                    builder.AppendLine($"- For .NET scaffolding without an external product root, use `workspace_dotnet_new` under `{currentRunManagedOutputRoot}`; do not use the bare managed workspace root, shared `src/`, shared `tests/`, or guessed host folders.");
                }
                else if (implementationMentionsJavaScript)
                {
                    builder.AppendLine($"- For JavaScript or TypeScript greenfield deliverables without an external product root, create files under `{currentRunManagedOutputRoot}` with the package/script toolchain named by the current-run requirements. Do not use `workspace_dotnet_new` unless the current-run requirements explicitly name .NET, C#, ASP.NET, Blazor, Razor, `.csproj`, or `.sln`.");
                }
                else
                {
                    builder.AppendLine($"- For greenfield deliverables without an external product root, create files under `{currentRunManagedOutputRoot}` with the toolchain explicitly named by the current-run requirements. Do not assume a .NET, JavaScript, Python, document, or other stack without a current-run stack signal.");
                }
            }
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(projectStructureGroundingSummary))
        {
            builder.AppendLine("Live project structure grounding:");
            builder.AppendLine(projectStructureGroundingSummary.Trim());
            builder.AppendLine();
        }

        if (isDotNetSolutionSetupScaffoldMutationStep)
        {
            builder.AppendLine(".NET setup subprocess boundary:");
            builder.AppendLine("- This is a scaffold/setup mutation step, not feature implementation, QA, runtime smoke, or browser proof.");
            builder.AppendLine("- Create or repair only the files named by this step's scaffold contract. Do not add feature behavior, feature tests, template cleanup, package upgrades, browser checks, or runtime proof unless this exact step explicitly requires them.");
            builder.AppendLine("- Do not run `dotnet run`, launch a web app, invoke browser tools, or create a long-running app process in this step. Leave build/test/runtime/browser validation to the validation step or parent QA step.");
            builder.AppendLine("- For `create-dotnet-project`, create the solution and app project only. Do not create the test project in that step; the test project belongs to the separate `add-test-project` step.");
            builder.AppendLine("- Evidence for this step is scaffold file presence, readback of representative solution/project files, and the required setup change-set artifact under the current-run artifact root.");
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
            builder.AppendLine("Required tool execution checklist:");
            builder.AppendLine($"- Completion of this step is gated on successful current-run tool receipts for: {string.Join(", ", requiredToolNames.Select(toolName => $"`{toolName}`"))}.");
            builder.AppendLine("- These are not optional hints. If a required tool is unavailable, denied, or cannot be run against the concrete target, return Blocked or Failed with the exact tool and reason.");
            if (RequiresConcreteImplementationProof(candidate))
            {
                builder.AppendLine("- For implementation steps, `workspace_write_file` is required for durable handoff evidence after concrete product mutation and validation. It does not replace creating, scaffolding, editing, reading, and validating real product files.");
            }

            builder.AppendLine();
        }

        AppendBrowserProofBoundaryNote(
            builder,
            requiresConcreteBrowserProof,
            browserProofGroundingText);

        AppendMandatoryBrowserProofPlan(
            builder,
            candidate,
            requiresConcreteBrowserProof,
            implementationMentionsDotNet,
            implementationMentionsJavaScript,
            currentRunManagedArtifactRoot);

        AppendRequiredArtifactResponseContract(builder, candidate.ExpectedArtifacts);
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
        builder.AppendLine("- If the current step contract describes greenfield implementation or gives you a bootstrap or init script, missing solution or project files are expected pre-bootstrap state, not a blocker. Run the bootstrap or init step first, then inspect the scaffolded files and continue.");
        builder.AppendLine("- Do not claim that planned scaffold targets are missing deliverables when the current step contract explicitly tells you to create, bootstrap, or scaffold them in this step.");
            builder.AppendLine("- If a required build, test, launch, browser check, or artifact import fails, inspect the real diagnostics, fix the underlying problem, and rerun the same required validation before you conclude. Do not treat the first failed validation as acceptable end-state evidence.");
            builder.AppendLine("- After a failed validation tool call, the next tool call must inspect the failing diagnostics or mutate files that directly address the failure. Repeating the same failed build/test/run command without an intervening cause-directed change is no-progress behavior.");
            builder.AppendLine("- Do not make validation pass by writing fake package, framework, runtime, browser, or test-tool shims. Fix real dependencies and project references, or return Blocked with the exact missing dependency or environment issue.");
            builder.AppendLine("- Do not stop after inspection, reconnaissance, bootstrap confirmation, or a next-steps summary if required tools, concrete deliverables required by this step, or required artifacts are still missing.");
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
            builder.AppendLine("- If the requested deliverable starts a local host, API, service, interactive UI, or executable workflow, perform a startup smoke with the appropriate run or launch tool after the latest build/test validation before writing final evidence.");
            if (implementationMentionsDotNet)
            {
                builder.AppendLine("- If `workspace_dotnet_run` fails after build/test passed, treat it as a repairable runtime defect before you return Blocked: inspect the startup diagnostics and repair missing DI registrations, `Program.cs` service wiring, routing, configuration, launch settings, static assets, or app initialization that caused the host or first HTTP request to fail.");
            }
            else
            {
                builder.AppendLine("- If the appropriate startup smoke fails after validation passed, treat it as a repairable runtime defect before you return Blocked: inspect startup diagnostics and repair the real entry point, routing, static assets, package scripts, configuration, or app initialization that caused the failure.");
            }

            builder.AppendLine("- If no concrete deliverable exists yet, do not return Completed.");
            builder.AppendLine("- If no concrete deliverable exists yet and mutation or scaffold tools are available, do not return Blocked before attempting a concrete product mutation or scaffold and inspecting the resulting files or failure diagnostics.");

            if (hasProjectStructureExecutionContext)
            {
                builder.AppendLine("- If the project structure sends you to an external target directory, map that directory to `external-target/<drive>/...`, create or update the real deliverable there, and inspect those mapped paths before you conclude.");
                builder.AppendLine("- Use `workspace_pwsh_run_script` only when you need a controlled helper command to bootstrap or verify the exact external target; otherwise stay on the mapped `external-target/...` path with the workspace tools.");
            }

            if (hasGroundedExternalTarget)
            {
                builder.AppendLine($"- For this implementation, create and edit the deliverable under `{groundedExternalMappedAlias}`. Do not build a shadow product in `artifacts/`, `output/`, `data/`, or other managed evidence folders when the grounded output root is external.");
                builder.AppendLine($"- If `{groundedExternalMappedAlias}` contains only markdown, notes, summaries, checklists, logs, or empty folders, treat it as an unimplemented product root. Scaffold or create the requested application, service, UI, document, analysis, or other concrete deliverable there before final artifacts.");
                builder.AppendLine($"- If `{groundedExternalMappedAlias}` is an unimplemented product root, the next product action must be a concrete mutation under that root, such as scaffolding a project, writing source/configuration files, or repairing generated content. Do not write final evidence artifacts or submit Blocked before trying that concrete mutation and reading either the changed files or the failure receipt.");
                if (hasGroundedExternalScaffoldTarget && implementationMentionsDotNet)
                {
                    if (usesScaffoldContractDrivenSetup)
                    {
                        builder.AppendLine("- The upstream scaffold contract overrides the generic product-root leaf scaffold shortcut. Read the scaffold contract before scaffolding, then use its solution name, app project name, app directory, template, target framework, and test framework exactly.");
                        builder.AppendLine($"- Treat `{groundedExternalMappedAlias}` as the solution/product root named by the contract. Do not derive an app project name from the product-root folder leaf `{groundedExternalLeafName}` and do not scaffold the app directly at the product root unless the scaffold contract explicitly says so.");
                        builder.AppendLine($"- For contract-driven .NET solution setup, create the product root when needed, scaffold the solution at `{groundedExternalMappedAlias}`, create the app parent directory from the contract such as `{groundedExternalMappedAlias}/src`, and set `workspace_dotnet_new` `name` to the contract's app project name.");
                        builder.AppendLine($"- `{groundedExternalParentAlias}` is only the parent of the product root. It is not a product root, source corpus, evidence root, or permission to inspect sibling folders.");
                    }
                    else
                    {
                        builder.AppendLine($"- For .NET scaffolding into the grounded external product root, use `workspace_dotnet_new` with `parentDirectory` set to `{groundedExternalParentAlias}` and `name` set to `{groundedExternalLeafName}`. If `{groundedExternalMappedAlias}` already exists, inspect and repair it in place instead of creating a sibling or managed artifact copy.");
                        builder.AppendLine("- Choose the .NET template and project shape named by the current-run requirements. Do not default to Blazor, Razor, or Web App templates unless the selected work branch explicitly asks for browser UI, Blazor, or Razor; console apps, minimal APIs, workers, services, and libraries must keep their requested archetype.");
                        builder.AppendLine($"- If `{groundedExternalMappedAlias}` has no project or source files, invoke `workspace_dotnet_new` with `parentDirectory` `{groundedExternalParentAlias}`, `name` `{groundedExternalLeafName}`, and `force` false before writing implementation-summary artifacts. Existing markdown, checklist, log, or README files in that directory are not a scaffold and are not a reason to skip project creation.");
                        builder.AppendLine($"- If `workspace_dotnet_new` cannot scaffold into `{groundedExternalMappedAlias}` because the directory already has evidence files, repair the root in place by writing the required project/source files or return Blocked with the exact scaffold diagnostic. Do not recursively delete `{groundedExternalMappedAlias}` to make room.");
                        builder.AppendLine($"- `{groundedExternalParentAlias}` is only the scaffold parent argument for creating `{groundedExternalMappedAlias}`. It is not a product root, evidence root, source corpus, or permission to inspect sibling folders.");
                        builder.AppendLine($"- After scaffolding, all reads, writes, builds, tests, runs, and evidence citations must target `{groundedExternalMappedAlias}` or `{currentRunManagedArtifactRoot}`, not sibling folders under `{groundedExternalParentAlias}`.");
                    }
                }
                else if (hasGroundedExternalScaffoldTarget && implementationMentionsJavaScript)
                {
                    builder.AppendLine($"- For JavaScript or TypeScript scaffolding into the grounded external product root, create or update the real deliverable directly under `{groundedExternalMappedAlias}` using the package/script toolchain named by the current-run requirements.");
                    builder.AppendLine("- Do not use `workspace_dotnet_new` for JavaScript, static HTML, Python, document, analysis, business, or other non-.NET work unless the current-run requirements explicitly name .NET, C#, ASP.NET, Blazor, Razor, `.csproj`, or `.sln`.");
                    builder.AppendLine($"- `{groundedExternalParentAlias}` is only the parent of the product root. It is not a product root, evidence root, source corpus, or permission to inspect sibling folders.");
                    builder.AppendLine($"- After scaffolding or file creation, all reads, writes, builds, tests, runs, and evidence citations must target `{groundedExternalMappedAlias}` or `{currentRunManagedArtifactRoot}`, not sibling folders under `{groundedExternalParentAlias}`.");
                }
                else if (hasGroundedExternalScaffoldTarget)
                {
                    builder.AppendLine($"- For scaffolding into the grounded external product root, create or update the real deliverable directly under `{groundedExternalMappedAlias}` using the toolchain explicitly named by the current-run requirements.");
                    builder.AppendLine("- Do not infer a stack only from prior examples, sibling folders, or generic application wording. Use the stack named by the selected work branch, upstream artifacts, or attached skills.");
                    builder.AppendLine($"- `{groundedExternalParentAlias}` is only the parent of the product root. It is not a product root, evidence root, source corpus, or permission to inspect sibling folders.");
                    builder.AppendLine($"- After scaffolding or file creation, all reads, writes, builds, tests, runs, and evidence citations must target `{groundedExternalMappedAlias}` or `{currentRunManagedArtifactRoot}`, not sibling folders under `{groundedExternalParentAlias}`.");
                }
            }

            if (implementationMentionsTests)
            {
                builder.AppendLine("- This implementation step explicitly includes tests. Add or update the relevant automated tests now and rerun the required validation before you conclude.");
                builder.AppendLine("- Keep automated tests in a dedicated test project or test folder that references the implementation. Do not move test classes into the runnable app project or delete tests to bypass build/test failures.");
                builder.AppendLine("- Do not defer implementation-owned tests to a later QA-only step when this step title, work brief, or expected outcome already says tests are part of the work.");
            }

            if (implementationMentionsDotNet)
            {
                builder.AppendLine("- For Blazor forms, bind inputs only to settable properties or explicit get/set wrappers. Positional records and init-only properties are not valid `@bind` targets and must be replaced with mutable form-state classes or properties before rerunning the build.");
                builder.AppendLine("- For .NET HTTP startup proof that does not need same-step browser follow-up, leave `workspace_dotnet_run` `keepAlive` false so the smoke test stops the launched process tree and avoids locking later builds. If this same step must run browser tools, set `keepAlive: true`, capture browser evidence, and cite the startup receipt; the dispatcher stops the kept-alive process tree after the finalizer, so do not run a cleanup script.");
            }
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
            builder.AppendLine("- This step requires runnable browser proof or screenshots, not build-only or file-only evidence.");
            builder.AppendLine("- Before browser proof, inspect the concrete host, launch instructions, prior validation receipts, or reviewed artifacts so you derive the actual target and reachable URL from the implementation.");
            builder.AppendLine("- If no reviewed browser surface is already running, start it using the launch path and toolchain appropriate for the assigned agent and current step contract, then capture the URL and diagnostics.");
            if (implementationMentionsDotNet)
            {
                builder.AppendLine("- For .NET browser proof, call `workspace_dotnet_run` with `keepAlive: true` so Playwright can reach the app. After browser evidence is captured, cite the startup receipt and final evidence; the dispatcher stops the kept-alive process tree after the finalizer, so do not run `workspace_pwsh_run_script` just for cleanup.");
            }
            else if (implementationMentionsJavaScript)
            {
                builder.AppendLine("- For JavaScript or TypeScript browser proof, start the app with the reviewed package script or launch path available to the assigned agent, preserve the actual URL and diagnostics, then stop any started process before finalizing.");
                builder.AppendLine("- Do not use `workspace_dotnet_build`, `workspace_dotnet_test`, or `workspace_dotnet_run` for JavaScript or TypeScript deliverables unless the current-run requirements explicitly name .NET, C#, ASP.NET, Blazor, Razor, `.csproj`, or `.sln`.");
                builder.AppendLine($"- If the available runner is `workspace_pwsh_run_script`, first create a helper script under `{currentRunManagedArtifactRoot}` with `workspace_write_file`, then inspect or stat that script before running it. Do not invoke a helper path that has not been created in this current run.");
                builder.AppendLine("- If a PowerShell helper writes another PowerShell script, use a single-quoted here-string (`@' ... '@`) or escape every literal `$` in the nested script. Read the generated nested script before running it; malformed lines such as `param([string] = ...)`, `.Start()`, or `.OutputStream.Write(,...)` mean variable expansion corrupted the child script and must be repaired before rerun.");
                builder.AppendLine("- If a PowerShell helper starts a package preview, static server, `HttpListener`, `python -m http.server`, or similar long-running browser host, launch that host as a background child process, wait for a reachable URL or startup log, write the URL and process id to durable evidence, then let the helper exit. Do not run the long-running server loop inside the `workspace_pwsh_run_script` process until the tool times out.");
                builder.AppendLine("- For long-running browser hosts, do not call blocking stream reads such as `.ReadToEnd()`, `.ReadToEndAsync().Result`, `.WaitForExit()`, or equivalent waits on redirected stdout/stderr. Redirect output to files, inherit handles, or use nonblocking event handlers so the helper can return after recording URL and process id.");
                builder.AppendLine("- A non-.NET helper script must convert an `external-target/<drive>/...` alias back to a native path before calling native commands such as `Resolve-Path`, `Test-Path`, `Set-Location`, `Start-Process`, `cmd.exe`, `node`, `npm`, `python`, or static-file launchers. Capture exit codes, stdout/stderr, the actual URL, and cleanup details in durable evidence.");
                builder.AppendLine("- On Windows, package-manager launch helpers must invoke the real command shim, for example `npm.cmd run preview`, or use `cmd.exe /d /s /c \"npm run preview\"`. Do not use `Start-Process -FilePath 'npm'`; if a helper reports `%1 is not a valid Win32 application`, rewrite it to use `npm.cmd` or `cmd.exe` and rerun the launch.");
                builder.AppendLine("- Never write helper code like `Resolve-Path 'external-target/C/...'`; native PowerShell resolves that relative to the managed artifact directory and will fail. Translate the alias to `C:\\...` first.");
            }
            else
            {
                builder.AppendLine("- For browser proof with an unspecified stack, first inspect the reviewed host, package, launch instructions, or upstream artifacts to determine the actual toolchain. Use .NET, package-script, Python, static-file, or other launch tooling only after that current-run evidence identifies the stack.");
                builder.AppendLine($"- If the only available launch runner is `workspace_pwsh_run_script`, first create a helper script under `{currentRunManagedArtifactRoot}` with `workspace_write_file`, then inspect or stat that script before running it. Do not invoke a helper path that has not been created in this current run.");
            }

            builder.AppendLine("- Do not assume a fixed URL. Use the actual URL reported by the launch command, host logs, configuration, or reviewed artifacts.");
            builder.AppendLine("- Do not treat an unstarted browser surface, a missing deployment, or unrelated transient output as acceptable proof when this QA step can launch or inspect the reviewed target itself.");
            builder.AppendLine("- Use browser tools after launch for navigation, accessibility or DOM proof, screenshot proof, and console diagnostics when those tools are available to the agent.");
            builder.AppendLine("- Keep browser evidence bounded: call `browser_snapshot` with depth 2 and boxes false unless a specific contract requires depth 3 or 4. Call `browser_take_screenshot` with fullPage false or omit fullPage. Do not retry a policy-denied browser call with the same arguments.");
            builder.AppendLine("- When a browser tool accepts a `filename`, write it under the current-run managed artifact root or an exact required browser artifact path. The dispatcher prepares those raw MCP output directories before the run so browser screenshots, snapshots, and console logs can be persisted and imported.");
            builder.AppendLine("- Browser screenshots, snapshots, console logs, and state outputs must be process-visible current-run artifacts. Do not rely on chat-only mentions, stale prior-run files, or unattached markdown links when the step contract requires browser evidence.");
            builder.AppendLine("- Provider-native browser files are created by the browser MCP before managed scope aliasing. Review the browser tool result itself and cite the returned filename; do not block only because `workspace_read_file` cannot read a provider-native `artifacts/process-runs/...` browser file.");
            builder.AppendLine("- If this is a static browser deliverable and the current step contract requires runtime or browser proof but does not explicitly require automated tests, a package manifest, or a nonzero test count, do not make missing `package.json` or missing automated tests release-blocking by itself. Record that as quality risk and validate with source inspection plus launch, console, screenshot or DOM, and representative interaction evidence.");
            builder.AppendLine("- After browser inspection, review the bounded snapshot, screenshot, or tool-returned content. If it shows placeholder starter content or lacks the requested workflow, return Blocked or repair instead of claiming proof.");
            builder.AppendLine("- For interactive browser work, perform a representative user sequence and assert that visible state changes to the expected result. For canvas, game, custom-control, or keyboard-first surfaces, use `browser_evaluate` when ordinary browser click/fill helpers cannot express the workflow; dispatch representative keyboard or pointer events and inspect visible state, DOM text, or client-side storage.");
            builder.AppendLine("- If a screenshot call fails, retry once with viewport capture. If bounded snapshot, console diagnostics, and visible-state checks prove the workflow and no exact screenshot artifact is required, do not block solely on the screenshot failure.");
            builder.AppendLine("- If `browser_snapshot` fails because of a tool-side selector, parsing, or accessibility-tree issue after navigation, screenshot, console diagnostics, and representative visible-state checks succeeded, replace it with `browser_evaluate` DOM or state proof and cite the snapshot failure. Do not block solely on the missing snapshot artifact unless the step explicitly requires that exact artifact.");
            builder.AppendLine("- If the app cannot be launched, the browser cannot be reached, bounded browser evidence cannot be captured, or the required UI flow is still missing, do not approve the proof.");
            builder.AppendLine("- When this step has an available branch outcome for repair, remediation, rework, changes required, or rejected validation, use status `Completed` with that exact BranchOutcomeKey for reproducible product defects or missing implemented behavior. Use `Blocked` only when missing inputs, denied tools, unavailable environment, or missing authority prevents you from making the governed quality disposition.");
            builder.AppendLine("- Do not reframe missing browser proof as a residual risk, deferred next step, or artifact-only note while still marking the step complete.");
        }

        builder.AppendLine("- Produce the final machine-readable result as a ProcessStepOutcomeResult through the configured structured output format.");
        builder.AppendLine("- If the runtime exposes `submit_process_step_outcome`, call that finalizer tool exactly once with the same ProcessStepOutcomeResult before concluding.");
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

    private static void AppendBrowserProofBoundaryNote(
        StringBuilder builder,
        bool requiresConcreteBrowserProof,
        string? browserProofGroundingText)
    {
        if (requiresConcreteBrowserProof ||
            !ContainsExplicitBrowserSurfaceSignal(browserProofGroundingText ?? string.Empty))
        {
            return;
        }

        builder.AppendLine("Browser proof boundary:");
        builder.AppendLine("- The current project may have a browser-visible surface, but this step is not browser-proof gated. Do not launch the app, invoke browser tools, or return Blocked for missing browser receipts unless this step's own contract explicitly requires runtime or browser proof.");
        builder.AppendLine("- If browser/runtime validation is needed later, record it as a downstream QA, release, or repair requirement instead of converting this step into that validation step.");
        builder.AppendLine("- If upstream QA, release, or review artifacts include browser snapshots, screenshots, console logs, or regression evidence files, inspect those inherited artifact paths directly with workspace tools when the prompt lists them. Consuming inherited browser evidence is not the same as capturing fresh browser proof.");
        builder.AppendLine();
    }

    private static bool LooksLikeExternalArtifactDestination(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary)
    {
        var context = string.Join(
            '\n',
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.StepDefinition.InputContractSummary,
            candidate.StepDefinition.OutputContractSummary,
            candidate.StepDefinition.EvidenceContractSummary,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            projectStructureGroundingSummary);

        return ContainsAnyArtifactDestinationSignal(
            context,
            [
                "artifact destination",
                "deliverable artifact",
                "document output",
                "report output",
                "plan output",
                "handoff folder",
                "business plan",
                "marketing plan",
                "financial model",
                "strategy brief",
                "research report",
                "analysis report",
                "decision package"
            ]) &&
            !ContainsAnyArtifactDestinationSignal(
                context,
                [
                    "product root",
                    "generated app source",
                    "app source belongs",
                    ".sln",
                    ".csproj",
                    "solution name",
                    "app project",
                    "test project",
                    "console app",
                    "blazor",
                    "razor",
                    "asp.net",
                    "javascript browser app",
                    "static javascript",
                    "index.html",
                    "app.js",
                    "package.json"
                ]);
    }

    private static bool UsesScaffoldContractDrivenSetup(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.ArtifactInputs.Any(ReferencesScaffoldContract))
        {
            return true;
        }

        var context = string.Join(
            '\n',
            candidate.Definition.Name,
            candidate.StepRun.Title,
            candidate.StepDefinition.InputContractSummary,
            candidate.StepDefinition.OutputContractSummary,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary);

        return context.Contains("scaffold contract", StringComparison.OrdinalIgnoreCase) ||
               context.Contains(".NET solution setup subprocess", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReferencesScaffoldContract(DispatchArtifactInput artifactInput)
    {
        if (artifactInput.ExpectedArtifactTitle.Contains("scaffold contract", StringComparison.OrdinalIgnoreCase) ||
            artifactInput.SourceStepTitle.Contains("scaffold contract", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return artifactInput.Artifacts.Any(artifact =>
            artifact.Title.Contains("scaffold contract", StringComparison.OrdinalIgnoreCase) ||
            artifact.ManagedStoragePath.EndsWith("scaffold-contract.md", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAnyArtifactDestinationSignal(string text, IReadOnlyCollection<string> needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static void AppendMandatoryBrowserProofPlan(
        StringBuilder builder,
        DispatchCandidate candidate,
        bool requiresConcreteBrowserProof,
        bool implementationMentionsDotNet,
        bool implementationMentionsJavaScript,
        string currentRunManagedArtifactRoot)
    {
        if (!requiresConcreteBrowserProof)
        {
            return;
        }

        builder.AppendLine("Mandatory browser proof execution plan:");
        builder.AppendLine("- Do not submit the final step outcome from file inspection alone. Current-run browser evidence must come from provider-native browser tools after the reviewed app is reachable.");
        if (implementationMentionsDotNet)
        {
            builder.AppendLine("- Start or verify the reviewed .NET host first. Prefer `workspace_dotnet_run` with `keepAlive: true`, capture the reported URL, and let the dispatcher stop the kept-alive process tree after the finalizer.");
        }
        else if (implementationMentionsJavaScript)
        {
                builder.AppendLine($"- Start or verify the reviewed JavaScript host first. If the only launch runner is `workspace_pwsh_run_script`, create a helper under `{currentRunManagedArtifactRoot}`, create its stdout/stderr directories, convert any `external-target/<drive>/...` alias to a native path inside the helper, invoke package scripts through the Windows shim such as `npm.cmd` or through `cmd.exe /d /s /c \"npm run <script>\"`, and capture the actual localhost URL plus cleanup details.");
                builder.AppendLine("- If that helper writes another PowerShell script, use a single-quoted here-string (`@' ... '@`) or escape literal `$` characters, then read the generated nested script before executing it.");
                builder.AppendLine("- The helper must not be the foreground web server. It must start any long-running static server or package preview as a background child process, wait until a URL is reachable, record the URL and process id, and exit so browser MCP tools can run next.");
                builder.AppendLine("- Do not pass a server implementation script itself to `workspace_pwsh_run_script` when its main body constructs `HttpListener`, calls `GetContext`, runs `python -m http.server`, or contains a request loop. Execute only a bounded launcher script that starts that server as a child process and exits after reachability proof.");
                builder.AppendLine("- Do not call blocking stream reads such as `.ReadToEnd()`, `.ReadToEndAsync().Result`, `.WaitForExit()`, or equivalent waits on redirected stdout/stderr for that long-running child process. Redirect output to files, inherit handles, or use nonblocking event handlers.");
                builder.AppendLine("- In PowerShell launch helpers, prefer `Start-Process -FilePath <command> -ArgumentList <args> -WorkingDirectory <native-path> -RedirectStandardOutput <stdout-file> -RedirectStandardError <stderr-file> -PassThru` for long-running hosts. Do not use `[System.Threading.Tasks.Task]::Run({ ... })` with scriptblocks to copy redirected streams; PowerShell can throw an ambiguous overload binding error before the server starts.");
                builder.AppendLine("- Use native absolute paths for stdout/stderr redirection. If `Start-Process -WorkingDirectory` points at the product root, relative redirect paths such as `artifacts/process-runs/...` will be resolved under the product root and become unreadable from workspace tools.");
                builder.AppendLine("- Do not build child PowerShell server code as a double-quoted `-Command` string when that code contains variables such as `$listener`, `$context`, `$request`, or `$file`. Write a separate child `.ps1` file with a single-quoted here-string, read it back, then launch it with `-File`, or use a reviewed package/static server command instead.");
                builder.AppendLine("- Treat HTTP reachability as the startup proof. Probe the recorded URL with a bounded `Invoke-WebRequest` loop before returning from the helper instead of relying only on stdout text from a long-running child process.");
        }
        else
        {
            builder.AppendLine("- Start or verify the reviewed browser surface first using the stack identified from current-run files, launch settings, package scripts, or upstream artifacts, then capture the actual URL and cleanup details.");
        }

        builder.AppendLine("- After a reachable URL is known, call `browser_navigate` against that URL, exercise one representative user-visible interaction when the surface is interactive, then call `browser_snapshot` with depth 2 and boxes false, `browser_take_screenshot` with fullPage false or no fullPage argument, and `browser_console_messages` in this same attempt.");
        builder.AppendLine("- Cite the returned screenshot, snapshot or state, and console filenames in the durable evidence artifact so the process can import them and expose the output folder through linked project structure.");
        builder.AppendLine("- If ordinary browser interaction tools cannot express the interaction, use `browser_evaluate` to dispatch representative keyboard or pointer events and read visible state, DOM text, or client-side storage.");
        builder.AppendLine("- If the contract does not explicitly require automated tests, a package manifest, or nonzero test count, do not block static browser deliverable approval only because those artifacts are absent; use browser/runtime proof and record automation coverage as residual quality risk.");
        builder.AppendLine("- If `browser_snapshot` fails for a tool-side selector, parsing, or accessibility-tree reason after other browser evidence proves the workflow, use `browser_evaluate` DOM or state proof as the replacement DOM evidence unless the exact snapshot artifact is explicitly required.");
        builder.AppendLine("- If launch fails before a URL exists, return Blocked with the exact launch command, logs, and repair target. If launch succeeds, do not return Blocked for missing browser receipts before attempting the browser tools.");
        builder.AppendLine();
    }

    private static void AppendRequiredArtifactResponseContract(
        StringBuilder builder,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
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
