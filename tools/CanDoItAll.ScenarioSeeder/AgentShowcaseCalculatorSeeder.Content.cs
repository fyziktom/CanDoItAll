using System.Text.Json;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.ScenarioSeeder;

internal sealed partial class AgentShowcaseCalculatorSeeder
{
    private IReadOnlyList<ShowcaseCapabilitySpec> BuildCapabilitySpecs(ShowcaseWorkspacePlan workspacePlan)
    {
        return
        [
            new ShowcaseCapabilitySpec(
                CapabilityKind.McpServer,
                "showcase-playwright-local-mcp",
                "Showcase Playwright MCP",
                "Local Playwright MCP for screenshot-driven browser validation of the calculator showcase.",
                "npx",
                SerializeConfiguration(new
                {
                    transport = "stdio",
                    serverName = "showcase-playwright",
                    command = "npx",
                    arguments = new[]
                    {
                        "@playwright/mcp@latest",
                        "--headless",
                        "--caps",
                        "vision",
                        "--ignore-https-errors",
                        "--isolated"
                    },
                    workingDirectory = PlaywrightScratchRelativePath,
                    allowedTools = new[]
                    {
                        "browser_navigate",
                        "browser_snapshot",
                        "browser_console_messages",
                        "browser_take_screenshot",
                        "browser_click",
                        "browser_type",
                        "browser_fill_form",
                        "browser_wait_for",
                        "browser_select_option",
                        "browser_hover",
                        "browser_press_key",
                        "browser_close"
                    },
                    approvalMode = "NeverRequire"
                })),
            new ShowcaseCapabilitySpec(
                CapabilityKind.Skill,
                "showcase-ui-proof-inline-skill",
                "Showcase UI Proof Skill",
                "Inline QA skill for launching the calculator app, validating the SSR UI, and capturing durable screenshot evidence.",
                "inline://showcase-ui-proof-inline-skill",
                SerializeConfiguration(new
                {
                    skillSource = "inline",
                    inlineSkill = new
                    {
                        name = "showcase-ui-proof",
                        description = "Launch the calculator app, validate the rendered UI, and collect screenshot-backed evidence.",
                        instructions = BuildUiProofSkillInstructions(),
                        resources = new object[]
                        {
                            new
                            {
                                name = "app-url",
                                content = AppUrl,
                                description = "Expected calculator showcase URL."
                            },
                            new
                            {
                                name = "launch-script",
                                content = LaunchScriptRelativePath,
                                description = "PowerShell helper that starts the SSR calculator app."
                            },
                            new
                            {
                                name = "ui-evidence-root",
                                content = workspacePlan.UiEvidenceFullPath,
                                description = "Absolute filesystem folder for screenshots and smoke-test notes."
                            },
                            new
                            {
                                name = "playwright-scratch-root",
                                content = workspacePlan.PlaywrightScratchFullPath,
                                description = "Playwright MCP working folder where default screenshots, snapshots, and logs are created."
                            },
                            new
                            {
                                name = "playwright-import-script",
                                content = ImportPlaywrightEvidenceScriptRelativePath,
                                description = "PowerShell helper that copies Playwright output into the managed UI evidence root and clears the scratch folder."
                            },
                            new
                            {
                                name = "launch-stdout-log",
                                content = Path.Combine(workspacePlan.UiEvidenceFullPath, "calculator-app.stdout.log"),
                                description = "Launch helper stdout log to inspect when the browser cannot connect."
                            },
                            new
                            {
                                name = "launch-stderr-log",
                                content = Path.Combine(workspacePlan.UiEvidenceFullPath, "calculator-app.stderr.log"),
                                description = "Launch helper stderr log to inspect when the browser cannot connect."
                            }
                        }
                    }
                })),
            new ShowcaseCapabilitySpec(
                CapabilityKind.Skill,
                "showcase-calculator-implementation-inline-skill",
                "Showcase Calculator Implementation Skill",
                "Inline implementation skill for normalizing the calculator app to the exact SSR showcase baseline before build validation.",
                "inline://showcase-calculator-implementation-inline-skill",
                SerializeConfiguration(new
                {
                    skillSource = "inline",
                    inlineSkill = new
                    {
                        name = "showcase-calculator-implementation",
                        description = "Normalize the calculator app to the expected static SSR baseline, then validate the real source before handoff.",
                        instructions = BuildCalculatorImplementationSkillInstructions(),
                        resources = new object[]
                        {
                            new
                            {
                                name = "app-project",
                                content = AppProjectRelativePath,
                                description = "Expected showcase calculator project."
                            },
                            new
                            {
                                name = "calculator-repair-script",
                                content = ApplyAppScriptRelativePath,
                                description = "PowerShell helper that scaffolds or rewrites the calculator app to the showcase baseline."
                            }
                        }
                    }
                })),
            new ShowcaseCapabilitySpec(
                CapabilityKind.Skill,
                "showcase-governance-review-inline-skill",
                "Showcase Governance Review Skill",
                "Inline governance skill for explicit evidence, scope control, and blocker reporting during the showcase run.",
                "inline://showcase-governance-review-inline-skill",
                SerializeConfiguration(new
                {
                    skillSource = "inline",
                    inlineSkill = new
                    {
                        name = "showcase-governance-review",
                        description = "Keep every process step explicit, evidence-backed, and honest about blockers.",
                        instructions = BuildGovernanceSkillInstructions(),
                        resources = new object[]
                        {
                            new
                            {
                                name = "showcase-brief",
                                content = BriefRelativePath,
                                description = "Primary scenario brief shared across roles."
                            },
                            new
                            {
                                name = "process-evidence-root",
                                content = ProcessEvidenceRelativePath,
                                description = "Root folder for role-authored process evidence."
                            }
                        }
                    }
                }))
            ,
            new ShowcaseCapabilitySpec(
                CapabilityKind.Skill,
                "showcase-architecture-review-inline-skill",
                "Showcase Architecture Review Skill",
                "Inline architecture skill for the solution-architect step that stays inside the showcase workspace and produces the required ADR.",
                "inline://showcase-architecture-review-inline-skill",
                SerializeConfiguration(new
                {
                    skillSource = "inline",
                    inlineSkill = new
                    {
                        name = "showcase-architecture-review",
                        description = "Review the calculator showcase architecture without expanding into a repo-wide audit.",
                        instructions = BuildArchitectureReviewSkillInstructions(),
                        resources = new object[]
                        {
                            new
                            {
                                name = "showcase-brief",
                                content = BriefRelativePath,
                                description = "Primary scenario brief shared across roles."
                            },
                            new
                            {
                                name = "app-project",
                                content = AppProjectRelativePath,
                                description = "Target calculator app path. It may not exist yet during the architecture step."
                            },
                            new
                            {
                                name = "calculator-repair-script",
                                content = ApplyAppScriptRelativePath,
                                description = "Deterministic implementation helper referenced by later steps, not a prerequisite for the architecture review."
                            }
                        }
                    }
                }))
        ];
    }

    private IReadOnlyList<ShowcaseAgentSpec> BuildAgentSpecs(ShowcaseWorkspacePlan workspacePlan)
    {
        return
        [
            BuildAgentSpec(
                "product-owner",
                "Showcase Product Owner",
                "Product owner",
                AgentWorkloadKind.Management,
                "Own the feature boundary, acceptance rules, and scope discipline for the calculator showcase.",
                [
                    "Translate the showcase brief into a delivery-ready scope packet.",
                    "Reject scope creep that is not required for a simple SSR calculator.",
                    "Write durable step evidence, not chat-only conclusions."
                ],
                AddEvidenceAuthoringCapabilities(
                [
                    "showcase-governance-review-inline-skill",
                    "generated-app-summary-inline-skill",
                    "workspace-list-files",
                    "workspace-search",
                    "workspace-read-file",
                    "workspace-source-rag"
                ]),
                workspacePlan),
            BuildAgentSpec(
                "delivery-manager",
                "Showcase Delivery Manager",
                "Delivery manager",
                AgentWorkloadKind.Management,
                "Keep the process executable, explicit, and synchronized across all AI delivery roles.",
                [
                    "Track dependencies and escalate blockers immediately.",
                    "Prefer the smallest path that still completes the full showcase.",
                    "Ensure every step leaves a readable evidence trail."
                ],
                AddEvidenceAuthoringCapabilities(
                [
                    "showcase-governance-review-inline-skill",
                    "workspace-list-files",
                    "workspace-search",
                    "workspace-read-file",
                    "workspace-source-rag",
                    "repository-playbook"
                ]),
                workspacePlan),
            BuildAgentSpec(
                "solution-architect",
                "Showcase Solution Architect",
                "Solution architect",
                AgentWorkloadKind.Management,
                "Review architecture fit, reuse existing patterns, and prevent unnecessary complexity in the SSR calculator app.",
                [
                    "Challenge any design that exceeds a simple SSR showcase.",
                    "Keep canonical ownership and module boundaries explicit.",
                    "Record architecture decisions and rejected alternatives."
                ],
                AddEvidenceAuthoringCapabilities(
                [
                    "showcase-governance-review-inline-skill",
                    "showcase-architecture-review-inline-skill",
                    "workspace-list-files",
                    "workspace-search",
                    "workspace-read-file"
                ]),
                workspacePlan),
            BuildAgentSpec(
                "lead-engineer",
                "Showcase Lead Engineer",
                "Lead engineer",
                AgentWorkloadKind.Programming,
                "Implement the calculator app, tests, and supporting notes in the showcase workspace with buildable proof.",
                [
                    "Create the Blazor SSR app under the requested showcase path.",
                    "Use existing repository conventions and keep the change minimal.",
                    "Validate locally before handing off to QA."
                ],
                [
                    "showcase-governance-review-inline-skill",
                    "showcase-calculator-implementation-inline-skill",
                    "aspnet-core-skill",
                    "repository-playbook",
                    "workspace-list-files",
                    "workspace-search",
                    "workspace-read-file",
                    "workspace-create-directory",
                    "workspace-write-file",
                    "workspace-append-file",
                    "workspace-diff-text",
                    "workspace-dotnet-build",
                    "workspace-dotnet-test",
                    "workspace-pwsh-run-script",
                    "workspace-source-rag"
                ],
                workspacePlan),
            BuildAgentSpec(
                "qa-lead",
                "Showcase QA Lead",
                "QA lead",
                AgentWorkloadKind.Qa,
                "Prove the calculator app works through tests, runtime checks, and screenshot-backed browser validation.",
                [
                    "Use the launch script instead of assuming the app is already running.",
                    "Capture screenshots and explicit pass/fail evidence.",
                    "Refuse weak evidence or unverifiable claims."
                ],
                AddEvidenceAuthoringCapabilities(
                [
                    "showcase-governance-review-inline-skill",
                    "showcase-ui-proof-inline-skill",
                    "showcase-playwright-local-mcp",
                    "workspace-list-files",
                    "workspace-search",
                    "workspace-read-file",
                    "workspace-dotnet-build",
                    "workspace-dotnet-test",
                    "workspace-pwsh-run-script",
                    "workspace-source-rag"
                ]),
                workspacePlan),
            BuildAgentSpec(
                "security-reviewer",
                "Showcase Security Reviewer",
                "Security reviewer",
                AgentWorkloadKind.Qa,
                "Review the calculator change for predictable failure modes, secret hygiene, and safe-by-default implementation choices.",
                [
                    "Focus on real trust boundaries and predictable failure handling.",
                    "Call out input-validation or error-masking problems explicitly.",
                    "Write a clear residual-risk note even when the risk is low."
                ],
                AddEvidenceAuthoringCapabilities(
                [
                    "showcase-governance-review-inline-skill",
                    "workspace-list-files",
                    "workspace-search",
                    "workspace-read-file",
                    "workspace-source-rag",
                    "repository-playbook"
                ]),
                workspacePlan),
            BuildAgentSpec(
                "release-manager",
                "Showcase Release Manager",
                "Release manager",
                AgentWorkloadKind.Management,
                "Coordinate release readiness, runtime launch proof, and closure of the calculator showcase.",
                [
                    "Do not approve release readiness without QA and security evidence.",
                    "Use the Playwright-enabled smoke path for final confidence.",
                    "Capture rollout and follow-up observations in durable notes."
                ],
                AddEvidenceAuthoringCapabilities(
                [
                    "showcase-governance-review-inline-skill",
                    "showcase-ui-proof-inline-skill",
                    "showcase-playwright-local-mcp",
                    "generated-app-summary-inline-skill",
                    "workspace-list-files",
                    "workspace-search",
                    "workspace-read-file",
                    "workspace-pwsh-run-script",
                    "workspace-source-rag"
                ]),
                workspacePlan)
        ];
    }

    private static IReadOnlyList<string> AddEvidenceAuthoringCapabilities(IReadOnlyList<string> capabilityKeys)
    {
        var merged = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var capabilityKey in capabilityKeys.Concat(
                     [
                         "workspace-create-directory",
                         "workspace-write-file",
                         "workspace-append-file"
                     ]))
        {
            if (!seen.Add(capabilityKey))
            {
                continue;
            }

            merged.Add(capabilityKey);
        }

        return merged;
    }

    private ShowcaseAgentSpec BuildAgentSpec(
        string roleKey,
        string name,
        string roleTitle,
        AgentWorkloadKind workload,
        string summary,
        IReadOnlyList<string> objectives,
        IReadOnlyList<string> capabilityKeys,
        ShowcaseWorkspacePlan workspacePlan)
    {
        var evidenceDirectoryRelativePath = $"{ProcessEvidenceRelativePath}/{roleKey}";
        var instructions = new StringBuilder()
            .AppendLine($"You are the {roleTitle.ToLowerInvariant()} for the Blazor SSR calculator showcase.")
            .AppendLine($"Work only inside the showcase scope rooted at `{ShowcaseRootRelativePath}`.")
            .AppendLine($"Read `{BriefRelativePath}` before making decisions.")
            .AppendLine($"Write durable markdown evidence into `{evidenceDirectoryRelativePath}`.")
            .AppendLine("When the step prompt names an exact required artifact path, create that file at that exact path inside the managed artifacts root.")
            .AppendLine("Raise blockers explicitly instead of inventing hidden fallbacks.")
            .AppendLine()
            .AppendLine("Execution objectives:")
            .AppendLine(string.Join(Environment.NewLine, objectives.Select(item => $"- {item}")))
            .ToString()
            .Trim();

        return new ShowcaseAgentSpec(
            roleKey,
            name,
            roleTitle,
            summary,
            instructions,
            $"showcase-blazor-ssr-calculator-{roleKey}",
            "gpt-4o-mini",
            workload,
            evidenceDirectoryRelativePath,
            capabilityKeys);
    }

    private static string BuildUiProofSkillInstructions()
    {
        return """
Launch the showcase app with the provided PowerShell script when the step needs a running UI.
Use Playwright navigation, snapshots, and screenshots to verify the rendered SSR page instead of relying on assumptions.
Use real browser tools for the proof path, especially browser_navigate plus mandatory browser_take_screenshot, browser_snapshot, and browser_console_messages calls.
Do not pass managed artifact paths or other absolute workspace paths to browser_snapshot, browser_console_messages, or browser_take_screenshot.
Let Playwright write its default files under the provided playwright-scratch-root, or use filenames relative to that scratch folder.
For every UI-proof run, create all three files in the scratch folder before you call the import script, using the current step key as the parent folder:
- <step-key>/calculator-proof.png via browser_take_screenshot
- <step-key>/calculator-page.yml via browser_snapshot
- <step-key>/calculator-console.log via browser_console_messages
A run without those imported evidence files is invalid.
After browser evidence is created, run the provided playwright-import-script with the current step key so the screenshot, snapshot, and console log are copied into the managed ui-evidence-root.
When you run workspace_pwsh_run_script for that import step, pass outputPaths for ui-evidence-root/<step-key>/calculator-proof.png, ui-evidence-root/<step-key>/calculator-page.yml, ui-evidence-root/<step-key>/calculator-console.log, and ui-evidence-root/<step-key>/import-summary.json so the process runtime records the imported proof as durable execution artifacts.
If the import script does not find all required evidence files, treat the UI proof as failed.
For the simple calculator, verify at least add, subtract, multiply, divide, clear/reset behavior, and divide-by-zero handling.
Reference the imported files under the managed ui-evidence-root in the markdown note so downstream reviewers can open the exact screenshot, snapshot, and console log you validated.
After a successful launch, navigate to the page, wait briefly if needed, and retry the browser check once before concluding the app is unavailable.
If browser access still fails after a successful launch, read the provided launch stdout and stderr logs before writing the blocker note.
Do not report success if the launch script fails, the page does not load, screenshots were not imported into the managed UI evidence root, or the visible behavior disagrees with the acceptance criteria.
When a visible problem appears, capture screenshot evidence and describe the failure precisely with the minimal reproducible path.
""";
    }

    private static string BuildCalculatorImplementationSkillInstructions()
    {
        return $$"""
Run the provided calculator-repair-script first for this scenario before you build, test, or write implementation evidence.
The repair script is the deterministic baseline for this showcase. It must leave Program.cs configured for static SSR only and Home.razor with a GET-driven calculator flow.
The canonical deliverables for this step are {{AppProjectRelativePath}}, {{AppProgramRelativePath}}, and {{AppHomeRelativePath}} exactly as produced by the repair script and the successful build.
Do not replace the repair script with ad-hoc file creation, dotnet new, or speculative manual edits unless the repair script itself fails.
Call workspace_pwsh_run_script for {{ApplyAppScriptRelativePath}} and pass outputPaths for {{AppProjectRelativePath}}, {{AppProgramRelativePath}}, and {{AppHomeRelativePath}} so the runtime records the real deliverable files.
After running the repair script, inspect {{AppProjectRelativePath}}, Program.cs, and Components/Pages/Home.razor before writing any implementation note.
Keep the left and right values bound with [SupplyParameterFromQuery], but treat the operation query value as a string token and parse it explicitly into the enum in code. Do not bind [SupplyParameterFromQuery] directly to CalculatorOperation or CalculatorOperation?.
Do not claim static SSR, clear/reset behavior, divide-by-zero handling, or result rendering unless the source files contain those exact behaviors.
Call workspace_dotnet_build for {{AppProjectRelativePath}} after the source is in place. If a test project exists, run workspace_dotnet_test too.
After the repair script succeeds, do not call workspace_write_file, workspace_append_file, or other mutating file tools against {{AppProjectRelativePath}}, {{AppProgramRelativePath}}, or {{AppHomeRelativePath}} unless the repair script itself left a concrete defect that you can name and fix.
Legacy Blazor Server rewrites are invalid for this showcase. Do not introduce net6.0, explicit Microsoft.AspNetCore.Components.* package references, Startup.cs, UseStartup<Startup>(), or button-only event-handler calculator logic.
Do not write implementation evidence until the repair script and build both succeed and the app-project exists at the canonical showcase path.
Do not run Launch-CalculatorApp.ps1 or Import-PlaywrightEvidence.ps1 in the implementation step. Browser launch and screenshot proof belong to QA and release rollout only.
The implementation note must describe the real files changed and the real validation you executed.
""";
    }

    private static string BuildGovernanceSkillInstructions()
    {
        return """
Keep scope, assumptions, blockers, and evidence explicit.
If a prerequisite artifact or upstream decision is missing, stop and say exactly what is missing.
Prefer short markdown evidence notes with sections for outcome, validation, risks, and follow-up actions.
Do not hide failed checks behind optimistic language.
Do not execute helper scripts, app launches, Playwright imports, or browser proof unless the current step contract explicitly requires them.
""";
    }

    private static string BuildArchitectureReviewSkillInstructions()
    {
        return """
Work only inside the showcase workspace for this step.
Do not load or follow any generic architecture-review workflow that expects the full CanDoItAll repository under src/.
The calculator app project may not exist yet during architecture review. That is not a blocker for this step.
Base the decision on the showcase brief, the upstream scope packet, the target app path, and the explicit acceptance criteria in the process brief.
For this showcase, prefer the smallest correct architecture: a static Blazor SSR app, no unnecessary extra layers, and no second source of truth beyond the existing platform modules.
The required outcome is an architecture decision record, not a broad codebase audit.
Before concluding, write the architecture decision record at the exact required artifact path and make sure it states the selected option, rejected alternatives, source-of-truth choice, and migration ownership.
If an upstream artifact is missing, say that clearly. Do not invent missing workspace files as blockers when the step does not require them yet.
""";
    }

    private string BuildShowcaseBriefContent(ShowcaseWorkspacePlan workspacePlan)
    {
        return new StringBuilder()
            .AppendLine("# Blazor SSR Calculator Showcase")
            .AppendLine()
            .AppendLine("This showcase proves that process-driven AI delivery can create, review, validate, and release a small Blazor SSR application end to end.")
            .AppendLine()
            .AppendLine("## Required output")
            .AppendLine($"- App project path: `{AppProjectRelativePath}`")
            .AppendLine($"- Launch URL: `{AppUrl}`")
            .AppendLine($"- Process evidence root: `{ProcessEvidenceRelativePath}`")
            .AppendLine($"- Managed UI evidence root: `{BuildScopedUiEvidenceRelativePath()}`")
            .AppendLine($"- Browser evidence filesystem root: `{workspacePlan.UiEvidenceFullPath}`")
            .AppendLine($"- Calculator repair script: `{ApplyAppScriptRelativePath}`")
            .AppendLine($"- Playwright evidence import script: `{ImportPlaywrightEvidenceScriptRelativePath}`")
            .AppendLine()
            .AppendLine("## Acceptance criteria")
            .AppendLine("- The app is a Blazor SSR app, not a client-side SPA.")
            .AppendLine("- The UI exposes two numeric inputs, operation controls, a result area, and a clear/reset path.")
            .AppendLine("- Supported operations: add, subtract, multiply, divide.")
            .AppendLine("- Divide-by-zero is handled predictably and explained in the UI.")
            .AppendLine("- The app builds successfully and has targeted validation or tests.")
            .AppendLine("- QA captures screenshot-backed evidence from the running app.")
            .AppendLine("- Process participants record explicit evidence and blockers instead of chat-only conclusions.")
            .AppendLine()
            .AppendLine("## Constraints")
            .AppendLine("- Reuse standard Blazor SSR patterns and keep the code intentionally small.")
            .AppendLine("- Do not introduce unrelated infrastructure or speculative abstractions.")
            .AppendLine("- Prefer maintainable, strongly typed code and predictable error handling.")
            .AppendLine("- Required process evidence must be written under the managed artifacts root so the runtime can register durable handoffs.")
            .AppendLine("- Steps without UI artifact expectations must not launch the app or rerun Playwright proof.")
            .AppendLine()
            .AppendLine("## Browser proof flow")
            .AppendLine("- Browser proof applies only to `qa-validation` and `execute-release-rollout`.")
            .AppendLine("- Browser tools should write to the local `.playwright-mcp` working folder inside the showcase root, not to managed artifact paths.")
            .AppendLine("- Every UI proof step must produce at least one real `.png` screenshot before import.")
            .AppendLine($"- After browser capture, run `{ImportPlaywrightEvidenceScriptRelativePath}` with the current step key to copy the latest screenshot, snapshot, page dump, and console log into the managed UI evidence root.")
            .AppendLine("- Do not mark QA or rollout complete until that imported UI evidence exists.")
            .ToString()
            .Trim();
    }

    private string BuildLaunchScriptContent()
    {
        return $$"""
$ErrorActionPreference = 'Stop'

$workspaceRoot = '{{ToPowerShellLiteral(options.WorkspaceRootPath)}}'
$showcaseRoot = '{{ToPowerShellLiteral(BuildShowcaseRootFullPath())}}'
$appProject = '{{ToPowerShellLiteral(BuildAppProjectFullPath())}}'
$uiEvidenceRoot = '{{ToPowerShellLiteral(BuildUiEvidenceRootFullPath())}}'
$pidFile = Join-Path $uiEvidenceRoot 'calculator-app.pid'
$stdoutLog = Join-Path $uiEvidenceRoot 'calculator-app.stdout.log'
$stderrLog = Join-Path $uiEvidenceRoot 'calculator-app.stderr.log'
$buildLog = Join-Path $uiEvidenceRoot 'calculator-app.build.log'
$appUrl = 'http://127.0.0.1:5088'

if (-not (Test-Path -LiteralPath $appProject)) {
    throw "Calculator project not found at $appProject."
}

New-Item -ItemType Directory -Force -Path $uiEvidenceRoot | Out-Null

if (Test-Path -LiteralPath $pidFile) {
    $existingPidText = Get-Content -LiteralPath $pidFile -Raw
    $existingPid = 0
    if ([int]::TryParse($existingPidText.Trim(), [ref]$existingPid)) {
        $existingProcess = Get-Process -Id $existingPid -ErrorAction SilentlyContinue
        if ($null -ne $existingProcess) {
            Stop-Process -Id $existingPid -Force
            Start-Sleep -Seconds 1
        }
    }
    Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
}

$projectDirectory = [System.IO.Path]::GetDirectoryName($appProject)
if ([string]::IsNullOrWhiteSpace($projectDirectory)) {
    throw "Calculator project directory could not be resolved from $appProject."
}
$appName = [System.IO.Path]::GetFileNameWithoutExtension($appProject)
$buildOutputDirectory = Join-Path $projectDirectory 'bin\\Debug\\net10.0'
$appExecutable = Join-Path $buildOutputDirectory ($appName + '.exe')
$appDll = Join-Path $buildOutputDirectory ($appName + '.dll')

if (-not (Test-Path -LiteralPath $appExecutable) -and -not (Test-Path -LiteralPath $appDll)) {
    $buildOutput = & dotnet build $appProject --nologo 2>&1
    $buildOutput | Set-Content -LiteralPath $buildLog -Encoding utf8NoBOM
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for $appProject. Review $buildLog."
    }
}

$launchFilePath = $appExecutable
$launchArguments = @(
    '--urls',
    $appUrl
)
if (-not (Test-Path -LiteralPath $launchFilePath)) {
    if (-not (Test-Path -LiteralPath $appDll)) {
        $availableOutputs = if (Test-Path -LiteralPath (Join-Path $projectDirectory 'bin\\Debug')) {
            (Get-ChildItem -LiteralPath (Join-Path $projectDirectory 'bin\\Debug') -Directory | Select-Object -ExpandProperty Name) -join ', '
        } else {
            'none'
        }

        throw "Built calculator host was not found under $buildOutputDirectory. Available Debug outputs: $availableOutputs. Review $buildLog."
    }

    $launchFilePath = 'dotnet'
    $launchArguments = @(
        $appDll,
        '--urls',
        $appUrl
    )
}

$process = Start-Process `
    -FilePath $launchFilePath `
    -ArgumentList $launchArguments `
    -WorkingDirectory $projectDirectory `
    -PassThru `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutLog `
    -RedirectStandardError $stderrLog

Set-Content -LiteralPath $pidFile -Value $process.Id -NoNewline

$ready = $false
for ($index = 0; $index -lt 60; $index++) {
    Start-Sleep -Seconds 1
    if ($process.HasExited) {
        break
    }

    try {
        $response = Invoke-WebRequest -Uri $appUrl -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
            $ready = $true
            break
        }
    }
    catch {
    }
}

if (-not $ready) {
    if ($process.HasExited) {
        throw "Calculator app exited before becoming ready. Review $stdoutLog and $stderrLog."
    }

    throw "Calculator app did not become ready at $appUrl within the timeout window."
}

Write-Output "Calculator app ready at $appUrl"
""";
    }

    private string BuildStopScriptContent()
    {
        return $$"""
$ErrorActionPreference = 'Stop'

$workspaceRoot = '{{ToPowerShellLiteral(options.WorkspaceRootPath)}}'
$pidFile = '{{ToPowerShellLiteral(Path.Combine(BuildUiEvidenceRootFullPath(), "calculator-app.pid"))}}'
if (-not (Test-Path -LiteralPath $pidFile)) {
    Write-Output 'Calculator app is not running.'
    return
}

$pidText = Get-Content -LiteralPath $pidFile -Raw
$pidValue = 0
if (-not [int]::TryParse($pidText, [ref]$pidValue)) {
    Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
    throw "Stored PID '$pidText' is invalid."
}

$process = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
if ($null -ne $process) {
    Stop-Process -Id $pidValue -Force
}

Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
Write-Output 'Calculator app stopped.'
""";
    }

    private string BuildApplyCalculatorAppScriptContent()
    {
        return $$"""
$ErrorActionPreference = 'Stop'

$workspaceRoot = '{{ToPowerShellLiteral(options.WorkspaceRootPath)}}'
$showcaseRoot = '{{ToPowerShellLiteral(BuildShowcaseRootFullPath())}}'
$appRoot = '{{ToPowerShellLiteral(BuildAppRootFullPath())}}'
$appProject = Join-Path $appRoot 'SimpleCalculatorApp.csproj'
$expectedTargetFramework = '<TargetFramework>net10.0</TargetFramework>'
$requiresRescaffold = -not (Test-Path -LiteralPath $appProject)

if (-not $requiresRescaffold) {
    $appProjectContent = Get-Content -LiteralPath $appProject -Raw
    $requiresRescaffold = $appProjectContent.IndexOf($expectedTargetFramework, [System.StringComparison]::OrdinalIgnoreCase) -lt 0
}

if ($requiresRescaffold) {
    if (Test-Path -LiteralPath $appRoot) {
        Remove-Item -LiteralPath $appRoot -Recurse -Force
    }

    & dotnet new blazor --name SimpleCalculatorApp --output $appRoot --no-restore | Out-Null
}

$programContent = @'
{{BuildCalculatorProgramContent()}}
'@
Set-Content -LiteralPath (Join-Path $appRoot 'Program.cs') -Value $programContent -Encoding utf8NoBOM

$homePath = Join-Path $appRoot 'Components\Pages\Home.razor'
New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($homePath)) | Out-Null
$homeContent = @'
{{BuildCalculatorHomeContent()}}
'@
Set-Content -LiteralPath $homePath -Value $homeContent -Encoding utf8NoBOM

Write-Output "Calculator showcase app normalized at $appProject"
""";
    }

    private string BuildImportPlaywrightEvidenceScriptContent()
    {
        return $$"""
param(
    [Parameter(Mandatory = $true)]
    [string]$StepKey
)

$ErrorActionPreference = 'Stop'

$workspaceRoot = '{{ToPowerShellLiteral(options.WorkspaceRootPath)}}'
$showcaseRoot = '{{ToPowerShellLiteral(BuildShowcaseRootFullPath())}}'
$playwrightRoot = Join-Path $showcaseRoot '.playwright-mcp'
$showcaseStepRoot = Join-Path $showcaseRoot $StepKey
$uiEvidenceRoot = '{{ToPowerShellLiteral(BuildUiEvidenceRootFullPath())}}'
$targetRoot = Join-Path $uiEvidenceRoot $StepKey

$stepScratchRoot = Join-Path $playwrightRoot $StepKey
$candidateRoots = @(
    $stepScratchRoot,
    $showcaseStepRoot,
    $playwrightRoot
) | Where-Object {
    Test-Path -LiteralPath $_
}

if ($candidateRoots.Count -eq 0) {
    throw "Playwright evidence roots were not found at $stepScratchRoot, $showcaseStepRoot, or $playwrightRoot."
}

$sourceRoot = $null
$files = @()
foreach ($candidateRoot in $candidateRoots) {
    $candidateFiles = Get-ChildItem -LiteralPath $candidateRoot -File -Recurse -Force | Where-Object {
        $_.Extension -in '.png', '.jpg', '.jpeg', '.md', '.yml', '.yaml', '.log'
    } | Sort-Object LastWriteTimeUtc, Name

    if ($candidateFiles.Count -eq 0) {
        continue
    }

    $sourceRoot = $candidateRoot
    $files = @($candidateFiles)
    break
}

if ($files.Count -eq 0) {
    throw "No Playwright evidence files were found under any expected source root."
}

$screenshots = $files | Where-Object { $_.Extension -in '.png', '.jpg', '.jpeg' }
if ($screenshots.Count -eq 0) {
    throw "No Playwright screenshot file was found under $sourceRoot."
}

if (Test-Path -LiteralPath $targetRoot) {
    Remove-Item -LiteralPath $targetRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null

foreach ($file in $files) {
    $relativePath = [System.IO.Path]::GetRelativePath($sourceRoot, $file.FullName)
    $destination = Join-Path $targetRoot $relativePath
    $destinationDirectory = Split-Path -Parent $destination
    if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
        New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    }

    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}

$summary = [pscustomobject]@{
    stepKey = $StepKey
    importedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    sourceRoot = $sourceRoot
    importedFiles = @($files | ForEach-Object {
        [System.IO.Path]::GetRelativePath($sourceRoot, $_.FullName)
    })
}
$summary | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $targetRoot 'import-summary.json') -Encoding utf8NoBOM

if ((Test-Path -LiteralPath $stepScratchRoot) -and -not [string]::Equals($stepScratchRoot, $targetRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    Remove-Item -LiteralPath $stepScratchRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if ((Test-Path -LiteralPath $showcaseStepRoot) -and -not [string]::Equals($showcaseStepRoot, $targetRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    Remove-Item -LiteralPath $showcaseStepRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output "Imported $($files.Count) Playwright evidence file(s) into $targetRoot."
""";
    }

    private static string BuildCalculatorProgramContent()
    {
        return """
using SimpleCalculatorApp.Components;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();
""";
    }

    private static string BuildCalculatorHomeContent()
    {
        return """
@page "/"
@using System.Globalization
@using Microsoft.AspNetCore.Components

<PageTitle>Simple Calculator</PageTitle>

<div class="container py-4">
    <h1 class="mb-3">Simple Calculator</h1>
    <p class="text-body-secondary">
        Enter two values, choose an arithmetic operation, and use the clear link to reset the form.
    </p>

    <form method="get" class="row g-3">
        <div class="col-12 col-md-4">
            <label class="form-label" for="left">Left value</label>
            <input
                id="left"
                name="left"
                type="number"
                step="0.01"
                value="@FormatValue(Left)"
                class="form-control" />
        </div>

        <div class="col-12 col-md-4">
            <label class="form-label" for="right">Right value</label>
            <input
                id="right"
                name="right"
                type="number"
                step="0.01"
                value="@FormatValue(Right)"
                class="form-control" />
        </div>

        <div class="col-12 col-md-4 d-flex align-items-end gap-2 flex-wrap">
            <button type="submit" name="operation" value="@nameof(CalculatorOperation.Add)" class="btn btn-primary">Add</button>
            <button type="submit" name="operation" value="@nameof(CalculatorOperation.Subtract)" class="btn btn-primary">Subtract</button>
            <button type="submit" name="operation" value="@nameof(CalculatorOperation.Multiply)" class="btn btn-primary">Multiply</button>
            <button type="submit" name="operation" value="@nameof(CalculatorOperation.Divide)" class="btn btn-primary">Divide</button>
            <a href="/" class="btn btn-outline-secondary">Clear</a>
        </div>
    </form>

    <section class="mt-4">
        <h2 class="h5">Current state</h2>
        <p>Operation: @GetOperationLabel()</p>
        @if (!string.IsNullOrWhiteSpace(ErrorMessage)) {
            <p class="text-danger">@ErrorMessage</p>
        } else if (Result.HasValue) {
            <p>Result: @Result.Value.ToString(CultureInfo.InvariantCulture)</p>
        } else {
            <p>Result: Provide two values and choose an operation.</p>
        }
    </section>
</div>

@code {
    [SupplyParameterFromQuery(Name = "left")]
    public decimal? Left { get; set; }

    [SupplyParameterFromQuery(Name = "right")]
    public decimal? Right { get; set; }

    [SupplyParameterFromQuery(Name = "operation")]
    public string? OperationToken { get; set; }

    private CalculatorOperation? Operation { get; set; }

    private decimal? Result { get; set; }

    private string? ErrorMessage { get; set; }

    protected override void OnParametersSet()
    {
        Result = null;
        ErrorMessage = null;
        Operation = ParseOperation(OperationToken);

        if (!Left.HasValue || !Right.HasValue || !Operation.HasValue) {
            return;
        }

        if (Operation.Value == CalculatorOperation.Divide && Right.Value == 0m) {
            ErrorMessage = "Division by zero is not allowed.";
            return;
        }

        Result = Operation.Value switch {
            CalculatorOperation.Add => Left.Value + Right.Value,
            CalculatorOperation.Subtract => Left.Value - Right.Value,
            CalculatorOperation.Multiply => Left.Value * Right.Value,
            CalculatorOperation.Divide => Left.Value / Right.Value,
            _ => null
        };
    }

    private string GetOperationLabel()
    {
        return Operation?.ToString() ?? "None selected";
    }

    private static CalculatorOperation? ParseOperation(string? operationToken)
    {
        if (string.IsNullOrWhiteSpace(operationToken)) {
            return null;
        }

        return Enum.TryParse<CalculatorOperation>(operationToken, ignoreCase: true, out var operation)
            ? operation
            : null;
    }

    private static string FormatValue(decimal? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public enum CalculatorOperation
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }
}
""";
    }

    private static string BuildWorkspaceRootTraversal()
    {
        var depth = ShowcaseRootRelativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Length;
        return string.Join(
            Path.DirectorySeparatorChar,
            Enumerable.Repeat("..", depth));
    }

    private string BuildScopedUiEvidenceRelativePath()
    {
        return ResolveScopedManagedRelativePath(
            UiEvidenceRelativePath,
            workspaceFactory.GetOrganizationScope());
    }

    private string BuildShowcaseRootFullPath()
    {
        return Path.Combine(
            options.WorkspaceRootPath,
            ShowcaseRootRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string BuildAppRootFullPath()
    {
        return Path.Combine(
            options.WorkspaceRootPath,
            Path.GetDirectoryName(AppProjectRelativePath.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty);
    }

    private string BuildAppProjectFullPath()
    {
        return Path.Combine(
            options.WorkspaceRootPath,
            AppProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string BuildUiEvidenceRootFullPath()
    {
        return Path.Combine(
            options.WorkspaceRootPath,
            BuildScopedUiEvidenceRelativePath().Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ToPowerShellLiteral(string path)
    {
        return path.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string SerializeConfiguration(object value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static Guid EnsureSuccess(Result<Guid> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(FormatErrors(result.Errors));
        }

        return result.Value;
    }

    private static void EnsureSuccess(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(FormatErrors(result.Errors));
        }
    }

    private static string FormatErrors(IReadOnlyCollection<Error> errors)
    {
        if (errors.Count == 0)
        {
            return "Unknown failure.";
        }

        return string.Join(
            Environment.NewLine,
            errors.Select(error => $"{error.Code}: {error.Message}"));
    }
}
