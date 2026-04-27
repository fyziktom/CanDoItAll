# QA Observer Real-Run Extension

## Status

- `Completed`

## Trigger

On 2026-04-26 a real PostgreSQL-backed process run reached `Run QA validation and browser proof` and failed after three Delivery QA Observer attempts.

## Live Run Evidence

- Database: `localhost:5432/candoitall`
- Process run: `33951fbf-9983-4a39-b440-fe0b371b4b32`
- Failed step run: `99413668-f019-4525-a584-a12846ea4b5c`
- Failed step: `Run QA validation and browser proof`
- Executor: `Delivery QA Observer`
- Failure summary: `workspace_pwsh_run_script` failed repeatedly with exit code 3.
- Generated app reviewed: `C:\programovani\dotnet\calculatorblazor\Calculator\Calculator.csproj`

## Root Cause

The QA launch helper passed `external-target/C/programovani/dotnet/calculatorblazor/Calculator/Calculator.csproj` directly to `dotnet run --no-build` from inside a PowerShell script. `workspace_pwsh_run_script` executes the script from the managed workspace, and the workspace path-shortening alias can make that relative value resolve under a drive such as `Z:\external-target\...` instead of the real external drive.

Direct native-path validation proved the host project exists and builds at `C:\programovani\dotnet\calculatorblazor\Calculator\Calculator.csproj`. The failure was therefore QA launch-path handling, not a missing project.

## Changes Made

- Default managed OpenAI provider/agent model is now `gpt-5-mini`, including seed providers, managed agent templates, provider setup defaults, provider health fallback, and repair/normalization paths.
- Delivery QA Observer instructions now require PowerShell helpers to convert `external-target/<drive>/...` to native Windows paths before invoking `dotnet`, `Start-Process`, `Test-Path`, or `Resolve-Path`.
- Process-step prompt and recovery directives now include the same external-target/native-path rule.
- QA validation now explicitly clicks representative button-driven Blazor flows and blocks when `@onclick` buttons do not mutate browser-visible state.
- Docs now state that real process-agent automation should use PostgreSQL when `Processes:Runtime:RequirePostgreSqlForAgentAutomation` is enabled.

## Separate QA Browser Proof

The generated app was launched with the native path:

```powershell
dotnet run --no-build --project C:\programovani\dotnet\calculatorblazor\Calculator\Calculator.csproj --urls http://127.0.0.1:5123
```

Playwright MCP reached `http://127.0.0.1:5123/`, clicked `1`, `+`, `2`, `=`, and captured desktop/mobile screenshots. The display stayed empty and history stayed empty, so the correct QA result is `Blocked`: the app is reachable, but its button-driven `@onclick` UI is not interactive in the browser.

Evidence:

- `reviews/evidence/2026-04-26-qa-observer-playwright/calculator-qa-static-render-defect.png`
- `reviews/evidence/2026-04-26-qa-observer-playwright/calculator-qa-static-render-defect-mobile.png`
- `reviews/evidence/2026-04-26-qa-observer-playwright/calculator-static-render-defect-snapshot.md`

## Validation Commands

| Command | Result | Notes |
| --- | --- | --- |
| `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "AgentProviderModelParameterPolicyTests|ManagedSeedProviderFallbacksTests"` | Passed, 19/19 | Covers GPT-5 temperature omission and managed seed provider defaults. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "Organization_workspace_seeds_serious_delivery_agents_on_openai_with_required_skills|Serious_delivery_review_and_validation_agents_require_durable_file_writes_in_their_seed_instructions|Legacy_serious_delivery_seed_agents_are_refreshed_to_the_current_baseline|Loading_a_stale_managed_catalog_persists_the_refreshed_agent_seed_for_other_processes|BuildExecutionPrompt_requires_blocked_outcome_when_browser_proof_cannot_be_captured|BuildRecoveryDirective_guides_browser_proof_retry_to_launch_grounded_host_and_capture_evidence|Managed_seed_execution_resolution_stays_on_openai_when_openai_key_is_missing|Organization_catalog_repair_rewrites_managed_seed_agents_to_openai_chat_completions"` | Passed, 8/8 after test repair | Covers seed refresh, QA instruction seeding, process prompt/recovery guidance, and catalog repair to `gpt-5-mini`. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "Serious_delivery_review_and_validation_agents_require_durable_file_writes_in_their_seed_instructions|BuildExecutionPrompt_requires_blocked_outcome_when_browser_proof_cannot_be_captured|BuildRecoveryDirective_guides_browser_proof_retry_to_launch_grounded_host_and_capture_evidence"` | Passed, 3/3 | Covers added button-driven Blazor browser-flow blocker. |
| `dotnet build C:\programovani\dotnet\calculatorblazor\Calculator\Calculator.csproj --no-restore /p:UseSharedCompilation=false` | Passed | Confirms the generated host builds from the native external path. |
| `dotnet test C:\programovani\dotnet\calculatorblazor\Calculator.Tests\Calculator.Tests.csproj --no-restore /p:UseSharedCompilation=false` | Passed | Existing MSTest package resolution warnings were already observed on earlier direct runs. |

Existing solution warnings about vulnerable package advisories and nullable/xUnit analyzer warnings remain unrelated to this change.
