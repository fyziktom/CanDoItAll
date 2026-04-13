# Baseline characterization and live-gap reconciliation

## Status

- `Ready`

## Objective

- Establish a trusted baseline from the live repository, add the minimum characterization coverage needed for safe refactoring, and prove the bundle is execution-ready before architectural mutation work begins.

## Covered Inputs

- `U001` Review the live Process module rather than a stale narrative.
- `U004` Produce an execution-grade bundle Codex can actually run.
- `BRQ-001` Initiative-grade bundle structure.
- `BRQ-002` Live-repository grounding.
- `BRQ-015` Regression and proof discipline.

## Prerequisites

- None.
- The repository is available at `C:\repositories\CanDoItAll` when execution begins.

## Exact Source References

- C:\repositories\CanDoItAll\cdi_process_module_architecture_hardening_bundle\README.md
- C:\repositories\CanDoItAll\cdi_process_module_architecture_hardening_bundle\plan\01-phase-plan.md
- C:\repositories\CanDoItAll\cdi_process_module_architecture_hardening_bundle\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Publication.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessesToolsTests.cs

## Deliverables

- Prepared-stage validator pass on the target machine.
- A live-gap memo confirming which findings are already covered by current tests and which are still uncovered.
- Characterization tests or test updates for the current risky behaviors that later refactors must preserve.
- An updated execution report with the real starting gate state.

## Dependency Impact

- Every later subbundle depends on this baseline being trustworthy.
- Weak baseline proof here would make later regression claims suspect.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Run the prepared-stage bundle validator and repair the bundle structure if needed before any code change starts.
2. Map existing integration, component, and MCP tests to the major risk areas: canonicality, save behavior, publish behavior, runtime transition behavior, and workspace behavior.
3. Add the smallest necessary characterization tests for behaviors that later phases are expected to preserve but that are currently under-covered.
4. Record the baseline proof and uncovered-gap list in `reviews/01-execution-report.md` and, if needed, refine the later proof expectations.

## Scope Exceptions

- This phase does not refactor production code except for testability hooks or minimal test-only adjustments.
- This phase does not change architecture; it only protects the starting line.

## Do Not Do

- Do not start canonical model refactors yet.
- Do not rewrite services or UI in the name of cleanup during this phase.
- Do not rely on memory of earlier audits when the live repository says something different.

## Acceptance Checklist

- Prepared-stage validator passes.
- The risky current behaviors are mapped to explicit tests or an explicit uncovered-gap list.
- The execution report clearly states what proof exists and what still needs to be added in later phases.
- No production behavior has been changed beyond baseline/testability scaffolding.

## Proof Required

- Prepared-stage validator command from `09-proof-contract.md`.
- Focused test run proving the selected characterization tests pass.
- Updated `reviews/01-execution-report.md` showing the real baseline status.

## Browser Validation Logging

- N/A for this phase unless a baseline UI test gap must be covered immediately.
- If a UI characterization gap is closed here, record it in the execution report and treat it as baseline proof only, not final UI closure.

## Progression Gate

- The prepared-stage validator passes, the risky current behaviors are explicitly covered or listed as gaps, and later subbundles no longer need to guess what they are preserving.

## Suggested Agent Prompt

```text
Implement only subbundle 01. Do not refactor the architecture yet. Establish a trustworthy baseline, strengthen characterization coverage where it is currently too weak, run the prepared-stage validator, and update the execution report before any deeper work begins.
```
