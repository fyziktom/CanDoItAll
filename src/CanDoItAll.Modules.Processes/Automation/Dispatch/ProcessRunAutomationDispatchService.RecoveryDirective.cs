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
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string BuildRecoveryDirective(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        int attemptNumber)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Attempt {attemptNumber} ended before the step was actually complete.");
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, responseText);
        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, responseText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);

        if (missingRequiredTools.Count > 0)
        {
            builder.AppendLine($"Missing required step tools: {string.Join(", ", missingRequiredTools)}.");
            builder.AppendLine("On this retry, call the missing required tools against the concrete deliverable or artifact paths before returning a final step outcome.");
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
        }

        if (!string.IsNullOrWhiteSpace(invalidBrowserProofSummary))
        {
            builder.AppendLine($"Browser proof is invalid: {invalidBrowserProofSummary}.");
        }

        var domainRecoveryFocusGuidance = BuildDomainRecoveryFocusGuidance(
            candidate,
            detail,
            responseText,
            missingConcreteImplementationProofSummary,
            missingRequiredTools,
            unresolvedCriticalToolFailures);
        if (!string.IsNullOrWhiteSpace(domainRecoveryFocusGuidance))
        {
            builder.AppendLine(domainRecoveryFocusGuidance);
        }

        builder.AppendLine("Do not stop after inspection, planning, bootstrap confirmation, or a next-steps summary on this retry.");
        builder.AppendLine("Finish the concrete work, rerun every failed or missing required validation successfully, and then write every required durable artifact.");
        builder.AppendLine("Do not repeat the same failed validation command or rewrite the same file with the same content in a loop. Before rerunning validation, inspect the diagnostic source or change/delete files that directly address that diagnostic.");

        var governedInspectionPaths = ResolveGovernedInspectionPaths(candidate.ExpectedArtifacts);
        var artifactInputInspectionPaths = ResolveArtifactInputInspectionPaths(candidate.ArtifactInputs);

        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("This retry is still the implementation step. Do not report that implementation or code artifacts are missing before you attempt the bootstrap or scaffold yourself.");
            builder.AppendLine("Bootstrap or repair the concrete deliverable now, inspect the files or artifacts you created, and rerun every required validation tool after the latest mutation before you conclude.");
            builder.AppendLine("If a required validation failed, rerun that exact validation against the same concrete target after every repair. A later unrelated validation does not recover the failed one by itself.");
            AppendDomainImplementationRecoveryGuidance(
                builder,
                candidate,
                detail,
                responseText,
                missingConcreteImplementationProofSummary,
                missingRequiredTools,
                unresolvedCriticalToolFailures);

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
                builder.AppendLine("If the resolved target directory is outside the managed workspace, map it to the workspace alias format `external-target/<drive>/...` for workspace tools.");
                builder.AppendLine("Create or repair the exact mapped external-target deliverable now. Use workspace_pwsh_run_script only when you need a controlled helper command for the real external target.");
            }
        }

        if (unresolvedCriticalToolFailures.Count > 0)
        {
            builder.AppendLine("If a prior helper process, locked output file, or stale generated artifact is blocking the retry, stop or repair that concrete blocker before rerunning the failed required tool.");
        }

        if (!RequiresConcreteImplementationProof(candidate) &&
            unresolvedCriticalToolFailures.Count > 0)
        {
            AppendDomainImplementationRecoveryGuidance(
                builder,
                candidate,
                detail,
                responseText,
                missingConcreteImplementationProofSummary,
                missingRequiredTools,
                unresolvedCriticalToolFailures);
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            builder.AppendLine("This retry is still the QA/browser-proof step. Inspect the reviewed host project, launch settings, and grounded implementation artifacts before you conclude.");
            if (HasProjectStructureContext(candidate))
            {
                builder.AppendLine("Call project_structure_read now, resolve the exact reviewed host under the grounded external-target path, and use that concrete app instead of assuming a separate published deployment.");
            }

            builder.AppendLine("Do not assume the app must be reachable at `http://localhost:5000/`. Derive the real launch URL from the reviewed host project, `launchSettings.json`, prior run diagnostics, or the URL reported by the launch command.");
            builder.AppendLine("If the app is not already running, start the reviewed host yourself before opening the browser. Use the launch tool or task-specific skill that matches the delivered technology, and record URL, process, stdout, and stderr evidence.");
            builder.AppendLine("When repairing a launch helper for an external target, keep `external-target/<drive>/...` for workspace tools, but convert it to the native OS path inside helper content before invoking native commands. A relative `external-target/...` string can resolve under the managed workspace path alias and fail even after prior validation succeeded.");
            builder.AppendLine("Do not assign to `$PID` in the PowerShell helper; use `$appProcess` and `$appProcessId`. If a helper already exists, inspect and repair it instead of rewriting the same broken content.");
            builder.AppendLine("Do not repeat successful unchanged validations while browser proof is missing. Launch plus browser evidence is the recovery path.");
            builder.AppendLine("Capture fresh browser evidence with `browser_take_screenshot`, `browser_snapshot`, and `browser_console_messages` before you conclude this retry.");
            builder.AppendLine("Inspect the saved `browser_snapshot` output before concluding. If it still shows starter-template, placeholder, or irrelevant content instead of the requested product behavior, repair or block instead of returning Completed.");
            AppendDomainBrowserRecoveryGuidance(
                builder,
                candidate,
                detail,
                responseText,
                missingConcreteImplementationProofSummary,
                missingRequiredTools,
                unresolvedCriticalToolFailures);
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
            builder.Append("Previous run summary: ");
            builder.AppendLine(TruncateForPrompt(priorSummary, 400));
        }

        return builder.ToString().Trim();
    }

}
