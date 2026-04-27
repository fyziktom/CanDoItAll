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
        }

        if (missingRequiredTools.Contains("workspace_dotnet_build", StringComparer.Ordinal))
        {
            builder.AppendLine("The previous attempt failed because it never invoked `workspace_dotnet_build`.");
            builder.AppendLine("On this retry, after you know the concrete solution or project path, call `workspace_dotnet_build` directly against that path before any final answer.");
            builder.AppendLine("Do not poll `bin/`, `obj/`, DLL, PDB, or test-output paths as a replacement for `workspace_dotnet_build`; repeated successful stat results are not validation progress.");
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

        var calculatorRecoveryFocusGuidance = BuildCalculatorRecoveryFocusGuidance(
            candidate,
            responseText,
            missingConcreteImplementationProofSummary,
            missingRequiredTools,
            unresolvedCriticalToolFailures);
        if (!string.IsNullOrWhiteSpace(calculatorRecoveryFocusGuidance))
        {
            builder.AppendLine(calculatorRecoveryFocusGuidance);
        }

        builder.AppendLine("Do not stop after inspection, planning, bootstrap confirmation, or a next-steps summary on this retry.");
        builder.AppendLine("Finish the concrete work, rerun every failed or missing required validation successfully, and then write every required durable artifact.");
        builder.AppendLine("Do not repeat the same failed validation command or rewrite the same file with the same content in a loop. Before rerunning validation, inspect the diagnostic source or change/delete files that directly address that diagnostic.");

        var governedInspectionPaths = ResolveGovernedInspectionPaths(candidate.ExpectedArtifacts);
        var artifactInputInspectionPaths = ResolveArtifactInputInspectionPaths(candidate.ArtifactInputs);

        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("This retry is still the implementation step. Do not report that implementation or code artifacts are missing before you attempt the bootstrap or scaffold yourself.");
            builder.AppendLine("Bootstrap the runnable solution or project now, then validate the concrete files you created with workspace_stat_path, workspace_read_file, and workspace_dotnet_build before you conclude.");
            builder.AppendLine("If the scaffold is greenfield, create the actual solution and project files now with workspace_dotnet_new or a controlled helper path instead of writing only a source file set.");
            builder.AppendLine("If the host or sibling test project already exists from an earlier attempt, do not call workspace_dotnet_new again with --force. Inspect and repair the existing scaffold in place; a forced re-scaffold can erase the implemented Components/Pages route and reset the app to Hello, world.");
            builder.AppendLine("Do not recover by deleting scaffold core files one by one. Preserve and edit `.csproj`, `Program.cs`, `Components/App.razor`, `Components/Routes.razor`, `_Imports.razor`, `Components/Pages/Home.razor`, layout files, `appsettings*.json`, and `wwwroot/app.css`.");
            builder.AppendLine("If you retry a greenfield .NET bootstrap with workspace_dotnet_new, explicitly request a supported target framework such as `net10.0` instead of accepting an older template default.");
            builder.AppendLine("If a prior workspace_dotnet_new attempt failed because files already existed or the template wanted to overwrite content, inspect the target directory immediately. When the scaffold is already present at the required path, continue by repairing, reading, and building that existing project in place instead of declaring the retry blocked.");
            builder.AppendLine("If this implementation produces browser-facing UI files such as `.razor`, `.cshtml`, or `wwwroot` assets, leave a runnable web host and startup entrypoint in place for downstream QA. Do not stop at a plain class library.");
            builder.AppendLine("If the project structure names Blazor SSR, repair toward a runnable Blazor SSR app instead of MVC, Razor Pages, or controller/view placeholders.");
            builder.AppendLine("Keep test projects outside the Blazor host folder. If a previous attempt left `*.Tests` folders or test files nested under the host project, use `workspace_delete_path` with `recursive: true` on that stale nested test folder before rerunning the host build.");
            builder.AppendLine("Do not recreate nested test files under the host after deleting them. For a host at `external-target/.../Calculator/Calculator.csproj`, test files belong in the sibling `external-target/.../Calculator.Tests/...` project, not in `external-target/.../Calculator/Calculator.Tests/...`.");
            builder.AppendLine("If `workspace_dotnet_test` was denied because the sibling test project is missing, create or repair the sibling test project and ProjectReference before rerunning the identical test command.");
            builder.AppendLine("If the failed validation was `workspace_dotnet_build`, rerun `workspace_dotnet_build` against the exact failed host project after every repair. A later `workspace_dotnet_test` success does not recover that failed build by itself.");
            builder.AppendLine("Call `workspace_dotnet_test` only against a test `.csproj`, `.sln`, or `.slnx`. A `.cs` test source file or plain test directory is an invalid target; repair or create the sibling test project and use its `.csproj` path.");
            builder.AppendLine("If the build error mentions missing xUnit, MSTest, `Fact`, or test attribute namespaces in the host project, treat that as misplaced test code under the host and fix the file layout, not the production host dependencies.");
            builder.AppendLine("If tests fail with `CS0118` or because a project/root namespace is being used like a type, create or inspect the concrete domain/application type first, such as `<RootNamespace>.Domain.CalculatorEngine`, add the test ProjectReference, update the tests to target that type, and then rerun workspace_dotnet_build and workspace_dotnet_test.");
            builder.AppendLine("If tests compile against a host-domain type but cannot resolve it, edit the sibling test project file to add `<ProjectReference Include=\"..\\<HostProject>\\<HostProject>.csproj\" />`; do not try to solve that by adding packages or rewriting only the test source.");
            builder.AppendLine("For calculator-like apps, write and read `Calculator/Domain/CalculatorEngine.cs`, add a sibling test project ProjectReference to the host, and replace empty template tests with assertions against `CalculatorEngine` operations before rerunning validation.");
            builder.AppendLine("If the build error mentions `_Imports.razor` and `CS0138` because the root name is a type instead of a namespace, rename the conflicting domain type to a non-root name such as `CalculatorEngine` under a concrete namespace such as `<RootNamespace>.Domain`, then update `_Imports.razor` to import that namespace or remove the bad root import.");
            builder.AppendLine("If the conflicting type comes from a Razor component file such as `Components/Calculator.razor` in a project/root namespace named `Calculator`, rename that component to `CalculatorPage.razor` or move its routed content into `Components/Pages/Home.razor`; a `.razor` file name is also a generated type name.");
            builder.AppendLine("For Blazor Web App scaffolds, do not create legacy root `Pages/*.razor` routes. Use `Components/Pages/Home.razor` for `/`, and delete any stale root `Pages/Home.razor`, `Pages/Index.razor`, or other root `Pages/*.razor` route that duplicates or replaces `Components/Pages/Home.razor` before rerunning build or launch validation.");
            builder.AppendLine("Never put `@page` in `Components/Routes.razor`; if it is present there, remove it and keep `Routes.razor` as the Router-only host before rerunning build or tests.");
            builder.AppendLine("Do not repair `_Imports.razor` by repeatedly rebuilding. Change the conflicting file/type first, then rerun the exact failed host build.");
            builder.AppendLine("If you renamed or deleted `MainLayout`, either restore `MainLayout.razor` or update every `MainLayout` reference before building, including `Routes.razor`, `NotFound.razor`, and any `_Imports.razor` layout namespace.");
            builder.AppendLine("Do not stop at a starter template or say the app is merely ready for later feature implementation. Replace default template output with the requested product behavior before you conclude.");
            builder.AppendLine("On this retry, repair placeholder or incomplete product files before validating. A validation-only retry is acceptable only when read-back proves the current concrete source already satisfies the full implementation contract, then build and tests pass without any later mutation.");
            if (RequiresCalculatorLikeImplementationProof(candidate, detail))
            {
                AppendCalculatorRecoveryChecklist(builder, missingConcreteImplementationProofSummary);
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
                builder.AppendLine("If the resolved target directory is outside the managed workspace, map it to the workspace alias format `external-target/<drive>/...` for workspace file and dotnet tools.");
                builder.AppendLine("Scaffold, inspect, and build the exact mapped external-target project now. Use workspace_pwsh_run_script only when you need a controlled helper command for the real external target.");
            }
        }

        if (unresolvedCriticalToolFailures.Any(item =>
                string.Equals(NormalizeToolToken(item.ToolName), "workspace_dotnet_build", StringComparison.Ordinal) ||
                string.Equals(NormalizeToolToken(item.ToolName), "workspace_pwsh_run_script", StringComparison.Ordinal)))
        {
            builder.AppendLine("If a prior runtime host, launch script, or locked output file is blocking the build or launch retry, stop the prior host before rerunning validation. Use any provided stop script or recorded PID file when the workspace includes one.");
        }

        var misplacedTestProjectRecoveryGuidance = BuildMisplacedTestProjectRecoveryGuidance(unresolvedCriticalToolFailures);
        if (!string.IsNullOrWhiteSpace(misplacedTestProjectRecoveryGuidance))
        {
            builder.AppendLine(misplacedTestProjectRecoveryGuidance);
        }

        var blazorBuildRecoveryGuidance = BuildBlazorBuildRecoveryGuidance(
            candidate,
            unresolvedCriticalToolFailures,
            responseText);
        if (!string.IsNullOrWhiteSpace(blazorBuildRecoveryGuidance))
        {
            builder.AppendLine(blazorBuildRecoveryGuidance);
        }

        var frameworkRecoveryGuidance = BuildDotnetFrameworkRecoveryGuidance(
            candidate,
            unresolvedCriticalToolFailures,
            responseText);
        if (!string.IsNullOrWhiteSpace(frameworkRecoveryGuidance))
        {
            builder.AppendLine(frameworkRecoveryGuidance);
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            builder.AppendLine("This retry is still the QA/browser-proof step. Inspect the reviewed host project, launch settings, and grounded implementation artifacts before you conclude.");
            if (HasProjectStructureContext(candidate))
            {
                builder.AppendLine("Call project_structure_read now, resolve the exact reviewed host under the grounded external-target path, and use that concrete app instead of assuming a separate published deployment.");
            }

            builder.AppendLine("Do not assume the app must be reachable at `http://localhost:5000/`. Derive the real launch URL from the reviewed host project, `launchSettings.json`, prior run diagnostics, or the URL reported by the launch command.");
            builder.AppendLine("If the app is not already running, start the reviewed host yourself before opening the browser. If `workspace_dotnet_run` is not available, write or repair a short PowerShell helper that starts `dotnet run --no-build --project <reviewed .csproj> --urls http://127.0.0.1:<free-port>` in the background, waits for a successful HTTP response, writes appProcessId/URL/stdout/stderr log-path evidence, and exits nonzero on 4xx/5xx or early process exit.");
            builder.AppendLine("When repairing a launch helper for an external target, keep `external-target/<drive>/...` for workspace tools, but convert it to the native Windows path inside the helper before invoking `dotnet`, `Start-Process`, `Test-Path`, or `Resolve-Path`. For example, `external-target/C/programovani/app/App.csproj` must become `C:\\programovani\\app\\App.csproj`; a relative `external-target/...` string can resolve under the managed workspace path alias and fail even after a successful build.");
            builder.AppendLine("Do not assign to `$PID` in the PowerShell helper; use `$appProcess` and `$appProcessId`. If a helper already exists, inspect and repair it instead of rewriting the same broken content.");
            builder.AppendLine("If the launched Blazor app returns HTTP 500, inspect the captured logs and route files. For Blazor Web App scaffolds, remove duplicate primary routes such as legacy root `Pages/Home.razor` or `Pages/Index.razor` when `Components/Pages/Home.razor` already declares `@page \"/\"`.");
            builder.AppendLine("Do not repeat a successful `workspace_dotnet_build` or `workspace_dotnet_test` receipt for the same unchanged project while browser proof is missing. Repeating build/test is not recovery; app launch plus Playwright evidence is the recovery path.");
            builder.AppendLine("Capture fresh browser evidence with `browser_take_screenshot`, `browser_snapshot`, and `browser_console_messages` before you conclude this retry.");
            builder.AppendLine("Inspect the saved `browser_snapshot` output before concluding. If it still contains starter template text such as `Hello, world!` or `Welcome to your new app.`, repair or block instead of returning Completed.");
            builder.AppendLine("For button-driven Blazor apps such as calculators, click a representative sequence and assert that the visible display or history changes to the expected result. If `@onclick` buttons do not mutate state in the browser, block with a Blazor render-mode or static-SSR implementation defect.");
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
