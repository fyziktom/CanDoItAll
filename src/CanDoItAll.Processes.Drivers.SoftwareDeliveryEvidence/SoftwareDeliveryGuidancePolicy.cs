namespace CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;

public static class SoftwareDeliveryGuidancePolicy
{
    public static SoftwareDeliveryGuidanceResult Create(SoftwareDeliveryGuidanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SoftwareDeliveryGuidanceResult(
            ImplementationGuidanceLines: [],
            BrowserGuidanceLines: [],
            RecoveryFocusLines: [],
            FinalCautionLines: []);
    }

    public static SoftwareDeliveryExecutionGuidanceResult CreateExecutionGuidance(
        SoftwareDeliveryExecutionGuidanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var signals = SoftwareDeliveryContractRules.ResolveSignals(request.Contract);
        return new SoftwareDeliveryExecutionGuidanceResult(
            ProjectStructureExecutionRuleLines: CreateProjectStructureExecutionRuleLines(request, signals),
            SetupBoundaryLines: CreateSetupBoundaryLines(request),
            BrowserProofBoundaryLines: CreateBrowserProofBoundaryLines(request),
            MandatoryBrowserProofPlanLines: CreateMandatoryBrowserProofPlanLines(request, signals),
            ImplementationProofLines: CreateImplementationProofLines(request, signals),
            BrowserProofLines: CreateBrowserProofLines(request, signals));
    }

    public static SoftwareDeliveryRecoveryGuidanceResult CreateRecoveryGuidance(
        SoftwareDeliveryRecoveryGuidanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var signals = SoftwareDeliveryContractRules.ResolveSignals(request.Contract);
        return new SoftwareDeliveryRecoveryGuidanceResult(
            RecoveryFocusLines: [],
            ImplementationGuidanceLines: CreateRecoveryImplementationGuidanceLines(request, signals),
            BrowserGuidanceLines: CreateRecoveryBrowserGuidanceLines(request, signals),
            FinalCautionLines:
            [
                "Do not recover by writing fake package, framework, runtime, browser, or test-tool shim types. Fix the real dependency or project reference, or return Blocked with a concrete environment/dependency blocker."
            ]);
    }

    private static IReadOnlyList<string> CreateProjectStructureExecutionRuleLines(
        SoftwareDeliveryExecutionGuidanceRequest request,
        SoftwareDeliveryContractSignals signals)
    {
        if (!request.HasProjectStructureExecutionContext)
        {
            return [];
        }

        var lines = new List<string>
        {
            "- Workspace file and execution tools cannot use a raw absolute external path like `C:\\target\\app` directly. Convert it to the mapped alias `external-target/C/target/app` when you call workspace tools that read, write, inspect, validate, or launch files.",
            "- Only inspect or modify `external-target/...` paths that are explicitly named by this run's project-structure grounding, work brief, upstream step artifacts, or tool outputs from this run. Do not reuse remembered prior-example paths or external targets from prior runs.",
            "- If tool policy denies an `external-target/...` path, treat that denied path as invalid for this run. Abandon it immediately and switch to the current grounded product root or current-run artifacts; do not retry or reason from the denied sample path.",
            "- `workspace_pwsh_run_script` executes a script file from the managed workspace. If that script invokes native tools against an external target, convert `external-target/<drive>/...` back to a native path such as `C:\\target\\app` inside the script before passing it to native commands like `Start-Process`, `Test-Path`, or `Resolve-Path`.",
            "- The mapped `external-target/<drive>/...` alias resolves to the real external target. Do not create a shadow copy in a different workspace folder."
        };

        if (request.HasGroundedExternalTarget)
        {
            lines.Add($"- The grounded project structure already identifies the external output root `{request.GroundedExternalAbsolutePath}` mapped to `{request.GroundedExternalMappedAlias}`. Treat that mapped alias as the product root for this run, not as an optional example.");
            lines.Add("- If a temporary managed workspace is used for greenfield scaffolding or validation, the final runnable product must be delivered into the grounded external target before the step can be considered complete.");
            lines.Add("- Completion evidence must cite build, run, or browser proof against the grounded external target after final delivery. Workspace-only proof is not sufficient when an external target is grounded.");
            lines.Add($"- With a grounded external product root, treat the managed workspace as evidence and artifact scratch space only, preferably under `{request.CurrentRunManagedArtifactRoot}`. Do not inspect managed workspace source, test, tool, or script roots such as `src/`, `tests/`, `tools/`, or `scripts/` unless the current run's project structure, work brief, upstream artifacts, or current-run tool outputs explicitly name those paths.");
            if (request.UsesGroundedExternalArtifactDestination)
            {
                lines.Add($"- This grounded external root is described as an artifact, report, plan, document, or handoff destination for non-implementation work. Write required generated deliverable artifacts under `{request.GroundedExternalMappedAlias}` when no narrower artifact path is listed, and keep `{request.CurrentRunManagedArtifactRoot}` for scratch evidence, logs, or managed handoff copies.");
            }
            else if (!request.AllowsExternalTargetMutation)
            {
                lines.Add($"- This step is non-mutating. Do not create directories or write files under `{request.GroundedExternalMappedAlias}`. Write required architecture, scope, review, readiness, or planning artifacts under `{request.CurrentRunManagedArtifactRoot}` unless an exact governed artifact path is listed.");
                lines.Add("- Read product files only when they already exist and the current review step needs them. For a missing greenfield product root, record the intended boundary and leave creation to the modeled setup or implementation step.");
            }

            lines.Add($"- In helper scripts that call native commands, use the native path `{request.GroundedExternalAbsolutePath}` or convert `{request.GroundedExternalMappedAlias}` to that native path before `Resolve-Path`, `Test-Path`, `Set-Location`, `Start-Process`, `cmd.exe`, `node`, `npm`, or similar native calls. Never pass an `external-target/...` alias directly to native PowerShell or process APIs.");
            lines.Add($"- Do not use broad managed-root workspace listing or search to discover launch helpers, source code, or requirements for this external-target run. List or search the grounded external-target alias and `{request.CurrentRunManagedArtifactRoot}` instead.");
            lines.Add("- Do not use files discovered only from broad managed workspace browsing as product requirements, app source, launch scripts, or validation helpers for this run.");
            lines.Add("- Do not list, read, cite, copy, or infer implementation patterns from sibling external-target applications on the same host. Framework examples must come from loaded skills, tool descriptions, official templates, or current-run artifacts, not from unrelated local apps.");
            lines.Add("- Never write `contextual example files`, `source files reviewed`, or similar evidence claims unless the exact files were inspected by current-run tool calls and are inside the grounded product root, current-run artifact root, or an explicitly grounded upstream input.");
        }
        else
        {
            lines.Add("- The dispatcher did not ground an external product root for this run. Do not invent, create, retry, or cite any `external-target/...` path unless a current-run project_structure_read result names an exact absolute local path.");
            lines.Add($"- If the current step must create a concrete greenfield deliverable and no external product root is found after project_structure_read, use `{request.CurrentRunManagedOutputRoot}` as the product root and `{request.CurrentRunManagedArtifactRoot}` for evidence.");
            if (signals.MentionsDotNet)
            {
                lines.Add($"- For .NET scaffolding without an external product root, use `workspace_dotnet_new` under `{request.CurrentRunManagedOutputRoot}`; do not use the bare managed workspace root, shared `src/`, shared `tests/`, or guessed host folders.");
            }
            else if (signals.MentionsJavaScript)
            {
                lines.Add($"- For JavaScript or TypeScript greenfield deliverables without an external product root, create files under `{request.CurrentRunManagedOutputRoot}` with the package/script toolchain named by the current-run requirements. Do not use `workspace_dotnet_new` unless the current-run requirements explicitly name .NET, C#, ASP.NET, Blazor, Razor, `.csproj`, or `.sln`.");
            }
            else
            {
                lines.Add($"- For greenfield deliverables without an external product root, create files under `{request.CurrentRunManagedOutputRoot}` with the toolchain explicitly named by the current-run requirements. Do not assume a .NET, JavaScript, Python, document, or other stack without a current-run stack signal.");
            }
        }

        return lines;
    }

    private static IReadOnlyList<string> CreateSetupBoundaryLines(SoftwareDeliveryExecutionGuidanceRequest request)
    {
        if (!request.Contract.IsDotNetSolutionSetupScaffoldMutationStep)
        {
            return [];
        }

        return
        [
            ".NET setup subprocess boundary:",
            "- This is a scaffold/setup mutation step, not feature implementation, QA, runtime smoke, or browser proof.",
            "- Create or repair only the files named by this step's scaffold contract. Do not add feature behavior, feature tests, template cleanup, package upgrades, browser checks, or runtime proof unless this exact step explicitly requires them.",
            "- Do not run `dotnet run`, launch a web app, invoke browser tools, or create a long-running app process in this step. Leave build/test/runtime/browser validation to the validation step or parent QA step.",
            "- For `create-dotnet-project`, create the solution and app project only. Do not create the test project in that step; the test project belongs to the separate `add-test-project` step.",
            "- Evidence for this step is scaffold file presence, readback of representative solution/project files, and the required setup change-set artifact under the current-run artifact root."
        ];
    }

    private static IReadOnlyList<string> CreateBrowserProofBoundaryLines(
        SoftwareDeliveryExecutionGuidanceRequest request)
    {
        if (!request.HasBrowserSurfaceSignalWithoutProof)
        {
            return [];
        }

        return
        [
            "Browser proof boundary:",
            "- The current project may have a browser-visible surface, but this step is not browser-proof gated. Do not launch the app, invoke browser tools, or return Blocked for missing browser receipts unless this step's own contract explicitly requires runtime or browser proof.",
            "- If browser/runtime validation is needed later, record it as a downstream QA, release, or repair requirement instead of converting this step into that validation step.",
            "- If upstream QA, release, or review artifacts include browser snapshots, screenshots, console logs, or regression evidence files, inspect those inherited artifact paths directly with workspace tools when the prompt lists them. Consuming inherited browser evidence is not the same as capturing fresh browser proof."
        ];
    }

    private static IReadOnlyList<string> CreateMandatoryBrowserProofPlanLines(
        SoftwareDeliveryExecutionGuidanceRequest request,
        SoftwareDeliveryContractSignals signals)
    {
        if (!request.Contract.RequiresConcreteBrowserProof)
        {
            return [];
        }

        var lines = new List<string>
        {
            "Mandatory browser proof execution plan:",
            "- Do not submit the final step outcome from file inspection alone. Current-run browser evidence must come from provider-native browser tools after the reviewed app is reachable."
        };
        if (signals.MentionsDotNet)
        {
            lines.Add("- Start or verify the reviewed .NET host first. Prefer `workspace_dotnet_run` with `keepAlive: true`, capture the reported URL, and let the dispatcher stop the kept-alive process tree after the finalizer.");
        }
        else if (signals.MentionsJavaScript)
        {
            lines.Add($"- Start or verify the reviewed JavaScript host first. If the only launch runner is `workspace_pwsh_run_script`, create a helper under `{request.CurrentRunManagedArtifactRoot}`, create its stdout/stderr directories, convert any `external-target/<drive>/...` alias to a native path inside the helper, invoke package scripts through the Windows shim such as `npm.cmd` or through `cmd.exe /d /s /c \"npm run <script>\"`, and capture the actual localhost URL plus cleanup details.");
            lines.Add("- If that helper writes another PowerShell script, use a single-quoted here-string (`@' ... '@`) or escape literal `$` characters, then read the generated nested script before executing it.");
            lines.Add("- The helper must not be the foreground web server. It must start any long-running static server or package preview as a background child process, wait until a URL is reachable, record the URL and process id, and exit so browser MCP tools can run next.");
            lines.Add("- Do not pass a server implementation script itself to `workspace_pwsh_run_script` when its main body constructs `HttpListener`, calls `GetContext`, runs `python -m http.server`, or contains a request loop. Execute only a bounded launcher script that starts that server as a child process and exits after reachability proof.");
            lines.Add("- Do not call blocking stream reads such as `.ReadToEnd()`, `.ReadToEndAsync().Result`, `.WaitForExit()`, or equivalent waits on redirected stdout/stderr for that long-running child process. Redirect output to files, inherit handles, or use nonblocking event handlers.");
            lines.Add("- In PowerShell launch helpers, prefer `Start-Process -FilePath <command> -ArgumentList <args> -WorkingDirectory <native-path> -RedirectStandardOutput <stdout-file> -RedirectStandardError <stderr-file> -PassThru` for long-running hosts. Do not use `[System.Threading.Tasks.Task]::Run({ ... })` with scriptblocks to copy redirected streams; PowerShell can throw an ambiguous overload binding error before the server starts.");
            lines.Add("- Use native absolute paths for stdout/stderr redirection. If `Start-Process -WorkingDirectory` points at the product root, relative redirect paths such as `artifacts/process-runs/...` will be resolved under the product root and become unreadable from workspace tools.");
            lines.Add("- Do not build child PowerShell server code as a double-quoted `-Command` string when that code contains variables such as `$listener`, `$context`, `$request`, or `$file`. Write a separate child `.ps1` file with a single-quoted here-string, read it back, then launch it with `-File`, or use a reviewed package/static server command instead.");
            lines.Add("- Treat HTTP reachability as the startup proof. Probe the recorded URL with a bounded `Invoke-WebRequest` loop before returning from the helper instead of relying only on stdout text from a long-running child process.");
        }
        else
        {
            lines.Add("- Start or verify the reviewed browser surface first using the stack identified from current-run files, launch settings, package scripts, or upstream artifacts, then capture the actual URL and cleanup details.");
        }

        lines.Add($"- After a reachable URL is known, call `browser_navigate` against that URL, exercise one representative user-visible interaction when the surface is interactive, then call `browser_snapshot` with depth 2, boxes false, and a `.yml` filename under `{request.CurrentRunManagedArtifactRoot}`, `browser_take_screenshot` with a `.png` filename under `{request.CurrentRunManagedArtifactRoot}` and fullPage false or no fullPage argument, and `browser_console_messages` with a `.log` filename under `{request.CurrentRunManagedArtifactRoot}` in this same attempt.");
        lines.Add("- Cite the returned screenshot, snapshot or state, and console filenames in the durable evidence artifact so the process can import them and expose the output folder through linked project structure.");
        lines.Add($"- If ordinary browser interaction tools cannot express the interaction, use `browser_evaluate` with a `.json`, `.txt`, or `.md` filename under `{request.CurrentRunManagedArtifactRoot}` to dispatch representative keyboard or pointer events and read visible state, DOM text, or client-side storage.");
        lines.Add("- If the contract does not explicitly require automated tests, a package manifest, or nonzero test count, do not block static browser deliverable approval only because those artifacts are absent; use browser/runtime proof and record automation coverage as residual quality risk.");
        lines.Add("- If `browser_snapshot` fails for a tool-side selector, parsing, or accessibility-tree reason after other browser evidence proves the workflow, use `browser_evaluate` DOM or state proof as the replacement DOM evidence unless the exact snapshot artifact is explicitly required.");
        lines.Add("- If launch fails before a URL exists, return Blocked with the exact launch command, logs, and repair target. If launch succeeds, do not return Blocked for missing browser receipts before attempting the browser tools.");
        return lines;
    }

    private static IReadOnlyList<string> CreateImplementationProofLines(
        SoftwareDeliveryExecutionGuidanceRequest request,
        SoftwareDeliveryContractSignals signals)
    {
        if (!request.Contract.RequiresConcreteImplementationProof)
        {
            return [];
        }

        var lines = new List<string>();
        if (signals.MentionsDotNet)
        {
            lines.Add("- If `workspace_dotnet_run` fails after build/test passed, treat it as a repairable runtime defect before you return Blocked: inspect the startup diagnostics and repair missing DI registrations, `Program.cs` service wiring, routing, configuration, launch settings, static assets, or app initialization that caused the host or first HTTP request to fail.");
        }
        else
        {
            lines.Add("- If the appropriate startup smoke fails after validation passed, treat it as a repairable runtime defect before you return Blocked: inspect startup diagnostics and repair the real entry point, routing, static assets, package scripts, configuration, or app initialization that caused the failure.");
        }

        if (request.HasProjectStructureExecutionContext)
        {
            lines.Add("- If the project structure sends you to an external target directory, map that directory to `external-target/<drive>/...`, create or update the real deliverable there, and inspect those mapped paths before you conclude.");
            lines.Add("- Use `workspace_pwsh_run_script` only when you need a controlled helper command to bootstrap or verify the exact external target; otherwise stay on the mapped `external-target/...` path with the workspace tools.");
        }

        if (request.HasGroundedExternalTarget)
        {
            lines.Add($"- For this implementation, create and edit the deliverable under `{request.GroundedExternalMappedAlias}`. Do not build a shadow product in `artifacts/`, `output/`, `data/`, or other managed evidence folders when the grounded output root is external.");
            lines.Add($"- If `{request.GroundedExternalMappedAlias}` contains only markdown, notes, summaries, checklists, logs, or empty folders, treat it as an unimplemented product root. Scaffold or create the requested application, service, UI, document, analysis, or other concrete deliverable there before final artifacts.");
            lines.Add($"- If `{request.GroundedExternalMappedAlias}` is an unimplemented product root, the next product action must be a concrete mutation under that root, such as scaffolding a project, writing source/configuration files, or repairing generated content. Do not write final evidence artifacts or submit Blocked before trying that concrete mutation and reading either the changed files or the failure receipt.");
            AddGroundedScaffoldLines(lines, request, signals);
        }

        if (SoftwareDeliveryContractRules.ImplementationContractMentionsTests(request.Contract))
        {
            lines.Add("- This implementation step explicitly includes tests. Add or update the relevant automated tests now and rerun the required validation before you conclude.");
            lines.Add("- Keep automated tests in a dedicated test project or test folder that references the implementation. Do not move test classes into the runnable app project or delete tests to bypass build/test failures.");
            lines.Add("- Do not defer implementation-owned tests to a later QA-only step when this step title, work brief, or expected outcome already says tests are part of the work.");
        }

        if (signals.MentionsDotNet)
        {
            lines.Add("- For Blazor forms, bind inputs only to settable properties or explicit get/set wrappers. Positional records and init-only properties are not valid `@bind` targets and must be replaced with mutable form-state classes or properties before rerunning the build.");
            lines.Add("- For .NET HTTP startup proof that does not need same-step browser follow-up, leave `workspace_dotnet_run` `keepAlive` false so the smoke test stops the launched process tree and avoids locking later builds. If this same step must run browser tools, set `keepAlive: true`, capture browser evidence, and cite the startup receipt; the dispatcher stops the kept-alive process tree after the finalizer, so do not run a cleanup script.");
        }

        return lines;
    }

    private static void AddGroundedScaffoldLines(
        List<string> lines,
        SoftwareDeliveryExecutionGuidanceRequest request,
        SoftwareDeliveryContractSignals signals)
    {
        if (!request.HasGroundedExternalScaffoldTarget)
        {
            return;
        }

        if (signals.MentionsDotNet)
        {
            if (request.Contract.UsesScaffoldContractDrivenSetup)
            {
                lines.Add("- The upstream scaffold contract overrides the generic product-root leaf scaffold shortcut. Read the scaffold contract before scaffolding, then use its solution name, app project name, app directory, template, target framework, and test framework exactly.");
                lines.Add($"- Treat `{request.GroundedExternalMappedAlias}` as the solution/product root named by the contract. Do not derive an app project name from the product-root folder leaf `{request.GroundedExternalLeafName}` and do not scaffold the app directly at the product root unless the scaffold contract explicitly says so.");
                lines.Add($"- For contract-driven .NET solution setup, create the product root when needed, scaffold the solution at `{request.GroundedExternalMappedAlias}`, create the app parent directory from the contract such as `{request.GroundedExternalMappedAlias}/src`, and set `workspace_dotnet_new` `name` to the contract's app project name.");
                lines.Add($"- `{request.GroundedExternalParentAlias}` is only the parent of the product root. It is not a product root, source corpus, evidence root, or permission to inspect sibling folders.");
                return;
            }

            lines.Add($"- For .NET scaffolding into the grounded external product root, use `workspace_dotnet_new` with `parentDirectory` set to `{request.GroundedExternalParentAlias}` and `name` set to `{request.GroundedExternalLeafName}`. If `{request.GroundedExternalMappedAlias}` already exists, inspect and repair it in place instead of creating a sibling or managed artifact copy.");
            lines.Add("- Choose the .NET template and project shape named by the current-run requirements. Do not default to Blazor, Razor, or Web App templates unless the selected work branch explicitly asks for browser UI, Blazor, or Razor; console apps, minimal APIs, workers, services, and libraries must keep their requested archetype.");
            lines.Add($"- If `{request.GroundedExternalMappedAlias}` has no project or source files, invoke `workspace_dotnet_new` with `parentDirectory` `{request.GroundedExternalParentAlias}`, `name` `{request.GroundedExternalLeafName}`, and `force` false before writing implementation-summary artifacts. Existing markdown, checklist, log, or README files in that directory are not a scaffold and are not a reason to skip project creation.");
            lines.Add($"- If `workspace_dotnet_new` cannot scaffold into `{request.GroundedExternalMappedAlias}` because the directory already has evidence files, repair the root in place by writing the required project/source files or return Blocked with the exact scaffold diagnostic. Do not recursively delete `{request.GroundedExternalMappedAlias}` to make room.");
            lines.Add($"- `{request.GroundedExternalParentAlias}` is only the scaffold parent argument for creating `{request.GroundedExternalMappedAlias}`. It is not a product root, evidence root, source corpus, or permission to inspect sibling folders.");
            lines.Add($"- After scaffolding, all reads, writes, builds, tests, runs, and evidence citations must target `{request.GroundedExternalMappedAlias}` or `{request.CurrentRunManagedArtifactRoot}`, not sibling folders under `{request.GroundedExternalParentAlias}`.");
            return;
        }

        if (signals.MentionsJavaScript)
        {
            lines.Add($"- For JavaScript or TypeScript scaffolding into the grounded external product root, create or update the real deliverable directly under `{request.GroundedExternalMappedAlias}` using the package/script toolchain named by the current-run requirements.");
            lines.Add("- Do not use `workspace_dotnet_new` for JavaScript, static HTML, Python, document, analysis, business, or other non-.NET work unless the current-run requirements explicitly name .NET, C#, ASP.NET, Blazor, Razor, `.csproj`, or `.sln`.");
            lines.Add($"- `{request.GroundedExternalParentAlias}` is only the parent of the product root. It is not a product root, evidence root, source corpus, or permission to inspect sibling folders.");
            lines.Add($"- After scaffolding or file creation, all reads, writes, builds, tests, runs, and evidence citations must target `{request.GroundedExternalMappedAlias}` or `{request.CurrentRunManagedArtifactRoot}`, not sibling folders under `{request.GroundedExternalParentAlias}`.");
            return;
        }

        lines.Add($"- For scaffolding into the grounded external product root, create or update the real deliverable directly under `{request.GroundedExternalMappedAlias}` using the toolchain explicitly named by the current-run requirements.");
        lines.Add("- Do not infer a stack only from prior examples, sibling folders, or generic application wording. Use the stack named by the selected work branch, upstream artifacts, or attached skills.");
        lines.Add($"- `{request.GroundedExternalParentAlias}` is only the parent of the product root. It is not a product root, evidence root, source corpus, or permission to inspect sibling folders.");
        lines.Add($"- After scaffolding or file creation, all reads, writes, builds, tests, runs, and evidence citations must target `{request.GroundedExternalMappedAlias}` or `{request.CurrentRunManagedArtifactRoot}`, not sibling folders under `{request.GroundedExternalParentAlias}`.");
    }

    private static IReadOnlyList<string> CreateBrowserProofLines(
        SoftwareDeliveryExecutionGuidanceRequest request,
        SoftwareDeliveryContractSignals signals)
    {
        if (!request.Contract.RequiresConcreteBrowserProof)
        {
            return [];
        }

        var lines = new List<string>
        {
            "- This step requires runnable browser proof or screenshots, not build-only or file-only evidence.",
            "- Before browser proof, inspect the concrete host, launch instructions, prior validation receipts, or reviewed artifacts so you derive the actual target and reachable URL from the implementation.",
            "- If no reviewed browser surface is already running, start it using the launch path and toolchain appropriate for the assigned agent and current step contract, then capture the URL and diagnostics."
        };

        if (signals.MentionsDotNet)
        {
            lines.Add("- For .NET browser proof, call `workspace_dotnet_run` with `keepAlive: true` so Playwright can reach the app. After browser evidence is captured, cite the startup receipt and final evidence; the dispatcher stops the kept-alive process tree after the finalizer, so do not run `workspace_pwsh_run_script` just for cleanup.");
        }
        else if (signals.MentionsJavaScript)
        {
            lines.AddRange(CreateJavaScriptBrowserLaunchGuidance(request.CurrentRunManagedArtifactRoot));
        }
        else
        {
            lines.Add("- For browser proof with an unspecified stack, first inspect the reviewed host, package, launch instructions, or upstream artifacts to determine the actual toolchain. Use .NET, package-script, Python, static-file, or other launch tooling only after that current-run evidence identifies the stack.");
            lines.Add($"- If the only available launch runner is `workspace_pwsh_run_script`, first create a helper script under `{request.CurrentRunManagedArtifactRoot}` with `workspace_write_file`, then inspect or stat that script before running it. Do not invoke a helper path that has not been created in this current run.");
        }

        lines.Add("- Do not assume a fixed URL. Use the actual URL reported by the launch command, host logs, configuration, or reviewed artifacts.");
        lines.Add("- Do not treat an unstarted browser surface, a missing deployment, or unrelated transient output as acceptable proof when this QA step can launch or inspect the reviewed target itself.");
        lines.Add("- Use browser tools after launch for navigation, accessibility or DOM proof, screenshot proof, and console diagnostics when those tools are available to the agent.");
        lines.Add($"- Keep browser evidence bounded and durable: call `browser_snapshot` with depth 2, boxes false, and a `.yml` filename under `{request.CurrentRunManagedArtifactRoot}` unless a specific contract requires depth 3 or 4. Call `browser_take_screenshot` with a `.png` filename under `{request.CurrentRunManagedArtifactRoot}` and fullPage false or omit fullPage. Call `browser_console_messages` with a `.log` filename under `{request.CurrentRunManagedArtifactRoot}`. Do not retry a policy-denied browser call with the same arguments.");
        lines.Add("- When a browser tool accepts a `filename`, request a path under the current-run managed artifact root or an exact required browser artifact path when supported. If the browser tool returns a different provider-native filename such as `.playwright-mcp/...`, cite that exact returned filename in the durable evidence.");
        lines.Add("- Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence. Do not rely on chat-only mentions, stale prior-run files, or unattached markdown links when the step contract requires browser evidence.");
        lines.Add("- Provider-native browser files are created by the browser MCP before managed scope aliasing and may not be visible to `workspace_list_files`, `workspace_stat_path`, or `workspace_read_file` until after the process finalizer imports them. Treat a successful browser tool receipt plus its returned filename as current-run proof, and do not block or select a repair/escalation branch solely because the managed browser folder is empty during the same agent attempt.");
        lines.Add("- If this is a static browser deliverable and the current step contract requires runtime or browser proof but does not explicitly require automated tests, a package manifest, or a nonzero test count, do not make missing `package.json` or missing automated tests release-blocking by itself. Record that as quality risk and validate with source inspection plus launch, console, screenshot or DOM, and representative interaction evidence.");
        lines.Add("- After browser inspection, review the bounded snapshot, screenshot, or tool-returned content. If it shows placeholder starter content or lacks the requested workflow, return Blocked or repair instead of claiming proof.");
        lines.Add($"- For interactive browser work, perform a representative user sequence and assert that visible state changes to the expected result. For canvas, game, custom-control, or keyboard-first surfaces, use `browser_evaluate` with a `.json`, `.txt`, or `.md` filename under `{request.CurrentRunManagedArtifactRoot}` when ordinary browser click/fill helpers cannot express the workflow; dispatch representative keyboard or pointer events and inspect visible state, DOM text, or client-side storage.");
        lines.Add("- If a screenshot call fails, retry once with viewport capture. If bounded snapshot, console diagnostics, and visible-state checks prove the workflow and no exact screenshot artifact is required, do not block solely on the screenshot failure.");
        lines.Add("- If `browser_snapshot` fails because of a tool-side selector, parsing, or accessibility-tree issue after navigation, screenshot, console diagnostics, and representative visible-state checks succeeded, replace it with `browser_evaluate` DOM or state proof and cite the snapshot failure. Do not block solely on the missing snapshot artifact unless the step explicitly requires that exact artifact.");
        lines.Add("- If the app cannot be launched, the browser cannot be reached, bounded browser evidence cannot be captured, or the required UI flow is still missing, do not approve the proof.");
        lines.Add("- When this step has an available branch outcome for repair, remediation, rework, changes required, or rejected validation, use status `Completed` with that exact BranchOutcomeKey for reproducible product defects or missing implemented behavior. Use `Blocked` only when missing inputs, denied tools, unavailable environment, or missing authority prevents you from making the governed quality disposition.");
        lines.Add("- Do not reframe missing browser proof as a residual risk, deferred next step, or artifact-only note while still marking the step complete.");
        return lines;
    }

    private static IReadOnlyList<string> CreateJavaScriptBrowserLaunchGuidance(string currentRunManagedArtifactRoot)
    {
        return
        [
            "- For JavaScript or TypeScript browser proof, start the app with the reviewed package script or launch path available to the assigned agent, preserve the actual URL and diagnostics, then stop any started process before finalizing.",
            "- Do not use `workspace_dotnet_build`, `workspace_dotnet_test`, or `workspace_dotnet_run` for JavaScript or TypeScript deliverables unless the current-run requirements explicitly name .NET, C#, ASP.NET, Blazor, Razor, `.csproj`, or `.sln`.",
            $"- If the available runner is `workspace_pwsh_run_script`, first create a helper script under `{currentRunManagedArtifactRoot}` with `workspace_write_file`, then inspect or stat that script before running it. Do not invoke a helper path that has not been created in this current run.",
            "- If a PowerShell helper writes another PowerShell script, use a single-quoted here-string (`@' ... '@`) or escape every literal `$` in the nested script. Read the generated nested script before running it; malformed lines such as `param([string] = ...)`, `.Start()`, or `.OutputStream.Write(,...)` mean variable expansion corrupted the child script and must be repaired before rerun.",
            "- If a PowerShell helper starts a package preview, static server, `HttpListener`, `python -m http.server`, or similar long-running browser host, launch that host as a background child process, wait for a reachable URL or startup log, write the URL and process id to durable evidence, then let the helper exit. Do not run the long-running server loop inside the `workspace_pwsh_run_script` process until the tool times out.",
            "- For long-running browser hosts, do not call blocking stream reads such as `.ReadToEnd()`, `.ReadToEndAsync().Result`, `.WaitForExit()`, or equivalent waits on redirected stdout/stderr. Redirect output to files, inherit handles, or use nonblocking event handlers so the helper can return after recording URL and process id.",
            "- A non-.NET helper script must convert an `external-target/<drive>/...` alias back to a native path before calling native commands such as `Resolve-Path`, `Test-Path`, `Set-Location`, `Start-Process`, `cmd.exe`, `node`, `npm`, `python`, or static-file launchers. Capture exit codes, stdout/stderr, the actual URL, and cleanup details in durable evidence.",
            "- On Windows, package-manager launch helpers must invoke the real command shim, for example `npm.cmd run preview`, or use `cmd.exe /d /s /c \"npm run preview\"`. Do not use `Start-Process -FilePath 'npm'`; if a helper reports `%1 is not a valid Win32 application`, rewrite it to use `npm.cmd` or `cmd.exe` and rerun the launch.",
            "- Never write helper code like `Resolve-Path 'external-target/C/...'`; native PowerShell resolves that relative to the managed artifact directory and will fail. Translate the alias to `C:\\...` first."
        ];
    }

    private static IReadOnlyList<string> CreateRecoveryImplementationGuidanceLines(
        SoftwareDeliveryRecoveryGuidanceRequest request,
        SoftwareDeliveryContractSignals signals)
    {
        if (!request.Contract.RequiresConcreteImplementationProof)
        {
            return [];
        }

        var lines = new List<string>
        {
            "When the requested deliverable is an application or service, produce a runnable host/project, not only libraries, loose files, or static fragments."
        };

        if (signals.MentionsDotNet)
        {
            lines.Add("For Blazor compile failures around `@bind`, inspect the bound model types: inputs require settable properties or explicit get/set wrappers. Do not keep positional records or init-only properties as bound form state.");
        }

        if (request.HasMissingRunnableApplicationProof)
        {
            if (signals.MentionsDotNet)
            {
                lines.Add("This retry must start the concrete runnable host after the latest implementation changes. For .NET hosts, use workspace_dotnet_run against the host project so startup URL, process id, stdout log, stderr log, and receipt evidence are recorded; for other stacks, use the matching launch tool with equivalent evidence.");
            }
            else
            {
                lines.Add("This retry must start the concrete runnable host after the latest implementation changes using the matching launch tool with URL, process, log, and receipt evidence.");
            }
        }

        if (signals.MentionsDotNet)
        {
            lines.Add("If a .NET startup smoke failed after build/test passed, inspect the captured stdout/stderr or startup receipt and repair the concrete runtime cause before returning Blocked. Common repair targets include missing dependency-injection registrations, `Program.cs` service wiring, routing, appsettings, launch settings, static assets, and startup initialization.");
        }

        if (request.HasProjectStructureGrounding)
        {
            lines.Add("If the resolved target directory is outside the managed workspace, map it to the workspace alias format `external-target/<drive>/...` for workspace tools.");
            lines.Add("Create or repair the exact mapped external-target deliverable now. Use workspace_pwsh_run_script only when you need a controlled helper command for the real external target.");
        }

        return lines;
    }

    private static IReadOnlyList<string> CreateRecoveryBrowserGuidanceLines(
        SoftwareDeliveryRecoveryGuidanceRequest request,
        SoftwareDeliveryContractSignals signals)
    {
        if (!request.Contract.RequiresConcreteBrowserProof)
        {
            return [];
        }

        var lines = new List<string>();
        if (signals.MentionsDotNet)
        {
            lines.Add("If the app is not already running, start the reviewed .NET host yourself before opening the browser. Prefer `workspace_dotnet_run` with `keepAlive: true` so the retry records URL, process id, stdout log, stderr log, and startup receipt evidence while Playwright can reach the app; after browser evidence is captured, stop it with the recorded `startup.json` `stopCommand` before finalizing.");
            lines.Add("For external targets, keep `external-target/<drive>/...` with workspace file and run tools. Do not write a one-off path-translation launch helper when a reviewed generic .NET launch tool is available; missing launch-tool access is a platform blocker to report explicitly.");
        }
        else if (signals.MentionsJavaScript)
        {
            lines.Add("If the app is not already running, start the reviewed JavaScript or TypeScript host yourself before opening the browser. Use the reviewed package script, launch tool, or task-specific skill that records URL, logs, exit code, and cleanup evidence.");
            lines.Add("Do not call `workspace_dotnet_build`, `workspace_dotnet_test`, or `workspace_dotnet_run` for JavaScript or TypeScript deliverables unless the current-run requirements explicitly name .NET, C#, ASP.NET, Blazor, Razor, `.csproj`, or `.sln`.");
            lines.Add($"If only `workspace_pwsh_run_script` is available, first write a helper script under `{request.CurrentRunManagedArtifactRoot}` with `workspace_write_file`, then stat or read it before running it. Do not invoke an artifact helper path that has not been created in this current run.");
            lines.Add("If `workspace_pwsh_run_script` is listed as a missing required tool, do not block from file inspection alone. Create or repair the launch helper and call `workspace_pwsh_run_script`; a missing localhost URL is a valid blocker only after the launch helper ran and its captured diagnostics show no reachable URL.");
            lines.Add("If the helper writes another PowerShell script, use a single-quoted here-string (`@' ... '@`) or escape every literal `$` in the nested script, then read the generated child script before executing it. If the child script contains stripped variables such as `param([string] = ...)`, `.Start()`, or `.OutputStream.Write(,...)`, repair the quoting before rerunning.");
            lines.Add("If the helper starts a static server, package preview, `HttpListener`, `python -m http.server`, or similar long-running browser host, it must start that host as a background child process, wait for a reachable URL, record the URL and process id, and exit. Do not run the foreground server loop inside `workspace_pwsh_run_script` until timeout.");
            lines.Add("Do not pass a server implementation script itself to `workspace_pwsh_run_script` when its main body constructs `HttpListener`, calls `GetContext`, runs `python -m http.server`, or contains a request loop. Execute only a bounded launcher script that starts that server as a child process and exits after reachability proof.");
            lines.Add("Do not call blocking stream reads such as `.ReadToEnd()`, `.ReadToEndAsync().Result`, `.WaitForExit()`, or equivalent waits on redirected stdout/stderr for a long-running browser host. Redirect output to files, inherit handles, or use nonblocking event handlers so the helper can return after startup evidence.");
            lines.Add("For external targets, keep `external-target/<drive>/...` with workspace file tools. Convert that alias to a native path inside the controlled helper script before calling native commands such as `Resolve-Path`, `Test-Path`, `Set-Location`, `Start-Process`, `cmd.exe`, `node`, `npm`, `python`, or a static-file launcher.");
            lines.Add("On Windows, package-script helpers must launch npm through `npm.cmd` or `cmd.exe /d /s /c \"npm run <script>\"`; do not call `Start-Process -FilePath 'npm'`. If the previous helper failed with `%1 is not a valid Win32 application`, rewrite the helper to use `npm.cmd` or `cmd.exe` and rerun browser launch proof.");
            lines.Add("Never call native PowerShell or process APIs with `external-target/...` directly. In a helper, translate `external-target/C/programovani/app` to `C:\\programovani\\app` before `Resolve-Path`, `Set-Location`, package scripts, or launch commands.");
        }
        else
        {
            lines.Add("If the app is not already running, inspect current-run files, launch settings, package scripts, or upstream artifacts to identify the reviewed host stack before opening the browser. Use the launch tool or task-specific skill that matches that evidence and records URL, logs, exit code, and cleanup evidence.");
            lines.Add($"If only `workspace_pwsh_run_script` is available, first write a helper script under `{request.CurrentRunManagedArtifactRoot}` with `workspace_write_file`, then stat or read it before running it. Do not invoke an artifact helper path that has not been created in this current run.");
            lines.Add("For external targets, keep `external-target/<drive>/...` with workspace file tools. Convert that alias to a native path only inside a controlled helper script when the reviewed native command requires it.");
        }

        lines.Add("Do not repeat successful unchanged validations while browser proof is missing. Launch plus browser evidence is the recovery path.");
        lines.Add("Use the UI as an end user would: navigate to the delivered entry point, fill or change representative controls, trigger representative actions, and verify the visible result changes.");
        lines.Add($"For canvas, game, custom-control, or keyboard-first surfaces, use `browser_evaluate` with a `.json`, `.txt`, or `.md` filename under `{request.CurrentRunManagedArtifactRoot}` when ordinary click/fill helpers cannot express the workflow; dispatch representative keyboard or pointer events and inspect visible state, DOM text, or client-side storage.");
        lines.Add("If this is a static browser deliverable and the current step contract requires runtime or browser proof but does not explicitly require automated tests, a package manifest, or a nonzero test count, do not block solely because `package.json` or automated tests are absent. Record the missing automation as quality risk and rely on fresh browser/runtime proof for the retry disposition.");
        lines.Add($"Capture fresh bounded browser evidence with durable filenames before you conclude this retry: call `browser_snapshot` with depth 2, boxes disabled, and a `.yml` filename under `{request.CurrentRunManagedArtifactRoot}` unless the contract requires depth 3 or 4; call `browser_take_screenshot` with a `.png` filename under `{request.CurrentRunManagedArtifactRoot}` and fullPage false or no fullPage argument; call `browser_console_messages` with a `.log` filename under `{request.CurrentRunManagedArtifactRoot}`. Do not retry a policy-denied browser call with the same arguments.");
        lines.Add("Browser screenshots, snapshots, console logs, and state outputs must be current-run artifacts. Cite the returned filenames in the durable evidence artifact so the process can import them and expose the output folder through linked project structure.");
        lines.Add("Provider-native browser files are written before managed scope aliasing and may not be visible to `workspace_list_files`, `workspace_stat_path`, or `workspace_read_file` until after the process finalizer imports them. Inspect the browser tool result itself and cite the returned filename; do not block or select a repair/escalation branch solely because the managed browser folder is empty during the same agent attempt.");
        lines.Add("Inspect the bounded `browser_snapshot` output before concluding. If it still shows starter-template, placeholder, irrelevant content, or non-interactive behavior instead of the requested product behavior, treat it as a routing, rendering, static-content, or client-interaction defect and repair or block instead of returning Completed.");
        lines.Add("If a screenshot call fails, retry once with viewport capture. If bounded snapshot, console diagnostics, and visible-state checks prove the workflow and no exact screenshot artifact is required, do not block solely on the screenshot failure.");
        lines.Add("If `browser_snapshot` fails because of a tool-side selector, parsing, or accessibility-tree issue after navigation, screenshot, console diagnostics, and representative visible-state checks succeeded, replace it with `browser_evaluate` DOM or state proof and cite the snapshot failure. Do not block solely on the missing snapshot artifact unless the step explicitly requires that exact artifact.");
        return lines;
    }
}

public sealed record SoftwareDeliveryGuidanceRequest(
    SoftwareDeliveryImplementationContractSnapshot Contract,
    SoftwareDeliveryProofPolicyResult ProofPolicy,
    IReadOnlyList<string> MissingRequiredTools,
    IReadOnlyList<string> CriticalFailureSummaries,
    bool HasProjectStructureGrounding,
    bool HasCurrentRunBrowserProof,
    bool IsRetryAfterFailedAttempt,
    string RecoveryFactsSummary);

public sealed record SoftwareDeliveryGuidanceResult(
    IReadOnlyList<string> ImplementationGuidanceLines,
    IReadOnlyList<string> BrowserGuidanceLines,
    IReadOnlyList<string> RecoveryFocusLines,
    IReadOnlyList<string> FinalCautionLines)
{
    public bool IsEmpty =>
        ImplementationGuidanceLines.Count == 0 &&
        BrowserGuidanceLines.Count == 0 &&
        RecoveryFocusLines.Count == 0 &&
        FinalCautionLines.Count == 0;
}

public sealed record SoftwareDeliveryExecutionGuidanceRequest(
    SoftwareDeliveryImplementationContractSnapshot Contract,
    bool HasProjectStructureExecutionContext,
    bool HasGroundedExternalTarget,
    string GroundedExternalAbsolutePath,
    string GroundedExternalMappedAlias,
    bool UsesGroundedExternalArtifactDestination,
    bool AllowsExternalTargetMutation,
    bool HasGroundedExternalScaffoldTarget,
    string GroundedExternalParentAlias,
    string GroundedExternalLeafName,
    bool HasBrowserSurfaceSignalWithoutProof,
    string CurrentRunManagedArtifactRoot,
    string CurrentRunManagedOutputRoot);

public sealed record SoftwareDeliveryExecutionGuidanceResult(
    IReadOnlyList<string> ProjectStructureExecutionRuleLines,
    IReadOnlyList<string> SetupBoundaryLines,
    IReadOnlyList<string> BrowserProofBoundaryLines,
    IReadOnlyList<string> MandatoryBrowserProofPlanLines,
    IReadOnlyList<string> ImplementationProofLines,
    IReadOnlyList<string> BrowserProofLines);

public sealed record SoftwareDeliveryRecoveryGuidanceRequest(
    SoftwareDeliveryImplementationContractSnapshot Contract,
    bool HasProjectStructureGrounding,
    bool HasMissingRunnableApplicationProof,
    string CurrentRunManagedArtifactRoot);

public sealed record SoftwareDeliveryRecoveryGuidanceResult(
    IReadOnlyList<string> RecoveryFocusLines,
    IReadOnlyList<string> ImplementationGuidanceLines,
    IReadOnlyList<string> BrowserGuidanceLines,
    IReadOnlyList<string> FinalCautionLines);
