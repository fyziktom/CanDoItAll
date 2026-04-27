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
        var implementationMentionsTests = RequiresConcreteImplementationProof(candidate) &&
                                          (
                                              candidate.StepRun.Title.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                                              (workBrief?.WorkBriefText?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                              (workBrief?.ExpectedOutcome?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                              (workBrief?.EvidenceExpectationSummary?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false));
        ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out var projectStructureContext);
        var hasGroundedExternalTarget = TryResolveExternalTargetHintFromProjectStructureGrounding(
            projectStructureGroundingSummary,
            out var groundedExternalAbsolutePath,
            out var groundedExternalMappedAlias);
        var summarizedTriggerReason = ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason);
        var builder = new StringBuilder();
        builder.AppendLine("You are executing a CanDoItAll process step.");
        builder.AppendLine();
        builder.AppendLine($"Process: {candidate.Definition.Name}");
        builder.AppendLine($"Run: {candidate.Run.Name}");
        builder.AppendLine($"Step: {candidate.StepRun.Title}");
        builder.AppendLine($"Executor: {candidate.StepRun.CurrentExecutorName}");
        builder.AppendLine();
        builder.AppendLine("Run objective:");
        builder.AppendLine(string.IsNullOrWhiteSpace(summarizedTriggerReason)
            ? string.IsNullOrWhiteSpace(candidate.Definition.Summary)
                ? candidate.Definition.ValueStatement
                : candidate.Definition.Summary
            : summarizedTriggerReason);
        builder.AppendLine();
        if (projectStructureContext is not null)
        {
            builder.AppendLine("Project structure context:");
            builder.AppendLine(ProcessProjectStructureContextFormatter.BuildPromptSummary(projectStructureContext));
            builder.AppendLine();
            builder.AppendLine("Project structure execution rules:");
            builder.AppendLine(string.IsNullOrWhiteSpace(projectStructureGroundingSummary)
                ? $"- Use `project_structure_read` early in this step for project `{projectStructureContext.ProjectId:D}` so you inspect the live project graph instead of relying only on the selected node label."
                : $"- The dispatcher already fetched a live project-structure snapshot for this selected branch and included it below. Treat that grounding as a starting point, not a substitute for tool execution. You must still call `project_structure_read` early in this step for project `{projectStructureContext.ProjectId:D}` before you conclude.");
            builder.AppendLine("- Do not assume the selected task node contains every requirement. Carry forward concrete stack choices, output directories, examples, UI expectations, and acceptance notes that appear on related root or sibling project-structure nodes.");
            builder.AppendLine("- If the project structure names a concrete output directory outside the managed workspace, do not silently relocate the deliverable. Use a controlled local execution path when necessary, and record the exact external target in the artifacts you write.");
            builder.AppendLine("- Workspace file and execution tools cannot use a raw absolute external path like `C:\\target\\app` directly. Convert it to the mapped alias `external-target/C/target/app` when you call workspace tools that read, write, inspect, build, test, or launch files.");
            builder.AppendLine("- `workspace_pwsh_run_script` executes a script file from the managed workspace. If that script invokes native tools against an external target, convert `external-target/<drive>/...` back to a native path such as `C:\\target\\app` inside the script before passing it to `dotnet`, `Start-Process`, `Test-Path`, or `Resolve-Path`.");
            builder.AppendLine("- The mapped `external-target/<drive>/...` alias resolves to the real external target. Do not create a shadow copy in a different workspace folder.");
            builder.AppendLine("- Treat missing project-structure inspection as incomplete work for this step.");
            builder.AppendLine("- If project_structure_read reveals an exact external output directory for the selected work node, scaffold and implement in that exact location during this step instead of returning a note that the code does not exist yet.");
            if (hasGroundedExternalTarget)
            {
                builder.AppendLine($"- The grounded project structure already identifies the external output root `{groundedExternalAbsolutePath}` mapped to `{groundedExternalMappedAlias}`. Treat that mapped alias as the product root for this run, not as an optional example.");
            }
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(projectStructureGroundingSummary))
        {
            builder.AppendLine("Live project structure grounding:");
            builder.AppendLine(projectStructureGroundingSummary.Trim());
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
        builder.AppendLine("Evidence expectation:");
        builder.AppendLine(workBrief?.EvidenceExpectationSummary ?? "Save any relevant evidence artifacts inside the workspace.");
        builder.AppendLine();
        builder.AppendLine("Required output artifacts:");
        builder.AppendLine(BuildExpectedArtifactSummary(candidate.ExpectedArtifacts));
        builder.AppendLine();
        AppendRequiredArtifactResponseContract(builder, candidate.ExpectedArtifacts);
        builder.AppendLine("Upstream artifacts:");
        builder.AppendLine(BuildArtifactInputSummary(candidate.ArtifactInputs));
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
                    builder.AppendLine("- The dispatcher already inspected upstream governed artifact files and included the verified paths and excerpts below. Treat that grounding as current evidence, and call workspace_stat_path or workspace_read_file again only when you need broader or fresher inspection before you conclude.");
                }

                builder.AppendLine("- Use workspace_stat_path and workspace_read_file on the concrete workspace files or durable artifacts you cite as evidence. Do not rely only on summaries, RAG snippets, or prior notes.");
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
                builder.AppendLine("- Use workspace_write_file to write required markdown or text artifacts at their governed managed paths instead of relying on response projection.");
            }

            builder.AppendLine();
        }

        builder.AppendLine("Execution rules:");
        builder.AppendLine("- Complete the actual work described in the work brief and expected outcome before writing summary artifacts.");
        builder.AppendLine("- Required output artifacts are evidence of completed work. They do not replace code changes, runnable outputs, tests, screenshots, or other concrete deliverables.");
        builder.AppendLine("- Do not execute helper scripts, app launches, browser proof, release rollout, or other side actions unless the current step contract or required artifacts explicitly call for them.");
        builder.AppendLine("- Paths under artifacts/, output/, integration-map/, and data/ are managed workspace aliases for the current scope. Use them directly, and create missing managed directories or files when the step contract requires them.");
        builder.AppendLine("- Treat run-level paths and planned solution targets as context unless the current step contract explicitly tells you to create, inspect, build, test, launch, or review them. Only then must that concrete output exist before you conclude.");
        builder.AppendLine("- If the current step contract describes greenfield implementation or gives you a bootstrap or init script, missing solution or project files are expected pre-bootstrap state, not a blocker. Run the bootstrap or init step first, then inspect the scaffolded files and continue.");
        builder.AppendLine("- Do not claim that planned scaffold targets are missing deliverables when the current step contract explicitly tells you to create, bootstrap, or scaffold them in this step.");
        builder.AppendLine("- If a required build, test, launch, browser check, or artifact import fails, inspect the real diagnostics, fix the underlying problem, and rerun the same required validation before you conclude. Do not treat the first failed validation as acceptable end-state evidence.");
        builder.AppendLine("- After a failed validation tool call, the next tool call must inspect the failing diagnostics or mutate files that directly address the failure. Repeating the same failed build/test/run command without an intervening cause-directed change is no-progress behavior.");
        builder.AppendLine("- Do not stop after inspection, reconnaissance, bootstrap confirmation, or a next-steps summary if required tools, concrete deliverables, or required artifacts are still missing.");
        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("- Because this is an implementation step, create the real deliverable now. A markdown change set alone is not completed implementation.");
            builder.AppendLine("- Follow the current step contract, assigned agent instructions, available skills, and upstream artifacts to choose the correct project shape, folder structure, tools, and validation path.");
            builder.AppendLine("- Inspect existing files before creating or replacing scaffolds. Repair an existing deliverable in place when that is safer than recreating it.");
            builder.AppendLine("- Do not write implementation change-set or rollout artifacts until after concrete source, content, configuration, analysis, or deliverable mutations and required validation in the same attempt.");
            builder.AppendLine("- Concrete feature and constraint nodes from the live project structure are required scope for this implementation step. Treat them as mandatory deliverables now, not as later backlog or rollout notes.");
            builder.AppendLine("- Do not defer grounded features, UI behavior, acceptance notes, or output constraints into `future steps`, follow-up work, or QA-only cleanup while still returning `Completed`.");
            builder.AppendLine("- Before you conclude this implementation step, use available workspace tools to inspect the concrete files, records, or artifacts you created or changed.");
            builder.AppendLine("- Required proof must happen after the last mutation in the same attempt. Previous attempt receipts do not prove the current mutated output.");
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
            builder.AppendLine("- If no concrete deliverable exists yet, do not return Completed.");

            if (projectStructureContext is not null)
            {
                builder.AppendLine("- If the project structure sends you to an external target directory, map that directory to `external-target/<drive>/...`, create or update the real deliverable there, and inspect those mapped paths before you conclude.");
                builder.AppendLine("- Use `workspace_pwsh_run_script` only when you need a controlled helper command to bootstrap or verify the exact external target; otherwise stay on the mapped `external-target/...` path with the workspace tools.");
            }

            if (hasGroundedExternalTarget)
            {
                builder.AppendLine($"- For this implementation, create and edit the deliverable under `{groundedExternalMappedAlias}`. Do not build a shadow product in `artifacts/`, `output/`, `data/`, or other managed evidence folders when the grounded output root is external.");
            }

            if (implementationMentionsTests)
            {
                builder.AppendLine("- This implementation step explicitly includes tests. Add or update the relevant automated tests now and rerun the required validation before you conclude.");
                builder.AppendLine("- Do not defer implementation-owned tests to a later QA-only step when this step title, work brief, or expected outcome already says tests are part of the work.");
            }
        }

        if (RequiresConcreteImplementationReview(candidate))
        {
            builder.AppendLine("- Because this review step depends on real implementation, inspect actual changed files, durable artifacts, records, or outputs in addition to managed summaries before you conclude.");
            builder.AppendLine("- If the implementation artifacts describe concrete required paths or records that the workspace does not contain, return Blocked with the missing concrete items instead of approving readiness.");
            builder.AppendLine("- Successful upstream validation receipts for the same unchanged deliverable count as evidence for this review step. Do not require fresh transient outputs unless the current step contract explicitly requires a rerun or those exact files.");
            builder.AppendLine("- When the implementation lives under a grounded external target, review the concrete deliverable in that target instead of blocking only because managed artifact folders do not contain product outputs.");
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            builder.AppendLine("- This step requires runnable browser proof or screenshots, not build-only or file-only evidence.");
            builder.AppendLine("- Before browser proof, inspect the concrete host, launch instructions, prior validation receipts, or reviewed artifacts so you derive the actual target and reachable URL from the implementation.");
            builder.AppendLine("- If no reviewed browser surface is already running, start it using the launch path and toolchain appropriate for the assigned agent and current step contract, then capture the URL and diagnostics.");
            builder.AppendLine("- Do not assume a fixed URL. Use the actual URL reported by the launch command, host logs, configuration, or reviewed artifacts.");
            builder.AppendLine("- Do not treat an unstarted browser surface, a missing deployment, or unrelated transient output as acceptable proof when this QA step can launch or inspect the reviewed target itself.");
            builder.AppendLine("- Use browser tools after launch for navigation, accessibility or DOM proof, screenshot proof, and console diagnostics when those tools are available to the agent.");
            builder.AppendLine("- After browser inspection, review the captured snapshot or screenshot content. If it shows placeholder starter content or lacks the requested workflow, return Blocked or repair instead of claiming proof.");
            builder.AppendLine("- For interactive browser work, perform a representative user sequence and assert that visible state changes to the expected result.");
            builder.AppendLine("- If the app cannot be launched, the browser cannot be reached, screenshots cannot be captured, or the required UI flow is still missing, return `Blocked` instead of `Completed`.");
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
        }

        builder.AppendLine("- Use status Completed only when the actual work is done, the concrete deliverable exists, required validation passed, and the next step may proceed.");
        builder.AppendLine("- Use status Blocked when unresolved defects, missing proof, rejected approval, or required remediation mean the next step must not proceed yet.");
        builder.AppendLine("- Use status Failed only when tool, execution, or environment failure prevented you from producing a governed step result.");
        builder.Append("Before concluding, create one durable workspace artifact for every required output listed above. Do not ask for confirmation, permission, or a follow-up reply before writing required artifacts. If a required artifact is a text or markdown file you can produce now, write it yourself with workspace tools instead of drafting it in chat. If required upstream artifacts are missing or the concrete deliverable does not exist, stop and say so explicitly. Keep the response concise and mention what you completed.");
        return builder.ToString();
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
