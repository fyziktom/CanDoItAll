# SB01 Browser Evidence Contract And Storage

## Status

- `Ready`

## Objective

Make browser MCP proof durable and process-visible. Required screenshots, snapshots/DOM captures, console logs, and evaluate outputs must be imported or mirrored into the scoped process artifact root and represented by `Processes_ArtifactRecords`.

## Covered Inputs

- `N002`: "there are not screenshots evidences"
- `N005`: "this should not happen when I run complicated process like this"
- `R001`, `R002`, `R012`

## Prerequisites

- None. This is the first critical foundation.
- Read the DB facts in `bundle://inputs/01-source-artifacts.md`.
- Confirm whether existing execution detail, logs, tool receipts, or provider-native MCP outputs are the best authoritative source before editing.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ProviderNativeMcp.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.BrowserProof.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- A generic browser proof evidence contract or derivation that identifies required screenshot, console, snapshot/DOM, and evaluate/interaction outputs.
- Provider-native browser MCP output discovery that works when `InMemoryChatHistoryProvider.messages` is empty but durable execution logs or provider-native output files exist.
- Safe mirroring/import into scoped process artifact paths.
- `Processes_ArtifactRecords` for imported browser evidence, linked to artifact expectations or typed proof categories where possible.
- Actionable diagnostics when requested browser output cannot be found, copied, parsed, or recorded.

## Dependency Impact

- `SB02` cannot validate proof quality unless `SB01` gives it durable artifacts.
- `SB03` should not make process definitions demand exact browser artifacts until `SB01` can satisfy or fail those artifacts predictably.
- `SB04` live validation is untrustworthy if this subbundle can still pass with detached `.playwright-mcp` paths.

## Validation Depth

- `Critical foundation`
- Requires Semantic Adequacy Gate proof and artifact-backed proof manifest.

## Implementation Steps

1. Write a failing-first test that reproduces the current DB shape: expected screenshot text, browser MCP invocations, provider-native `.playwright-mcp` outputs, empty chat-history messages, and no process browser artifact records.
2. Identify the production source for browser tool output metadata. Prefer real receipts/results; if execution logs are used, parse them through a bounded typed parser and test malformed/irrelevant logs.
3. Implement safe import/mirroring into the scoped process artifact root.
4. Record process artifact records for screenshot, console, snapshot/DOM, and evaluate outputs.
5. Record conformance observations when an evidence reference or expected artifact cannot be resolved into a durable file.
6. Add unit and integration tests for successful import, missing file, ignored requested filename, unsafe path, and empty chat-history scenarios.
7. Update `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md`.

## Scope Exceptions

- Do not repair generated Tetris output.
- Do not require browser evidence for non-UI process steps.

## Do Not Do

- Do not add Tetris-specific logic.
- Do not make `.playwright-mcp` paths accepted as final process evidence without import/mirroring.
- Do not silently ignore missing files after a required evidence reference has been claimed.
- Do not rely only on markdown evidence-pack text.

## Acceptance Checklist

- A required screenshot expectation fails before the fix when no managed image artifact exists.
- The same scenario passes after the fix only when an actual image file is imported into the scoped artifact root and recorded.
- Empty chat-history messages no longer prevent browser evidence import when durable tool logs/results exist.
- Unsafe or missing provider-native output paths produce diagnostics and do not pass.
- No production process-runtime code checks Tetris-specific concepts.

## Proof Required

- `proof/SB01/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit.
- `proof/SB01/semantic-invariants.md` with shallow-pass trap, adversarial negative proof, semantic positive proof, raw-note literal closure, and production behavior artifact matrix.
- Targeted `dotnet test` commands for new unit/integration tests.
- A process artifact record query or integration assertion proving screenshot/console/snapshot records exist.

## Browser Validation Logging

- This subbundle does not require a live browser UI pass by itself, but it must log simulated/provider-native browser evidence paths in the execution report.
- Required analytics row: `SB01`, route `N/A fixture`, viewport `N/A fixture`, MCP evidence source `execution detail/logs/provider-native files`, screenshot path `scoped artifact path asserted by test`, result `Pending/Passed/Failed`.
- Screenshot review question: would a markdown-only screenshot mention pass this gate? The required answer is `No`.

## Progression Gate

- Do not start `SB02` or `SB03` until the failing-first fixture fails for the original condition and passes only when provider-native browser evidence becomes process-visible artifact records.
- The execution report must cite `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md`.

## Suggested Agent Prompt

```text
Implement SB01 only. Make provider-native browser evidence durable and process-visible without hardcoding product semantics. Start with failing-first tests for the DB failure shape, then implement the smallest typed ingestion/projection path that records scoped process artifacts and conformance observations. Update proof/SB01 before asking to progress.
```
