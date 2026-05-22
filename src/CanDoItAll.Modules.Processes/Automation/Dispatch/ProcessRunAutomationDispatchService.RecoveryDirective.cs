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
        var missingRunnableApplicationProofSummary = ResolveMissingRunnableApplicationProofSummary(candidate, detail);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);
        var invalidQualityValidationProofSummary = ResolveInvalidQualityValidationProofSummary(
            candidate,
            detail,
            ResolveOutputInspectionText(responseText));
        var implementationMentionsDotNet = ImplementationContractMentionsDotNet(candidate);
        var implementationMentionsJavaScript = ImplementationContractMentionsJavaScript(candidate);
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

        if (!string.IsNullOrWhiteSpace(outOfScopeExternalTargetReferenceSummary))
        {
            builder.AppendLine("Generated evidence referenced stale or ungrounded product paths outside the current grounded product root. Exact stale paths are omitted from this retry prompt to prevent reuse.");
            var allowedExternalTargetAliases = PruneAllowedExternalTargetAliasesForCurrentRun(
                ExecutionInvocationMetadata.ResolveAllowedExternalTargetAliases(detail.Run));
            if (allowedExternalTargetAliases.Count > 0)
            {
                builder.AppendLine($"Current grounded external-target root(s): {FormatPromptPathList(allowedExternalTargetAliases)}.");
            }

            builder.AppendLine("On this retry, ignore those stale or ungrounded paths unless the current project structure explicitly grounds them.");
            builder.AppendLine("Use only the current grounded product root and current-run artifacts; do not cite sibling external-target applications as evidence.");
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

        if (recoverableGovernedOutcomeGap &&
            missingRequiredTools.Count == 0 &&
            unresolvedCriticalToolFailures.Count == 0)
        {
            builder.AppendLine("The previous attempt did not provide a valid governed step outcome. Inspect the existing concrete outputs and validation evidence, then return the required governed outcome instead of regenerating unrelated work.");
        }

        if (recoverableFinalizerValidationFailure)
        {
            builder.AppendLine($"{finalizerFailureSummary} On this retry, call `{AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName}` exactly once with a valid ProcessStepOutcomeResult before concluding.");
            builder.AppendLine("Do not answer only in prose, markdown, or a JSON snippet; the process source of truth is the required finalizer call.");
        }

        if (recoverableExecutionInterruption)
        {
            builder.AppendLine($"{executionInterruptionSummary} Continue the same process step from durable project structure, artifact, and workspace evidence instead of treating the interruption as a product failure.");
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
        builder.AppendLine("Do not recover by writing fake package, framework, runtime, browser, or test-tool shim types. Fix the real dependency or project reference, or return Blocked with a concrete environment/dependency blocker.");

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
            builder.AppendLine("When the requested deliverable is an application or service, produce a runnable host/project, not only libraries, loose files, or static fragments.");
            builder.AppendLine("For Blazor compile failures around `@bind`, inspect the bound model types: inputs require settable properties or explicit get/set wrappers. Do not keep positional records or init-only properties as bound form state.");
            if (!string.IsNullOrWhiteSpace(missingRunnableApplicationProofSummary))
            {
                builder.AppendLine("This retry must start the concrete runnable host after the latest implementation changes. For .NET hosts, use workspace_dotnet_run against the host project so startup URL, process id, stdout log, stderr log, and receipt evidence are recorded; for other stacks, use the matching launch tool with equivalent evidence.");
            }

            builder.AppendLine("If a required validation failed, rerun that exact validation against the same concrete target after every repair. A later unrelated validation does not recover the failed one by itself.");
            builder.AppendLine("If a .NET startup smoke failed after build/test passed, inspect the captured stdout/stderr or startup receipt and repair the concrete runtime cause before returning Blocked. Common repair targets include missing dependency-injection registrations, `Program.cs` service wiring, routing, appsettings, launch settings, static assets, and startup initialization.");
            if (HasScaffoldOverwriteConflict(detail, responseText))
            {
                builder.AppendLine("The scaffold command appears to have stopped because files already existed. Reuse the existing scaffold: inspect it, repair it in place, and validate that concrete project instead of rerunning the same scaffold command.");
            }

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
            if (implementationMentionsDotNet)
            {
                builder.AppendLine("If the app is not already running, start the reviewed .NET host yourself before opening the browser. Prefer `workspace_dotnet_run` with `keepAlive: true` so the retry records URL, process id, stdout log, stderr log, and startup receipt evidence while Playwright can reach the app; after browser evidence is captured, stop it with the recorded `startup.json` `stopCommand` before finalizing.");
                builder.AppendLine("For external targets, keep `external-target/<drive>/...` with workspace file and run tools. Do not write a one-off path-translation launch helper when a reviewed generic .NET launch tool is available; missing launch-tool access is a platform blocker to report explicitly.");
            }
            else if (implementationMentionsJavaScript)
            {
                var currentRunManagedArtifactRoot = BuildCurrentRunManagedArtifactRoot(candidate);
                builder.AppendLine("If the app is not already running, start the reviewed JavaScript or TypeScript host yourself before opening the browser. Use the reviewed package script, launch tool, or task-specific skill that records URL, logs, exit code, and cleanup evidence.");
                builder.AppendLine("Do not call `workspace_dotnet_build`, `workspace_dotnet_test`, or `workspace_dotnet_run` for JavaScript or TypeScript deliverables unless the current-run requirements explicitly name .NET, C#, ASP.NET, Blazor, Razor, `.csproj`, or `.sln`.");
                builder.AppendLine($"If only `workspace_pwsh_run_script` is available, first write a helper script under `{currentRunManagedArtifactRoot}` with `workspace_write_file`, then stat or read it before running it. Do not invoke an artifact helper path that has not been created in this current run.");
                builder.AppendLine("If `workspace_pwsh_run_script` is listed as a missing required tool, do not block from file inspection alone. Create or repair the launch helper and call `workspace_pwsh_run_script`; a missing localhost URL is a valid blocker only after the launch helper ran and its captured diagnostics show no reachable URL.");
                builder.AppendLine("If the helper writes another PowerShell script, use a single-quoted here-string (`@' ... '@`) or escape every literal `$` in the nested script, then read the generated child script before executing it. If the child script contains stripped variables such as `param([string] = ...)`, `.Start()`, or `.OutputStream.Write(,...)`, repair the quoting before rerunning.");
                builder.AppendLine("If the helper starts a static server, package preview, `HttpListener`, `python -m http.server`, or similar long-running browser host, it must start that host as a background child process, wait for a reachable URL, record the URL and process id, and exit. Do not run the foreground server loop inside `workspace_pwsh_run_script` until timeout.");
                builder.AppendLine("Do not pass a server implementation script itself to `workspace_pwsh_run_script` when its main body constructs `HttpListener`, calls `GetContext`, runs `python -m http.server`, or contains a request loop. Execute only a bounded launcher script that starts that server as a child process and exits after reachability proof.");
                builder.AppendLine("Do not call blocking stream reads such as `.ReadToEnd()`, `.ReadToEndAsync().Result`, `.WaitForExit()`, or equivalent waits on redirected stdout/stderr for a long-running browser host. Redirect output to files, inherit handles, or use nonblocking event handlers so the helper can return after startup evidence.");
                builder.AppendLine("For external targets, keep `external-target/<drive>/...` with workspace file tools. Convert that alias to a native path inside the controlled helper script before calling native commands such as `Resolve-Path`, `Test-Path`, `Set-Location`, `Start-Process`, `cmd.exe`, `node`, `npm`, `python`, or a static-file launcher.");
                builder.AppendLine("On Windows, package-script helpers must launch npm through `npm.cmd` or `cmd.exe /d /s /c \"npm run <script>\"`; do not call `Start-Process -FilePath 'npm'`. If the previous helper failed with `%1 is not a valid Win32 application`, rewrite the helper to use `npm.cmd` or `cmd.exe` and rerun browser launch proof.");
                builder.AppendLine("Never call native PowerShell or process APIs with `external-target/...` directly. In a helper, translate `external-target/C/programovani/app` to `C:\\programovani\\app` before `Resolve-Path`, `Set-Location`, package scripts, or launch commands.");
            }
            else
            {
                var currentRunManagedArtifactRoot = BuildCurrentRunManagedArtifactRoot(candidate);
                builder.AppendLine("If the app is not already running, inspect current-run files, launch settings, package scripts, or upstream artifacts to identify the reviewed host stack before opening the browser. Use the launch tool or task-specific skill that matches that evidence and records URL, logs, exit code, and cleanup evidence.");
                builder.AppendLine($"If only `workspace_pwsh_run_script` is available, first write a helper script under `{currentRunManagedArtifactRoot}` with `workspace_write_file`, then stat or read it before running it. Do not invoke an artifact helper path that has not been created in this current run.");
                builder.AppendLine("For external targets, keep `external-target/<drive>/...` with workspace file tools. Convert that alias to a native path only inside a controlled helper script when the reviewed native command requires it.");
            }
            builder.AppendLine("Do not repeat successful unchanged validations while browser proof is missing. Launch plus browser evidence is the recovery path.");
            builder.AppendLine("Use the UI as an end user would: navigate to the delivered entry point, fill or change representative controls, trigger representative actions, and verify the visible result changes.");
            builder.AppendLine("For canvas, game, custom-control, or keyboard-first surfaces, use `browser_evaluate` when ordinary click/fill helpers cannot express the workflow; dispatch representative keyboard or pointer events and inspect visible state, DOM text, or client-side storage.");
            builder.AppendLine("If this is a static browser deliverable and the current step contract requires runtime or browser proof but does not explicitly require automated tests, a package manifest, or a nonzero test count, do not block solely because `package.json` or automated tests are absent. Record the missing automation as quality risk and rely on fresh browser/runtime proof for the retry disposition.");
            builder.AppendLine("Capture fresh bounded browser evidence with `browser_take_screenshot`, `browser_snapshot`, and `browser_console_messages` before you conclude this retry. Use `browser_snapshot` depth 2 with boxes disabled unless the contract requires depth 3 or 4, and call `browser_take_screenshot` with fullPage false or no fullPage argument. Do not retry a policy-denied browser call with the same arguments.");
            builder.AppendLine("Browser screenshots, snapshots, console logs, and state outputs must be process-visible current-run artifacts. Cite the returned filenames in the durable evidence artifact so the process can import them and expose the output folder through linked project structure.");
            builder.AppendLine("Provider-native browser files are written before managed scope aliasing. Inspect the browser tool result itself and cite the returned filename; do not block only because `workspace_read_file` cannot read a provider-native `artifacts/process-runs/...` browser file.");
            builder.AppendLine("Inspect the bounded `browser_snapshot` output before concluding. If it still shows starter-template, placeholder, irrelevant content, or non-interactive behavior instead of the requested product behavior, treat it as a routing, rendering, static-content, or client-interaction defect and repair or block instead of returning Completed.");
            builder.AppendLine("If a screenshot call fails, retry once with viewport capture. If bounded snapshot, console diagnostics, and visible-state checks prove the workflow and no exact screenshot artifact is required, do not block solely on the screenshot failure.");
            builder.AppendLine("If `browser_snapshot` fails because of a tool-side selector, parsing, or accessibility-tree issue after navigation, screenshot, console diagnostics, and representative visible-state checks succeeded, replace it with `browser_evaluate` DOM or state proof and cite the snapshot failure. Do not block solely on the missing snapshot artifact unless the step explicitly requires that exact artifact.");
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
            var promptSafePriorSummary = RedactUnallowedExternalTargetReferencesForPrompt(
                priorSummary,
                ExecutionInvocationMetadata.ResolveAllowedExternalTargetAliases(detail.Run));
            builder.Append("Previous run summary: ");
            builder.AppendLine(TruncateForPrompt(promptSafePriorSummary, 400));
        }

        return builder.ToString().Trim();
    }

    private static string RedactUnallowedExternalTargetReferencesForPrompt(
        string text,
        IReadOnlyList<string> allowedAliases)
    {
        if (string.IsNullOrWhiteSpace(text) || allowedAliases.Count == 0)
        {
            return text;
        }

        var normalizedAllowedAliases = PruneAllowedExternalTargetAliasesForCurrentRun(allowedAliases);
        if (normalizedAllowedAliases.Count == 0)
        {
            return text;
        }

        return WorkspacePathInToolRequestRegex.Replace(
            text,
            match =>
            {
                var rawPath = match.Groups["path"].Value;
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    return rawPath;
                }

                var referencedAlias = rawPath.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                                      rawPath.StartsWith(ExternalTargetAliasRoot + "\\", StringComparison.OrdinalIgnoreCase)
                    ? NormalizeExternalTargetAlias(rawPath)
                    : TryMapAbsoluteExternalPathToAlias(rawPath, out var mappedAlias)
                        ? mappedAlias
                        : string.Empty;
                if (string.IsNullOrWhiteSpace(referencedAlias) ||
                    IsAllowedExternalTargetReference(referencedAlias, normalizedAllowedAliases) ||
                    IsDocumentedScaffoldParentReference(text, match.Index, referencedAlias, normalizedAllowedAliases))
                {
                    return rawPath;
                }

                return "[stale external-target path omitted]";
            });
    }

    private static bool HasScaffoldOverwriteConflict(
        ExecutionRunDetail detail,
        string? responseText)
    {
        if (ContainsScaffoldOverwriteConflictSignal(responseText))
        {
            return true;
        }

        return detail.ToolReceipts.Any(receipt =>
            string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_new", StringComparison.Ordinal) &&
            (ContainsScaffoldOverwriteConflictSignal(receipt.ExitSummary) ||
             ContainsScaffoldOverwriteConflictSignal(receipt.RequestSummary)));
    }

    private static bool ContainsScaffoldOverwriteConflictSignal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("overwrite conflict", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("files already exist", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("files already existed", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("would overwrite", StringComparison.OrdinalIgnoreCase);
    }

}
