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
            builder.AppendLine("- Workspace file and dotnet tools cannot use a raw absolute external path like `C:\\target\\app` directly. Convert it to the mapped alias `external-target/C/target/app` when you call `workspace_create_directory`, `workspace_write_file`, `workspace_read_file`, `workspace_stat_path`, `workspace_dotnet_new`, or `workspace_dotnet_build`.");
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

        if (ShouldIncludeBlazorWebAppHostingContract(
                candidate,
                projectStructureGroundingSummary,
                artifactInspectionGroundingSummary))
        {
            builder.AppendLine("Blazor Web App hosting contract:");
            AppendBlazorWebAppHostingContract(builder);
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
            builder.AppendLine("- Because this is an implementation step, create the real scaffold or code now. A markdown change set alone is not a completed implementation.");
            builder.AppendLine("- Follow this implementation critical path: scaffold or inspect the runnable host, identify the generated project shape, create the real domain/application logic, wire the UI to that logic, create sibling automated tests with a ProjectReference to the host, replace stale template content, then build the host and run the tests.");
            builder.AppendLine("- Use `workspace_dotnet_new` only for the first bootstrap when the target project is missing. If a runnable host or test project already exists, inspect and repair it in place; do not rerun `workspace_dotnet_new --force` because it can overwrite the implemented route back to starter template content.");
            builder.AppendLine("- If a previous attempt already created a stock scaffold, treat that scaffold as the host to repair. Do not delete it or scaffold again; edit concrete source/project files such as `Components/Pages/Home.razor`, `Domain/CalculatorEngine.cs`, the sibling test `.csproj`, and test source.");
            builder.AppendLine("- When the project structure gives an exact product output root, treat that directory as the outer container. If the host project name is `Calculator`, scaffold with parentDirectory set to the exact output root and name `Calculator`, producing `<output-root>/Calculator/Calculator.csproj`; do not scaffold into the output root itself.");
            builder.AppendLine("- After `workspace_dotnet_new -n <Name>` under an output root, the canonical host is usually `<output-root>/<Name>/<Name>.csproj`. Do not create `<output-root>/<Name>.csproj`, `<output-root>/Program.cs`, or root `Pages/*.razor` files beside that nested host; target the actual scaffolded project path.");
            builder.AppendLine("- Do not use `workspace_delete_path` recursively on a directory that contains a `.csproj`, `.fsproj`, `.vbproj`, `.sln`, or `.slnx` just to make `workspace_dotnet_new` succeed. Repair the project in place.");
            builder.AppendLine("- Do not delete scaffold core files such as `.csproj`, `Program.cs`, `Components/App.razor`, `Components/Routes.razor`, `_Imports.razor`, `Components/Pages/Home.razor`, layout files, `appsettings*.json`, or `wwwroot/app.css`. Edit or overwrite those files instead.");
            builder.AppendLine("- After `workspace_dotnet_new`, do not guess framework-era file locations. Inspect the scaffolded `.csproj`, `Program.cs`, and routed page paths before writing UI or tests.");
            builder.AppendLine("- Do not write implementation change-set or rollout artifacts until after concrete source/project mutations and successful build/test validation in the same attempt.");
            builder.AppendLine("- For a calculator-like Blazor app, the minimum concrete implementation is a public domain/application type such as `CalculatorEngine`, a non-placeholder routed UI that calls it, and tests that instantiate that concrete type through a sibling test project.");
            builder.AppendLine("- For a calculator-like app, write and then read `Calculator/Domain/CalculatorEngine.cs`, include concrete Add/Subtract/Multiply/Divide operations there, wire the routed page to that engine, and test that engine directly.");
            builder.AppendLine("- Do not leave the generated empty `UnitTest1.cs` as the test evidence. Replace it or add meaningful test source that asserts CalculatorEngine addition, subtraction, multiplication, division, and division-by-zero behavior.");
            builder.AppendLine("- If tests fail with `CS0118` or text like `'Calculator' is a namespace but is used like a type`, do not rerun the same tests. Create or read the concrete engine type, add the test ProjectReference, update tests to `new CalculatorEngine()`, and rerun the host build plus test project.");
            builder.AppendLine("- Concrete feature and constraint nodes from the live project structure are required scope for this implementation step. Treat them as mandatory deliverables now, not as later backlog or rollout notes.");
            builder.AppendLine("- Do not defer grounded features, UI behavior, acceptance notes, or output constraints into `future steps`, follow-up work, or QA-only cleanup while still returning `Completed`.");
            builder.AppendLine("- Before you conclude this implementation step, use `workspace_stat_path` on the concrete solution, project, and source paths you created or changed, and use `workspace_read_file` on at least one concrete project or source file from that implementation.");
            builder.AppendLine("- Run `workspace_dotnet_build` against the implemented solution or project before you claim the scaffold or code is build-ready.");
            builder.AppendLine("- Build/read proof must happen after the last scaffold or source mutation in the same attempt. Previous attempt receipts do not prove the current mutated output.");
            builder.AppendLine("- Keep automated test projects as siblings of the runnable web host, not inside the host project directory. If a `*.Tests` folder or test source file is nested under the Blazor host, use `workspace_delete_path` with `recursive: true` on that stale nested test folder before building the host.");
            builder.AppendLine("- Never create or write test project files under the runnable web host. For a host at `external-target/.../Calculator/Calculator.csproj`, the sibling test project path is `external-target/.../Calculator.Tests/...`; `external-target/.../Calculator/Calculator.Tests/...` is invalid and must be deleted before the host build.");
            builder.AppendLine("- If `workspace_dotnet_test` is denied or fails because the sibling test project path does not exist, create or repair that sibling test project first, add a ProjectReference to the host project, and only then rerun `workspace_dotnet_test`. Repeating the same missing-path test command is not recovery.");
            builder.AppendLine("- `workspace_dotnet_test` targetPath must be a solution or test project file such as `Calculator.Tests/Calculator.Tests.csproj`. Never pass a `.cs` source file or a plain test directory as the target.");
            builder.AppendLine("- When repairing a scaffolded test project, clean stale template and duplicate test files before rerunning tests. Delete or replace files such as `UnitTest1.cs`, `<Project>.Tests.cs`, old `.bak` sources that are still compiled, or duplicate `CalculatorTests` classes instead of repeatedly rewriting only one new test file.");
            builder.AppendLine("- After creating, moving, or repairing tests, rerun `workspace_dotnet_build` against the runnable web host and `workspace_dotnet_test` against the test project. A successful test run does not recover an earlier failed host build unless the same host build is rerun successfully.");
            builder.AppendLine("- If `workspace_dotnet_build` reports missing xUnit, MSTest, or test attribute namespaces from the web project, inspect for misplaced test files under the host and remove or move them; do not fix that by adding test packages to the production web project.");
            builder.AppendLine("- Put business logic that needs automated coverage in a public domain or application class and test that class through a sibling test project with a ProjectReference to the host. For calculator-like tasks, use a concrete type such as `CalculatorEngine` under `<RootNamespace>.Domain`; do not instantiate the project namespace, root namespace, or a Razor component as if it were the calculator engine.");
            builder.AppendLine("- When tests use host-domain types, edit the sibling test `.csproj` to include a real `<ProjectReference Include=\"..\\<HostProject>\\<HostProject>.csproj\" />` before running tests; package references alone do not make the host code visible.");
            builder.AppendLine("- Avoid C# types whose simple name equals the Blazor project or root namespace, such as a `Calculator` class inside namespace `Calculator`. Use a name like `CalculatorEngine` under a non-conflicting namespace such as `<RootNamespace>.Domain`, and import that concrete namespace in `_Imports.razor`.");
            builder.AppendLine("- A `.razor` component file also generates a C# type. In a project/root namespace named `Calculator`, do not create `Components/Calculator.razor`; put the route in `Components/Pages/Home.razor` or name the component `CalculatorPage.razor` so `_Imports.razor` can still import namespaces.");
            builder.AppendLine("- For Blazor Web App scaffolds from `dotnet new blazor`, routed pages live under `Components/Pages`. If `Components/Pages/Home.razor` exists, it is the effective primary route. Put the primary `/` calculator surface there or another `Components/Pages/*.razor` route; do not create legacy root `Pages/*.razor` routes such as `Pages/Home.razor` or `Pages/Index.razor`.");
            builder.AppendLine("- Do not add `@page` directives to `Components/Routes.razor`. That file must stay the generated Router host; route directives belong in `Components/Pages/*.razor`.");
            builder.AppendLine("- If you find both `Components/Pages/Home.razor` and a legacy root `Pages/*.razor` file declaring `@page \"/\"`, delete or move the legacy root route with `workspace_delete_path` before build or runtime validation. Duplicate routes can build successfully but fail at app startup/browser proof.");
            builder.AppendLine("- Do not convert a `dotnet new blazor` Blazor Web App into older Blazor Server/Razor Pages hosting. Do not add `Pages/_Host.cshtml`, `Startup.cs`, `UseStartup<Startup>()`, `blazor.server.js`, or ASP.NET Core 7.x component package references to a net10 Blazor Web App scaffold.");
            builder.AppendLine("- If a repair attempt already added older Blazor Server hosting files or package references, delete `Pages/_Host.cshtml` and other legacy root `Pages/*.cshtml` files, remove obsolete `Microsoft.AspNetCore.Components*` package references, restore the generated minimal `Program.cs`/`Components/App.razor`/`Components/Routes.razor` shape, then rebuild.");
            builder.AppendLine("- Keep the generated `MainLayout` type/file unless you update every `@layout MainLayout`, `DefaultLayout=\"typeof(MainLayout)\"`, and `NotFound.razor` reference in the same change. For recovery, prefer editing `MainLayout` content/styles instead of renaming it.");
            builder.AppendLine("- Do not substitute repeated `workspace_stat_path` calls or checks for `bin/Debug/...` outputs for `workspace_dotnet_build`. The build tool creates and validates those outputs; stat polling does not.");
            builder.AppendLine("- If you scaffold from a starter template, replace placeholder output with the requested product surface before you conclude. Default starter content such as `Hello, world!`, untouched sample routes, or stock template pages is not a completed implementation.");
            builder.AppendLine("- Do not write implementation artifacts that say the requested UI, logic, tests, or rollout preparation will happen in a later step while this implementation step still returns `Completed`.");
            if (artifactInputInspectionPaths.StatPaths.Count > 0 || artifactInputInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine("- Before you implement against inherited requirements or architecture notes, inspect the upstream durable artifacts directly instead of relying only on their summaries.");
                if (artifactInputInspectionPaths.StatPaths.Count > 0)
                {
                    builder.AppendLine($"- Use `workspace_stat_path` on these upstream durable artifact paths before you code against them: {FormatPromptPathList(artifactInputInspectionPaths.StatPaths)}.");
                }

                if (artifactInputInspectionPaths.ReadPaths.Count > 0)
                {
                    builder.AppendLine($"- Use `workspace_read_file` on these upstream durable text artifacts before you code or conclude: {FormatPromptPathList(artifactInputInspectionPaths.ReadPaths)}.");
                }
            }

            builder.AppendLine("- If the solution or project files do not exist yet, bootstrap them now with `workspace_dotnet_new` or an approved local helper path instead of hand-writing only loose source files.");
            builder.AppendLine("- Prefer `workspace_dotnet_new` over hand-written `.csproj` or `.sln` files when you are bootstrapping a greenfield .NET solution.");
            builder.AppendLine("- When you bootstrap with `workspace_dotnet_new`, explicitly request a supported target framework such as `net10.0` instead of accepting an older template default.");
            builder.AppendLine("- If `workspace_dotnet_new` reports overwrite conflicts or exits with code 73, immediately inspect the target directory before you declare a blocker. When a runnable scaffold already exists at the required path, repair and continue in place instead of retrying the scaffold into a deeper nested folder.");
            builder.AppendLine("- If you must write a new `.csproj` manually, choose a target framework supported by this workspace and repo baseline. For this repository, prefer `net10.0` unless the project structure or existing solution explicitly requires another target.");
            builder.AppendLine("- If you create browser-facing UI files such as `.razor`, `.cshtml`, or `wwwroot` assets, scaffold a runnable web host with the required startup entrypoint. Do not leave browser UI inside a plain class library or non-host project.");
            builder.AppendLine("- If the inherited requirements or project structure describe a browser-validated Blazor or web app, leave a runnable browser surface for downstream QA instead of concluding with service-only or library-only output.");
            builder.AppendLine("- If project-structure scope names Blazor SSR, do not replace it with MVC, Razor Pages, or controller/view placeholder scaffolding unless the project structure explicitly changed that architecture.");
            builder.AppendLine("- If no concrete solution, project, or source files exist yet, do not return Completed.");
            if (ContainsCalculatorContext(candidate))
            {
                AppendCalculatorImplementationContract(builder);
            }

            if (projectStructureContext is not null)
            {
                builder.AppendLine("- If the project structure sends you to an external target directory, map that directory to `external-target/<drive>/...`, scaffold the real solution there, inspect those mapped paths, and run `workspace_dotnet_build` against that mapped solution or project.");
                builder.AppendLine("- Use `workspace_pwsh_run_script` only when you need a controlled helper command to bootstrap or verify the exact external target; otherwise stay on the mapped `external-target/...` path with the workspace tools.");
            }

            if (hasGroundedExternalTarget)
            {
                builder.AppendLine($"- For this implementation, bootstrap and edit the runnable app under `{groundedExternalMappedAlias}`. Do not scaffold or repair the product in `artifacts/`, `output/`, `data/`, or other managed evidence folders when the grounded output root is external.");
                builder.AppendLine($"- If you use `workspace_dotnet_new` for this implementation, pass `{groundedExternalMappedAlias}` as the parent directory root instead of an `artifacts/...` evidence directory.");
            }

            if (implementationMentionsTests)
            {
                builder.AppendLine("- This implementation step explicitly includes tests. Add or update the relevant automated tests now and rerun the required validation before you conclude.");
                builder.AppendLine("- Do not defer implementation-owned tests to a later QA-only step when this step title, work brief, or expected outcome already says tests are part of the work.");
            }
        }

        if (RequiresConcreteImplementationReview(candidate))
        {
            builder.AppendLine("- Because this review step depends on real implementation, inspect actual solution, project, or source files in addition to managed artifacts before you conclude.");
            builder.AppendLine("- If the implementation artifacts describe concrete solution, project, source, or required durable evidence paths that the workspace does not contain, return Blocked with the missing concrete paths instead of approving integration readiness.");
            builder.AppendLine("- Successful upstream `workspace_dotnet_build` or `workspace_dotnet_test` receipts for the concrete implementation paths count as validation evidence for this review step. Do not require fresh `bin/`, `obj/`, or other transient build output folders unless the current step contract explicitly requires a rerun or those exact files.");
            builder.AppendLine("- Do not assume a `.sln`, `.slnx`, or specific `bin/Debug/<tfm>` folder must exist unless the work brief, expected outcome, or reviewed artifacts explicitly require that exact path.");
            builder.AppendLine("- If you inspect compiled output locations, derive them from the actual reviewed project files instead of assuming a target framework such as `net8.0`.");
            builder.AppendLine("- When the implementation lives under a grounded external target, review the concrete project and source files in that target instead of blocking only because managed artifact folders do not contain product binaries.");
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            builder.AppendLine("- This step requires runnable browser proof or screenshots, not build-only or file-only evidence.");
            builder.AppendLine("- Before browser proof, inspect the concrete host project, launch settings, or prior successful build/test receipts so you derive the actual launch target and reachable URL from the reviewed implementation.");
            builder.AppendLine("- If no reviewed app is already running, start the concrete host yourself before you open the browser. If `workspace_dotnet_run` is not available in your tool list, create or repair a short PowerShell helper with `workspace_write_file` and run it with `workspace_pwsh_run_script`; the helper should launch `dotnet run --no-build --project <reviewed .csproj> --urls http://127.0.0.1:<free-port>` in the background, wait until the URL returns a successful HTTP status, write a small JSON receipt containing `appProcessId`, URL, stdout log path, and stderr log path, then exit nonzero on 4xx/5xx or early process exit.");
            builder.AppendLine("- Use `external-target/<drive>/...` with workspace file and dotnet tools, but convert that alias to the native Windows path inside PowerShell helper content before passing it to `dotnet`, `Start-Process`, `Test-Path`, or `Resolve-Path`. For example, `external-target/C/programovani/app/App.csproj` must become `C:\\programovani\\app\\App.csproj` inside the helper. Do not pass a relative `external-target/...` string to `dotnet run` from a helper script.");
            builder.AppendLine("- In PowerShell helpers, never assign to `$PID`; it is a built-in read-only variable. Use names such as `$appProcess` and `$appProcessId`, and capture stdout/stderr so a runtime 500 includes actionable logs.");
            builder.AppendLine("- After a successful build/test receipt for the same unchanged project, do not repeat `workspace_dotnet_build` or `workspace_dotnet_test` just because browser proof is still missing. The next required action is app launch plus Playwright browser tools; repeated build/test receipts are not progress.");
            builder.AppendLine("- Do not assume the app must be reachable at `http://localhost:5000/`. Use the actual URL reported by the launch command, host logs, or `launchSettings.json`.");
            builder.AppendLine("- If a Blazor Web App returns HTTP 500 on the primary route after a successful build, inspect the app logs and route files before concluding. A common cause is duplicate `@page \"/\"` routes, especially legacy root `Pages/Index.razor` plus `Components/Pages/Home.razor`.");
            builder.AppendLine("- Do not treat an unstarted app, a missing published deployment, or an empty `bin/Debug/<tfm>` folder as an acceptable blocker when this QA step can launch the reviewed host itself. Launch it, confirm the reachable URL, and only return `Blocked` if launch or browser interaction still fails after you inspect the real diagnostics.");
            builder.AppendLine("- When the implementation lives under a grounded external target, run and inspect the reviewed host project from that target instead of expecting a separate published deployment.");
            builder.AppendLine("- Use the attached Playwright MCP tools after launch: `browser_navigate` to the launched URL, `browser_snapshot` for accessibility proof, `browser_take_screenshot` for visual proof, and `browser_console_messages` for console diagnostics.");
            builder.AppendLine("- After `browser_snapshot`, inspect the saved snapshot content. If it shows starter template text such as `Hello, world!` or `Welcome to your new app.`, return `Blocked` and repair the implementation instead of claiming proof.");
            builder.AppendLine("- For button-driven Blazor apps such as calculators, click a representative sequence and assert that the visible display or history changes to the expected result. If `@onclick` buttons do not mutate state in the browser, report a Blazor render-mode or static-SSR implementation defect instead of treating route reachability as proof.");
            builder.AppendLine("- If the app cannot be launched, the browser cannot be reached, screenshots cannot be captured, or the required UI flow is still missing, return `Blocked` instead of `Completed`.");
            builder.AppendLine("- Do not reframe missing browser proof as a residual risk, deferred next step, or artifact-only note while still marking the step complete.");
        }

        builder.AppendLine("- End your final response with exactly one HTML comment in this format: <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed|Blocked|Failed|WaitingApproval|Refused\",\"reason\":\"short concrete reason\"} -->.");
        if (candidate.BranchOutcomes.Count > 0)
        {
            builder.AppendLine("- If this step completes onto a specific downstream branch, include the exact branchOutcomeKey from the available branch outcomes, for example <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"short concrete reason\",\"branchOutcomeKey\":\"approved\"} -->.");
        }

        builder.AppendLine("- Use status Completed only when the actual work is done, the concrete deliverable exists, required validation passed, and the next step may proceed.");
        builder.AppendLine("- Use status Blocked when unresolved defects, missing proof, rejected approval, or required remediation mean the next step must not proceed yet.");
        builder.AppendLine("- Use status Failed only when tool, execution, or environment failure prevented you from producing a governed step result.");
        builder.Append("Before concluding, create one durable workspace artifact for every required output listed above. Do not ask for confirmation, permission, or a follow-up reply before writing required artifacts. If a required artifact is a text or markdown file you can produce now, write it yourself with workspace tools instead of drafting it in chat. If required upstream artifacts are missing or the concrete deliverable does not exist, stop and say so explicitly. Keep the response concise and mention what you completed.");
        return builder.ToString();
    }

    private static bool ContainsCalculatorContext(DispatchCandidate candidate)
    {
        var contextText = string.Join(
            Environment.NewLine,
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.Name,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary);

        return contextText.Contains("Calculator", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendCalculatorImplementationContract(StringBuilder builder)
    {
        builder.AppendLine("- Calculator implementation contract: the exact product root is the outer directory `external-target/C/programovani/csharp/calculator`. Bootstrap with `workspace_create_directory` for that exact root, then `workspace_dotnet_new` with parentDirectory `external-target/C/programovani/csharp/calculator` and name `Calculator` so the host is `external-target/C/programovani/csharp/calculator/Calculator/Calculator.csproj`.");
        builder.AppendLine("- Calculator implementation contract: never call `workspace_dotnet_new` with parentDirectory `external-target/C/programovani/csharp` and name `Calculator` for this task. On Windows that targets the same lowercase output root by casing and creates the wrong top-level host shape.");
        builder.AppendLine("- Calculator implementation contract: after `workspace_dotnet_new` creates `external-target/.../calculator/Calculator/Calculator.csproj`, that nested host is the canonical app. Do not hand-write or repair `external-target/.../calculator/Calculator.csproj` or `external-target/.../calculator/Program.cs` at the output root.");
        builder.AppendLine("- Calculator implementation contract: preserve the generated Blazor Web App hosting shape. Do not replace `Calculator/Program.cs` with `WebAssemblyHostBuilder`, do not add `Microsoft.AspNetCore.Components.WebAssembly`, and do not add ASP.NET Core 7 component package references to a net10 host.");
        builder.AppendLine("- Calculator implementation contract: complete the concrete source sequence before any artifact writing or final answer: `Calculator/Domain/CalculatorEngine.cs`, `Calculator/Program.cs`, `Calculator/Components/Pages/Home.razor`, `Calculator.Tests/Calculator.Tests.csproj`, and one meaningful sibling test source.");
        builder.AppendLine("- Calculator implementation contract: `Calculator/Program.cs` must register `CalculatorEngine` in DI before `builder.Build()` if `Home.razor` injects it.");
        builder.AppendLine("- Calculator implementation contract: `Calculator/Components/Pages/Home.razor` must be the primary `/` route, call `CalculatorEngine`, and expose add, subtract, multiply, divide, equals/evaluate, numeric keypad, current display/result, divide-by-zero feedback, and calculation history behavior.");
        builder.AppendLine("- Calculator implementation contract: `Calculator/Components/Pages/Home.razor` must start with a valid route such as `@page \"/\"`; `@page \"\"` and `RZ9988` route-template failures mean the app is not buildable.");
        builder.AppendLine("- Calculator implementation contract: Razor keypad callbacks in `Home.razor` must be syntax-safe and type-consistent. Prefer char handlers such as `AppendDigit(char digit)` and `ChooseOperator(char op)` with callbacks like `@onclick=\"() => AppendDigit('1')\"` and `@onclick=\"() => ChooseOperator('+')\"`. If a handler accepts `string`, wrap the whole Razor attribute in single quotes, for example `@onclick='() => AppendDigit(\"1\")'`. Never pass char literals to string handlers, for example do not write `AppendToResult('1')` when the method is `AppendToResult(string value)`, and never write `@onclick=\"() => AppendDigit(\"1\")\"`.");
        builder.AppendLine("- Calculator implementation contract: create the sibling test project with `workspace_dotnet_new` using parentDirectory `external-target/C/programovani/csharp/calculator` and name `Calculator.Tests`, producing `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj`. Never set parentDirectory to a path already ending in `Calculator.Tests`, and never move `Calculator.Tests/Calculator.Tests` to `Calculator.Tests/Calculator.Tests.csproj`.");
        builder.AppendLine("- Calculator implementation contract: `Calculator.Tests/Calculator.Tests.csproj` must contain `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />`; package references alone do not make `Calculator.Domain.CalculatorEngine` visible to tests.");
        builder.AppendLine("- Calculator implementation contract: test source must use the host domain type, for example `using Calculator.Domain;` and `new CalculatorEngine()`, and must assert addition, subtraction, multiplication, division, and divide-by-zero behavior.");
        builder.AppendLine("- Calculator implementation contract: the template `Calculator.Tests/UnitTest1.cs` must not remain as an empty placeholder test; replace it with meaningful tests or delete it if another meaningful test source exists.");
        builder.AppendLine("- Calculator implementation contract: do not use a free-form text box with placeholder parsing or a `Calculate` handler that assigns a fixed result. The UI must invoke the concrete engine operations and update visible state from user-entered keypad/operator interactions.");
        builder.AppendLine("- Calculator implementation contract: repair product behavior before test-project polish. If `Home.razor` is still placeholder/free-form UI, the next concrete mutation must be `Calculator/Components/Pages/Home.razor`; repeatedly rewriting `Calculator.Tests/Calculator.Tests.csproj` is no-progress behavior.");
        builder.AppendLine("- Calculator implementation contract: a valid minimal recovery overwrites or repairs `Calculator/Program.cs`, `Calculator/Components/Pages/Home.razor`, `Calculator/Domain/CalculatorEngine.cs`, `Calculator.Tests/Calculator.Tests.csproj` when its ProjectReference is missing, and a meaningful sibling test source before build/test validation.");
    }

    private static void AppendCalculatorRecoveryChecklist(StringBuilder builder, string missingConcreteImplementationProofSummary)
    {
        builder.AppendLine("Calculator recovery checklist for this retry:");
        if (!string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
        {
            builder.AppendLine($"- Last concrete proof failure: {missingConcreteImplementationProofSummary}.");
        }

        builder.AppendLine("- Do not call `workspace_dotnet_new` again if either `external-target/C/programovani/csharp/calculator/Calculator/Calculator.csproj` or `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj` exists.");
        builder.AppendLine("- If `external-target/C/programovani/csharp/calculator/Calculator.csproj`, `external-target/C/programovani/csharp/calculator/Program.cs`, or `external-target/C/programovani/csharp/calculator/Components` exists at the output-root level, the host was scaffolded in the wrong place. Do not build that root host or create a second project under it in the same attempt; return Blocked/Failed so the next clean run can start from the correct outer-root shape.");
        builder.AppendLine("- If `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj` is a directory, do not write or delete it repeatedly. That path shape is corrupt; stop targeting it, report the path-shape failure, and continue only from a clean sibling test project path on a clean retry.");
        builder.AppendLine("- First read these exact files when present: `external-target/C/programovani/csharp/calculator/Calculator/Calculator.csproj`, `external-target/C/programovani/csharp/calculator/Calculator/Program.cs`, `external-target/C/programovani/csharp/calculator/Calculator/CalculatorEngine.cs`, `external-target/C/programovani/csharp/calculator/Calculator/Components/Routes.razor`, `external-target/C/programovani/csharp/calculator/Calculator/Components/Pages/Home.razor`, `external-target/C/programovani/csharp/calculator/Calculator/Domain/CalculatorEngine.cs`, `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj`, `external-target/C/programovani/csharp/calculator/Calculator.Tests/UnitTest1.cs`, `external-target/C/programovani/csharp/calculator/Calculator.Tests/CalculatorTests.cs`, and `external-target/C/programovani/csharp/calculator/Calculator.Tests/CalculatorEngineTests.cs`.");
        builder.AppendLine("- Repair, in place, with `workspace_write_file`: keep `Calculator/Calculator.csproj` as a net10 Blazor Web App project without ASP.NET Core 7 component package references; keep `Calculator/Program.cs` on the generated `WebApplication`/`AddRazorComponents`/`MapRazorComponents<App>()` hosting path; add `using Calculator.Domain;` and `builder.Services.AddScoped<CalculatorEngine>();` before `builder.Build()` when the page injects the engine.");
        builder.AppendLine("- Repair `Calculator/Components/Pages/Home.razor` as the `/` route instead of editing `Components/Routes.razor`; `Routes.razor` must remain the Router host without `@page`.");
        builder.AppendLine("- If the host build reports `RZ9988`, `@page directive must specify a route template`, or `@page \"\"` in `Home.razor`, the next mutation must set `Home.razor` to `@page \"/\"` before any test-project repair or test rerun.");
        builder.AppendLine("- Replace placeholder UI in `Home.razor`; a free-form expression text box, TODO/parser comment, or `Calculate` method that sets a fixed/default result is not implementation. The route needs numeric keypad buttons, `+`, `-`, `*`, `/`, `=`, display/result state, divide-by-zero feedback, history, and calls to `CalculatorEngine` operations.");
        builder.AppendLine("- When writing `Home.razor` keypad buttons, use syntax-safe callbacks. Preferred pattern: handlers accept `char` and buttons use `@onclick=\"() => AppendDigit('1')\"` and `@onclick=\"() => ChooseOperator('+')\"`. Alternative pattern: handlers accept `string` and buttons use single-quoted Razor attributes such as `@onclick='() => AppendDigit(\"1\")'`. Do not write `@onclick=\"() => AppendDigit(\"1\")\"`, `@onclick=\"() => SetOperation(\"+\")\"`, `AppendToResult('1')` with a string parameter, or `SetOperation('+')` with a string parameter; these caused prior Razor/CS1503 failures.");
        builder.AppendLine("- If `Calculator.Tests/Calculator.Tests.csproj` already contains `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />`, do not rewrite that project file again until after the routed UI proof passes. The blocker is the effective UI, not the test project file.");
        builder.AppendLine("- If tests fail with `CS0234`, `CS0246`, `Calculator.Domain` missing, or `CalculatorEngine` missing from the sibling test project, the next mutation must repair `Calculator.Tests/Calculator.Tests.csproj` to include `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />` and confirm `Calculator/Domain/CalculatorEngine.cs` exists in namespace `Calculator.Domain`.");
        builder.AppendLine("- If the host build fails with `CS0101` or `CS0111` for `Calculator.Domain.CalculatorEngine`, inspect both `Calculator/CalculatorEngine.cs` and `Calculator/Domain/CalculatorEngine.cs`. Delete stale `Calculator/CalculatorEngine.cs` if both define `CalculatorEngine`; deleting and rewriting only `Domain/CalculatorEngine.cs` does not remove the duplicate type.");
        builder.AppendLine("- Repair `Calculator.Tests/Calculator.Tests.csproj` only when the ProjectReference or test packages are missing; replace or delete the generated empty `UnitTest1.cs`; keep concrete arithmetic tests in the sibling test project.");
        builder.AppendLine("- Replace duplicate add/divide-only tests with one meaningful test source that covers Add, Subtract, Multiply, Divide, and divide-by-zero behavior against `CalculatorEngine`.");
        builder.AppendLine("- After the last source or project-file mutation, read back at least `Calculator/Program.cs`, `Calculator/Components/Pages/Home.razor`, `Calculator/Domain/CalculatorEngine.cs`, and `Calculator.Tests/Calculator.Tests.csproj`, then run `workspace_dotnet_build` on `Calculator/Calculator.csproj` and `workspace_dotnet_test` on `Calculator.Tests/Calculator.Tests.csproj`.");
        builder.AppendLine("- Write required markdown artifacts only after those build and test commands succeed in this same retry.");
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

        builder.AppendLine("Required response structure:");
        builder.AppendLine("- Keep the response artifact-first. Use a dedicated markdown heading with the exact artifact title for every required output artifact.");
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

        builder.AppendLine("- If you finish the step successfully, keep those exact section titles in the final response before the PROCESS_STEP_OUTCOME comment.");
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
