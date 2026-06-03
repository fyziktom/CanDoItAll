# SB04 Proof Manifest

## Scope

Subbundle: `SB04 Real agent-driven multi-domain process E2E harness`.

This pass replaces the old V1 fixture-style SB08 proof with current-run process automation proof. The harness seeds request packets, starts real process runs, waits for automation dispatch, resolves generated source roots from process artifacts/tool receipts, and validates the generated Blazor app in desktop and mobile browsers. It does not scaffold or write scenario app source itself.

## Source Changes

- `repo://codex/bundles/process-workflow-agent-hardening-v2/scripts/run_sb04_real_process_e2e.ps1`
  - Adds the real process E2E harness and removes harness-owned app generation from the claimed production proof path.
- `repo://tests/CanDoItAll.Tests.Playwright/Sb04GeneratedAppBrowserValidationTests.cs`
  - Adds desktop/mobile generated-app browser validation, screenshots, console/page/network logs, reload/persistence checks, and static-asset failure checks.
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs`
  - Keeps `dotnet run` validation resilient when generated static web asset manifests reference a temporary subst drive.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs`
  - Cleans up static-web-assets drive aliases created for long generated paths.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
  - Allows explicit QA repair disposition to complete when diagnostic tool failures are the route signal, without weakening implementation critical-tool failure behavior.
- `repo://Templates/Processes/processes/blazor-app-delivery/steps/*.md`
- `repo://src/CanDoItAll.AgentFramework.Persistence/SeedAssets/instructions/skills/dotnet-app-delivery.md`
- `repo://src/CanDoItAll.AgentFramework.Persistence/SeedAssets/instructions/skills/blazor-ssr-delivery.md`
  - Harden generated-app instructions for source-root placement, browser validation, asset validation, warning handling, and Blazor WASM/PWA runtime issues.

Changed file hashes:

- `bundle://proof/SB04/changed-file-hashes.txt`

Source assertions:

- `bundle://proof/SB04/source-assertions.txt`

## Run Set

- Schema: `candoitall.sb04.realProcessE2E.v1`
- Manifest: `bundle://proof/SB04/manifest.json`
- Run stamp: `20260602-160706-and-20260602-181836-consolidated`
- Source: four scenarios from full five-scenario harness run `20260602-160704/160706`; recipe replacement from recipe-only rerun `20260602-181834/181836`.
- Process template: `blazor-app-delivery`
- Scenario count: 5
- Browser proof deferred: false

## Scenarios

| Scenario | Process run id | Proof folder |
| --- | --- | --- |
| `tetris-mini-game` | `22edea5a-0e1d-4a4b-a3c9-312a2b04b75f` | `bundle://proof/SB04/scenarios/tetris-mini-game` |
| `expense-tracker-lite` | `4a9c3618-c095-4459-9603-fd8f97b031ac` | `bundle://proof/SB04/scenarios/expense-tracker-lite` |
| `plant-watering-planner` | `27ebcdab-04f6-4576-858b-1cda593293b5` | `bundle://proof/SB04/scenarios/plant-watering-planner` |
| `study-kanban-flashcards` | `c6ae4288-6b06-4607-944e-918b7697f004` | `bundle://proof/SB04/scenarios/study-kanban-flashcards` |
| `recipe-pantry-planner` | `2b017a75-4941-485e-b187-dcd6a08809ad` | `bundle://proof/SB04/scenarios/recipe-pantry-planner` |

Each scenario folder contains `process-run-detail.json`, `agent-execution-runs.json`, `tool-receipts.json`, `usage-summary.json`, `generated-source-root.json`, `generated-source-root-layout.json`, `command-transcripts/dotnet-build-generated-app.txt`, and `browser/browser-validation-summary.json`.

## Passing Proof

- `bundle://proof/SB04/command-transcripts/run-sb04-five-scenarios-full-20260602-160704.out.txt`
  - Full harness run that produced four accepted scenario proof sets.
- `bundle://proof/SB04/command-transcripts/run-sb04-recipe-only-20260602-181834.out.txt`
  - Recipe replacement rerun after QA accepted and repair branch skipped.
- `bundle://proof/SB05/transcripts/passing-new-sb04-proof.txt`
  - `python codex\bundles\process-workflow-agent-hardening-v2\scripts\validate_bundle.py --check-process-e2e-proof --process-e2e-proof codex\bundles\process-workflow-agent-hardening-v2\proof\SB04 --process-e2e-script codex\bundles\process-workflow-agent-hardening-v2\scripts\run_sb04_real_process_e2e.ps1`
  - Result: pass.
- `bundle://proof/SB07/transcripts/template-contract-and-scenario-scan.txt`
  - Scenario-key scan passed for production templates, agents, skills, and seed assets.

## Anti-Stub Audit

- `bundle://proof/SB04/anti-stub-audit.txt`
  - Scanned the SB04 harness, generated-app browser validator, static-assets cleanup, QA routing change, and hardened Blazor delivery prompts for `TODO`, `NotImplemented`, `throw new NotImplementedException`, `fixture-specific`, and stub markers.
  - Result: pass.

## Raw Note Closure

SB04 closes the raw-note slice for "real five-example app-generation tests" with five real process runs, non-empty process-step execution runs, tool receipts, provider usage observations, current-run generated roots, builds, browser screenshots, browser diagnostics, and cleanup receipts.

## Downstream Impact

SB05 now validates this proof shape and rejects the old V1 SB08 proof shape. SB06 refactor and SB08 UI changes were verified after this proof path existed.
