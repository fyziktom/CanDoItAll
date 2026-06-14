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
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string BuildRecoveryDirective(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures,
        int attemptNumber)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Attempt {attemptNumber} ended before the step was actually complete.");
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, responseText);
        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, responseText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        var missingRunnableApplicationProofSummary = ResolveMissingRunnableApplicationProofSummary(candidate, detail);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);
        var invalidQualityValidationProofSummary = ResolveInvalidQualityValidationProofSummary(
            candidate,
            detail,
            ResolveOutputInspectionText(responseText));
        var missingUpstreamArtifactInspectionPaths = ResolveMissingUpstreamArtifactInspectionPaths(candidate, detail);
        var missingUpstreamArtifactInspectionSummary = ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail);
        var softwareDeliveryRecoveryGuidance = SoftwareDeliveryGuidancePolicy.CreateRecoveryGuidance(
            new SoftwareDeliveryRecoveryGuidanceRequest(
                CreateSoftwareDeliveryImplementationContractSnapshot(
                    candidate,
                    requiresConcreteBrowserProof: RequiresConcreteBrowserProof(candidate)),
                HasProjectStructureContext(candidate),
                !string.IsNullOrWhiteSpace(missingRunnableApplicationProofSummary),
                BuildCurrentRunManagedArtifactRoot(candidate)));
        var outOfScopeExternalTargetReferenceSummary = ResolveOutOfScopeExternalTargetReferenceSummary(
            detail,
            ResolveOutputInspectionText(responseText));
        var shallowSharedManagedArtifactReferenceSummary = ResolveShallowSharedManagedArtifactReferenceSummary(
            detail,
            ResolveOutputInspectionText(responseText));
        var recoverableGovernedOutcomeGap = IsRecoverableGovernedOutcomeGap(candidate, responseText);
        var recoverableFinalizerValidationFailure = TryResolveRecoverableFinalizerValidationFailure(
            candidate,
            detail,
            responseText,
            out var finalizerFailureSummary);
        var recoverableExecutionInterruption = TryResolveRecoverableExecutionInterruption(
            detail,
            responseText,
            out var executionInterruptionSummary);

        if (missingRequiredTools.Count > 0)
        {
            var missingDotNetNewForSetupScaffold =
                missingRequiredTools.Contains(ToolContractCatalog.WorkspaceDotNetNew, StringComparer.Ordinal) &&
                IsDotNetSolutionSetupScaffoldMutationStep(candidate);
            builder.AppendLine($"Missing required step tools: {string.Join(", ", missingRequiredTools)}.");
            if (missingDotNetNewForSetupScaffold)
            {
                builder.AppendLine($"No successful current-run {ToolContractCatalog.WorkspaceDotNetNew} receipt exists for this setup scaffold step.");
                builder.AppendLine($"If the requested solution or project files are absent or have not been proven with current-run workspace_stat_path and workspace_read_file receipts, call {ToolContractCatalog.WorkspaceDotNetNew} against the concrete scaffold target before writing final evidence or calling {AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName}.");
                builder.AppendLine("The prior scaffold contract or markdown summary is not the solution/test change-set artifact and is not a substitute for a scaffold tool receipt.");
                builder.AppendLine("For a .NET solution setup scaffold step, do not rerun workspace_dotnet_new into an already scaffolded target only to satisfy a receipt gate. If workspace_dotnet_new was denied because the solution or project files already exist, inspect the existing .slnx, .sln, and .csproj files with workspace_stat_path and workspace_read_file, write the required current-run managed change-set artifact, and return Completed when the scaffold satisfies the step contract.");
                builder.AppendLine("If the existing scaffold is missing or invalid, create or repair it with workspace_dotnet_new or workspace_pwsh_run_script using ProductMutation before returning a final step outcome.");
                builder.AppendLine("For every other missing required tool, call it against the concrete deliverable or artifact paths before returning a final step outcome.");
            }
            else
            {
                builder.AppendLine("On this retry, call the missing required tools against the concrete deliverable or artifact paths before returning a final step outcome.");
            }

            builder.AppendLine("Do not substitute repeated path polling or summaries for a required validation, browser, inspection, or artifact-write tool.");
        }

        if (unresolvedCriticalToolFailures.Count > 0)
        {
            builder.AppendLine(
                $"Unresolved critical tool failures: {string.Join("; ", unresolvedCriticalToolFailures.Take(2).Select(item => $"{item.ToolName}: {item.ExitSummary}"))}.");
        }

        if (!string.IsNullOrWhiteSpace(incompleteImplementationSummary))
        {
            builder.AppendLine($"Implementation remains incomplete: {incompleteImplementationSummary}.");
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteProofSummary))
        {
            builder.AppendLine($"Browser proof remains incomplete: {missingConcreteProofSummary}.");
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
        {
            builder.AppendLine($"Current-attempt implementation proof is invalid: {missingConcreteImplementationProofSummary}.");
            builder.AppendLine("Reading only markdown evidence, implementation summaries, change-set notes, rollout checklists, or migration notes does not recover an implementation step.");
            builder.AppendLine("If the product root currently contains only those evidence files, treat the root as unimplemented: scaffold or create the concrete deliverable first, validate it, then write the required evidence artifacts.");
        }

        if (!string.IsNullOrWhiteSpace(missingRunnableApplicationProofSummary))
        {
            builder.AppendLine($"Runnable application proof is incomplete: {missingRunnableApplicationProofSummary}.");
        }

        if (!string.IsNullOrWhiteSpace(invalidBrowserProofSummary))
        {
            builder.AppendLine($"Browser proof is invalid: {invalidBrowserProofSummary}.");
        }

        if (!string.IsNullOrWhiteSpace(invalidQualityValidationProofSummary))
        {
            builder.AppendLine($"Validation proof is invalid: {invalidQualityValidationProofSummary}.");
            builder.AppendLine("Rerun the concrete validation command after repair and record command output that proves a warning-free build and nonzero executed tests when tests are part of the acceptance contract.");
        }

        if (!string.IsNullOrWhiteSpace(missingUpstreamArtifactInspectionSummary))
        {
            builder.AppendLine($"Inherited upstream artifact inspection is incomplete: {missingUpstreamArtifactInspectionSummary}.");
            builder.AppendLine("This is a governed inspection receipt gap, not a product defect by itself. On this retry, inspect the exact inherited artifact paths before returning the final step outcome.");
            if (missingUpstreamArtifactInspectionPaths.StatPaths.Count > 0)
            {
                builder.AppendLine($"Use workspace_stat_path on these exact inherited artifact paths now: {FormatPromptPathList(missingUpstreamArtifactInspectionPaths.StatPaths)}.");
            }

            if (missingUpstreamArtifactInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine($"Use workspace_read_file on these exact inherited text artifact paths now: {FormatPromptPathList(missingUpstreamArtifactInspectionPaths.ReadPaths)}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(outOfScopeExternalTargetReferenceSummary))
        {
            builder.AppendLine("Generated evidence referenced stale or ungrounded product paths outside the current grounded product root. Exact stale paths are omitted from this retry prompt to prevent reuse.");
            var allowedExternalTargetAliases = PruneAllowedExternalTargetAliasesForCurrentRun(
                ResolveAllowedExternalTargetAliases(detail.Run));
            if (allowedExternalTargetAliases.Count > 0)
            {
                builder.AppendLine($"Current grounded target root(s): {FormatPromptPathList(allowedExternalTargetAliases)}.");
            }

            builder.AppendLine("On this retry, ignore those stale or ungrounded paths unless the current project structure explicitly grounds them.");
            builder.AppendLine("Use only the current grounded product root and current-run artifacts; do not cite sibling local applications as evidence.");
            builder.AppendLine("Remove stale path lists from final artifacts instead of restating them as `provided context`, `ignored context`, or unrelated source-document notes.");
            if (HasProjectStructureContext(candidate))
            {
                builder.AppendLine("Call project_structure_read now and restate the current grounded target before inspecting, writing, validating, or finalizing evidence.");
            }
        }

        if (!string.IsNullOrWhiteSpace(shallowSharedManagedArtifactReferenceSummary))
        {
            var currentRunManagedArtifactRoot = BuildCurrentRunManagedArtifactRoot(candidate);
            builder.AppendLine($"Generated evidence used shared managed artifact paths that can be overwritten by concurrent runs: {shallowSharedManagedArtifactReferenceSummary}.");
            builder.AppendLine($"On this retry, write new evidence under `{currentRunManagedArtifactRoot}` unless a required artifact input or output gives an exact deeper managed path.");
            builder.AppendLine("Do not read, rewrite, or cite shallow files directly under a shared `artifacts/scopes/<scope>/<id>/`, `output/scopes/<scope>/<id>/`, `integration-map/scopes/<scope>/<id>/`, or `data/scopes/<scope>/<id>/` root as current-run truth.");
        }

        if (!RequiresConcreteImplementationProof(candidate) &&
            candidate.ExpectedArtifacts.Any(item => item.IsRequired))
        {
            builder.AppendLine($"If a prior workspace_write_file call was denied because it targeted source, test, or product files for required evidence, rewrite that evidence under `{BuildCurrentRunManagedArtifactRoot(candidate)}` unless the required artifact contract lists an exact grounded artifact path.");
            builder.AppendLine("Do not convert a product/source path-policy denial into `Blocked` when the required evidence artifact can be produced as a current-run managed artifact.");
        }

        if (recoverableGovernedOutcomeGap &&
            missingRequiredTools.Count == 0 &&
            unresolvedCriticalToolFailures.Count == 0)
        {
            builder.AppendLine("The previous attempt did not provide a valid governed step outcome. Inspect the existing concrete outputs and validation evidence, then return the required governed outcome instead of regenerating unrelated work.");
        }

        if (recoverableFinalizerValidationFailure)
        {
            builder.AppendLine($"{finalizerFailureSummary} On this retry, call `{AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName}` exactly once with a valid ProcessStepOutcomeResult before concluding.");
            builder.AppendLine("Pass exactly one `result` object argument shaped like `{ \"status\": \"Completed\", \"reason\": \"...\", \"evidenceRefs\": [\"artifacts/process-runs/...\"], \"nextActions\": [], \"humanReadableSummaryMarkdown\": \"...\" }`; do not pass scalar `result`, `status`, or `reason` sibling arguments.");
            builder.AppendLine("Do not answer only in prose, markdown, or a JSON snippet; the process source of truth is the required finalizer call.");
        }

        if (recoverableExecutionInterruption)
        {
            builder.AppendLine($"{executionInterruptionSummary} Continue the same process step from durable project structure, artifact, and workspace evidence instead of treating the interruption as a product failure.");
        }

        AppendPromptLines(builder, softwareDeliveryRecoveryGuidance.RecoveryFocusLines);

        builder.AppendLine("Do not stop after inspection, planning, bootstrap confirmation, or a next-steps summary on this retry.");
        builder.AppendLine("Finish the concrete work, rerun every failed or missing required validation successfully, and then write every required durable artifact.");
        builder.AppendLine("Do not repeat the same failed validation command or rewrite the same file with the same content in a loop. Before rerunning validation, inspect the diagnostic source or change/delete files that directly address that diagnostic.");
        AppendPromptLines(builder, softwareDeliveryRecoveryGuidance.FinalCautionLines);

        var governedInspectionPaths = ResolveGovernedInspectionPaths(candidate.ExpectedArtifacts);
        var artifactInputInspectionPaths = ResolveArtifactInputInspectionPaths(candidate.ArtifactInputs);

        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("This retry is still the implementation step. Do not report that implementation or code artifacts are missing before you attempt the bootstrap or scaffold yourself.");
            builder.AppendLine("Bootstrap or repair the concrete deliverable now, inspect the files or artifacts you created, and rerun every required validation tool after the latest mutation before you conclude.");
            builder.AppendLine("If the prior attempt only inspected markdown, notes, checklists, logs, or README markers and then wrote handoff artifacts, it did not attempt implementation. On this retry, attempt the concrete scaffold or product-file mutation before writing final evidence artifacts or returning Blocked.");
            builder.AppendLine("Do not recover by submitting Completed for pre-existing markdown artifacts, even when their titles match required output artifacts. Those files are evidence only until this retry creates or repairs concrete product files.");
            builder.AppendLine("After the final concrete product mutation in this retry, read at least one representative changed source, project, document, workbook, deck, or deliverable file before writing final evidence artifacts or submitting the governed outcome. If you mutate another product file after that read, repeat the read and rerun required validation.");
            builder.AppendLine("If the prior attempt changed product files after a failed validation but did not rerun that validation, inspect the changed file and rerun the failed build, test, run, browser, lint, or validation tool before writing final artifacts. If it still fails, repair the diagnostic and rerun the same validation again.");
            AppendPromptLines(builder, softwareDeliveryRecoveryGuidance.ImplementationGuidanceLines);
            builder.AppendLine("If a required validation failed, rerun that exact validation against the same concrete target after every repair. A later unrelated validation does not recover the failed one by itself.");
            if (HasScaffoldOverwriteConflict(detail, responseText))
            {
                builder.AppendLine("The scaffold command appears to have stopped because files already existed. Reuse the existing scaffold: inspect it, repair it in place, and validate that concrete project instead of rerunning the same scaffold command.");
            }

            if (artifactInputInspectionPaths.StatPaths.Count > 0 || artifactInputInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine("Inspect the inherited durable artifacts directly on this retry instead of relying only on prior summaries or response text.");
                if (artifactInputInspectionPaths.StatPaths.Count > 0)
                {
                    builder.AppendLine($"Use workspace_stat_path on these upstream durable artifact paths now: {FormatPromptPathList(artifactInputInspectionPaths.StatPaths)}.");
                }

                if (artifactInputInspectionPaths.ReadPaths.Count > 0)
                {
                    builder.AppendLine($"Use workspace_read_file on these upstream durable text artifacts now: {FormatPromptPathList(artifactInputInspectionPaths.ReadPaths)}.");
                }
            }

            if (HasProjectStructureContext(candidate))
            {
                builder.AppendLine("Call project_structure_read now, resolve the exact target output directory from the project structure, and honor that path instead of improvising a different location.");
            }
        }

        if (unresolvedCriticalToolFailures.Count > 0)
        {
            builder.AppendLine("If a prior helper process, locked output file, or stale generated artifact is blocking the retry, stop or repair that concrete blocker before rerunning the failed required tool.");
        }

        if (!RequiresConcreteImplementationProof(candidate) &&
            unresolvedCriticalToolFailures.Count > 0)
        {
            AppendPromptLines(builder, softwareDeliveryRecoveryGuidance.ImplementationGuidanceLines);
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            builder.AppendLine("This retry is still the QA/browser-proof step. Inspect the reviewed host project, launch settings, and grounded implementation artifacts before you conclude.");
            if (HasProjectStructureContext(candidate))
            {
                builder.AppendLine("Call project_structure_read now, resolve the exact reviewed host under the grounded product path, and use that concrete app instead of assuming a separate published deployment.");
            }

            builder.AppendLine("Do not assume the app must be reachable at `http://localhost:5000/`. Derive the real launch URL from the reviewed host project, `launchSettings.json`, prior run diagnostics, or the URL reported by the launch command.");
            AppendPromptLines(builder, softwareDeliveryRecoveryGuidance.BrowserGuidanceLines);
        }

        if (missingRequiredTools.Contains("workspace_stat_path", StringComparer.Ordinal) &&
            governedInspectionPaths.StatPaths.Count > 0)
        {
            builder.AppendLine($"Use workspace_stat_path on these exact governed output paths after they exist: {FormatPromptPathList(governedInspectionPaths.StatPaths)}.");
        }
        else if (missingRequiredTools.Contains("workspace_stat_path", StringComparer.Ordinal) &&
                 artifactInputInspectionPaths.StatPaths.Count > 0)
        {
            builder.AppendLine($"Use workspace_stat_path on these exact upstream durable artifact paths now: {FormatPromptPathList(artifactInputInspectionPaths.StatPaths)}.");
        }

        if (missingRequiredTools.Contains("workspace_read_file", StringComparer.Ordinal))
        {
            if (governedInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine($"Use workspace_read_file on these exact governed text artifacts after they exist: {FormatPromptPathList(governedInspectionPaths.ReadPaths)}.");
            }
            else if (artifactInputInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine($"Use workspace_read_file on these exact upstream durable text artifacts now: {FormatPromptPathList(artifactInputInspectionPaths.ReadPaths)}.");
            }
            else if (governedInspectionPaths.StatPaths.Count > 0)
            {
                builder.AppendLine("If the governed outputs are binary-only, read the nearest durable markdown, log, JSON, YAML, or text artifact that explains the governed outputs after you create it.");
            }
            else if (artifactInputInspectionPaths.StatPaths.Count > 0)
            {
                builder.AppendLine("If the upstream artifacts are binary-only, read the nearest durable markdown, log, JSON, YAML, or text artifact that explains them before you conclude this retry.");
            }
        }

        if (RequiresGovernedStepOutcome(candidate.StepRun))
        {
            builder.AppendLine("Do not conclude this governed retry without returning a valid structured ProcessStepOutcomeResult.");
            builder.AppendLine("Use the configured structured output format. Status must be one of Completed, Blocked, Failed, WaitingApproval, or Refused, and Reason must be concrete.");
            builder.AppendLine($"If `{AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName}` is available, call it exactly once with one `result` object; the object must contain `status` and `reason`.");
            builder.AppendLine("Put display-only markdown in HumanReadableSummaryMarkdown. Do not encode the workflow decision in markdown or an HTML comment.");
            if (candidate.RequiresExplicitBranchOutcomeSelection)
            {
                builder.AppendLine("If this retry completes onto a specific downstream branch, set BranchOutcomeKey to the exact branchOutcomeKey from the available branch outcomes.");
            }
        }

        var priorSummary = !string.IsNullOrWhiteSpace(detail.Run.ResultSummary)
            ? detail.Run.ResultSummary
            : responseText;
        if (!string.IsNullOrWhiteSpace(priorSummary))
        {
            var promptSafePriorSummary = RedactUnallowedExternalTargetReferencesForPrompt(
                priorSummary,
                ResolveAllowedExternalTargetAliases(detail.Run));
            builder.Append("Previous run summary: ");
            builder.AppendLine(TruncateForPrompt(promptSafePriorSummary, 400));
        }

        return builder.ToString().Trim();
    }

    private static string RedactUnallowedExternalTargetReferencesForPrompt(
        string text,
        IReadOnlyList<string> allowedAliases)
        => ProcessExternalTargetGroundingService.RedactUnallowedReferencesForPrompt(text, allowedAliases);

}
