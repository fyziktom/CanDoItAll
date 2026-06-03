# SB08 Proof Manifest

## Scope

Subbundle: `SB08 UI and observability hardening for blockers and usage`.

This pass makes contract blockers, policy denials, missing/unknown/estimated usage, zero cost, known actual cost, and workflow executor side-effect/preview semantics visible in the process and workflow UI.

## Source Changes

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessUsageDisplayAdapter.cs`
  - Adds `ProcessUsageCostDisplayKind` and distinguishes `KnownActual`, `Estimated`, `UnknownUsage`, `MissingUsage`, and `ZeroCost`.
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
  - Adds invariant diagnostics, recommended action, target scope, allowed operations, block code, recovery options, contract/blocker details, and policy-denied descriptions.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowExecutorDisplayAdapter.cs`
  - Adds preview/commit executor badge and description.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
  - Shows LLM provider usage summaries and storage executor preview/commit side-effect status in editor and modal surfaces.
- `repo://tests/CanDoItAll.Tests.Components/ProcessUsageDisplayAdapterTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowExecutorDisplayAdapterTests.cs`
  - Adds coverage for usage display distinctions and workflow executor preview/commit badge behavior.
- `repo://tests/CanDoItAll.Tests.Playwright/Sb08OperationalObservabilityBrowserTests.cs`
  - Adds live browser proof for `/processes/live`, process detail, step detail, workflow selection window, and workflow executor editor in desktop and mobile.

Changed file hashes:

- `bundle://proof/SB08/changed-file-hashes.txt`

## Passing Proof

- `bundle://proof/SB08/transcripts/passing-component-adapters.txt`
  - Component adapter slice: 7/7 passed.
- `bundle://proof/SB08/transcripts/passing-web-build.txt`
  - `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore --verbosity minimal`
  - Result: build succeeded with existing EF `MSB3277` warnings and 0 errors.
- `bundle://proof/SB08/transcripts/browser-proof-live-passing-attempt-3.txt`
  - Live Playwright proof: 1/1 passed.
- `bundle://proof/SB08/browser/browser-validation-summary.json`
  - Desktop and mobile proof rows captured, each with process detail and workflow executor proof, zero console errors, zero page errors, and zero failed responses.

## Browser Artifacts

- `bundle://proof/SB08/browser/live-processes-desktop.png`
- `bundle://proof/SB08/browser/live-process-detail-desktop.png`
- `bundle://proof/SB08/browser/live-process-detail-steps-desktop.png`
- `bundle://proof/SB08/browser/live-step-detail-desktop.png`
- `bundle://proof/SB08/browser/workflow-selection-window-desktop.png`
- `bundle://proof/SB08/browser/workflow-executor-editor-desktop.png`
- `bundle://proof/SB08/browser/live-processes-mobile.png`
- `bundle://proof/SB08/browser/live-process-detail-mobile.png`
- `bundle://proof/SB08/browser/live-process-detail-steps-mobile.png`
- `bundle://proof/SB08/browser/live-step-detail-mobile.png`
- `bundle://proof/SB08/browser/workflow-selection-window-mobile.png`
- `bundle://proof/SB08/browser/workflow-executor-editor-mobile.png`
- `bundle://proof/SB08/browser/browser-console-errors-desktop.txt`
- `bundle://proof/SB08/browser/browser-console-errors-mobile.txt`
- `bundle://proof/SB08/browser/browser-page-errors-desktop.txt`
- `bundle://proof/SB08/browser/browser-page-errors-mobile.txt`

The raw failed-request logs contain only expected Blazor disconnect aborts during page teardown; the proof test treats those as non-actionable and still fails on static asset, page, console, or response errors.

## Anti-Stub Audit

- `bundle://proof/SB08/anti-stub-audit.txt`
  - Scanned UI adapters, Blazor components, and SB08 tests for TODO, NotImplemented, and stub-only markers.
  - Result: pass.

## Raw Note Closure

SB08 closes the raw-note slice for UI observability. The process UI no longer presents missing provider usage as precise actual zero cost, and the workflow editor exposes side-effect and preview/commit semantics for storage executors.

## Downstream Impact

SB09 must manually inspect the desktop/mobile screenshots and verify the UI wording matches runtime state.
