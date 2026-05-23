# Current State

## Repository Findings

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs` already instructs QA/browser-proof steps to launch the real surface, navigate, perform a representative interaction, call `browser_snapshot`, `browser_take_screenshot`, and `browser_console_messages`, and use `browser_evaluate` for canvas/game/custom-control surfaces.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs` derives browser-proof gating from generic step contracts, expected artifacts, work briefs, and project structure context.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` recognizes declared paths only when validation text contains markers such as `Create this artifact at`, `must exist at`, or `must be written at`. The DB expectations required screenshots in prose but did not declare exact paths.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` can project provider-native browser outputs, but it depends on expected paths, discovered browser outputs, and filename matching.
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ProviderNativeMcp.cs` enriches provider-native browser MCP calls into synthetic receipts from `InMemoryChatHistoryProvider.messages`. The failing execution run had an empty messages array.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` mirrors screenshots to scoped artifact paths only when the browser MCP produced the requested unscoped file. In the failing run, default `.playwright-mcp\page-*` files existed but the requested `artifacts/process-runs/.../browser-proof.png` did not.
- Existing tests in `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` cover browser proof prompting, missing browser proof, and console-defect handling, but they do not cover a completed process run where required screenshot prose exists, browser tools are invoked, provider-native files exist only under `.playwright-mcp`, no process image artifact records exist, and conformance observations remain empty.

## Behavioral Findings

- The process completed and selected `Quality accepted` after repair, even though no screenshot or browser proof artifact was recorded in `Processes_ArtifactRecords`.
- The repaired evidence pack cited provider-native `.playwright-mcp` paths. Those paths are useful raw evidence, but they are not scoped process artifacts and were not available through the process evidence ledger.
- The screenshot at `.playwright-mcp\page-2026-05-22T14-59-45-865Z.png` visually showed the Tetris page in a paused state. That was accepted as a visible state transition, but it did not prove that active pieces were visible or that representative gameplay worked.
- The console log at `.playwright-mcp\console-2026-05-22T14-58-45-447Z.log` contains errors after roughly 86 seconds. These may have occurred after the app was stopped, but the evidence pack still claimed console diagnostics were warning-free without a durable active-proof interval.
- `Processes_ConformanceObservations` had zero rows for the run, so the system did not record that screenshot evidence was required but absent from process artifacts.

## Failure Chain

1. The process step contract said screenshots were required for UI surfaces, but the expectation text did not provide an exact artifact path.
2. The agent called browser tools and requested process-run filenames.
3. Browser MCP produced default provider-native `.playwright-mcp` files, while requested `artifacts/process-runs/...` browser files were not created.
4. Chat history persisted no browser function-call messages, so synthetic receipt enrichment could not reconstruct browser output files.
5. Artifact projection had no durable browser output source to import into `Processes_ArtifactRecords`.
6. The evidence pack markdown mentioned screenshots and console diagnostics, so the process accepted the step despite missing process-visible artifacts and weak interaction proof.

## Gaps

- Required browser proof artifacts are not expressed as typed, exact, process-visible obligations.
- Provider-native browser outputs can remain detached from process artifact records.
- QA acceptance can pass from textual claims and shallow interaction descriptions.
- Console diagnostics are not phase-classified, which makes both false negatives and false positives likely.
- Conformance observations do not flag missing screenshot artifacts, detached provider-native evidence, or invalid browser proof.
- There is no regression fixture matching the DB failure, so the same shape can recur.
