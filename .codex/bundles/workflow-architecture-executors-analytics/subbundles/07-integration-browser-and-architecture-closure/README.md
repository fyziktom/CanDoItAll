# Integration Browser And Architecture Closure

## Status

- `Completed`

## Objective

- Prove the full workflow system, close every raw note, and block completion on weak architecture, integration, analytics, plugin, or browser evidence.

## Success Criteria

- All preceding subbundle progression gates and proof manifests pass.
- End-to-end flows cover each launch origin, new/plugin executors, lifecycle, usage persistence/query/API/UI, cancellation/failure, and process lineage.
- Solution build and focused/unit/component/integration/Playwright suites pass.
- CodeAnalytics and architecture review show intended dependency direction, no new cycles, no fake separation, and no partial executor growth.
- Completed-stage bundle validator passes with all raw notes closed.

## Covered Inputs

- WF-TEST-01 and final closure of every normalized requirement/raw note.
- Architecture-governor, modular-refactoring, plugin, UI, analytics, and lifecycle proof obligations.

## Prerequisites

- SB01 through SB06 are completed with honest proof manifests and execution-report rows.
- Working tree is reviewed so unrelated user changes remain untouched.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `repo://tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/WorkflowShellSmokeTests.cs`
- `bundle://reviews/01-execution-report.md`
- `bundle://reviews/csharp-architecture-gate.md`
- `bundle://traceability/01-requirement-traceability.md`

## Deliverables

- Final regression tests and repaired defects found by integration/browser review.
- Maximized browser evidence and screenshot findings.
- Final CodeAnalytics dependency/cycle evidence and C# architecture review.
- Completed execution report, proof manifests/invariants, raw-note closure, and validator transcript.

## Dependency Impact

- This is the terminal closure phase; a failure reopens the owning subbundle rather than introducing a patch in SB07.

## Validation Depth

- `End-to-end regression and closure` across architecture, unit, component, integration, persistence, API, process, plugin, and browser layers.

## C# Architecture Impact

- No planned architecture changes. Any repair must be assigned to/reopen the owning earlier subbundle and re-run downstream proof.

## Boundary Ownership

- Review ownership follows `architecture/01-csharp-boundary-map.md`; closure does not create a miscellaneous integration service.

## Dependency Direction

- Compare final graph to `architecture/02-csharp-dependency-direction.md` and baseline snapshots; no new cycles or forbidden edges.

## Pattern Decision

- Validate PSR-01 through PSR-06 against production code. Reject patterns that exist only in tests/docs or have no real consumer.

## Testability Contract

- Tests must exercise production DI, stores, APIs, plugin manifests, browser route, and controllable negative cases. No fabricated catalog/projection proof.

## Partial Class Policy

- Architecture review blocks partial executor classes and behavior extraction that merely moves methods into partial files.

## Architecture Proof Required

- Run the `csharp-architecture-review-gate` skill, CodeAnalytics dependency/cycle audit, anti-service-locator/partial/duplicate-operation search, and direct-test inventory.

## Implementation Steps

1. Validate all subbundle manifests/invariants and raw-note traceability.
2. Run focused unit/component/integration tests and full solution build.
3. Run persistence/API/process/plugin end-to-end matrices and repair by reopening owners.
4. Run maximized browser workflow scenarios, capture/review screenshots, console, and network.
5. Run final CodeAnalytics and C# architecture review gate.
6. Complete execution report and run completed-stage bundle validator.

## Scope Exceptions

- No small/medium browser or responsive-design pass.
- Pre-existing unrelated Module.AgentFramework cycles remain baseline exceptions only if unchanged and untouched.

## Do Not Do

- Do not waive failed tests, weak semantic proof, missing browser evidence, or new architecture findings.
- Do not update proof documents to claim behavior not present in production.
- Do not fold unrelated refactors into closure.

## Acceptance Checklist

- Every raw note is completed with source/test/browser proof or an explicit user-approved blocker.
- All runnable executors are in production catalog/invoker/UI and settings round trip.
- All launch origins and analytics producer/consumer paths work.
- Builds/tests/browser/architecture/validators pass.
- Execution report is sufficient for a new agent without conversation context.

## Proof Required

- Full build and selected test transcripts with exit codes and test names.
- CodeAnalytics graph/cycle report and architecture gate review.
- Browser action/assertion log, screenshots, console/network review.
- Completed raw-note table, semantic evidence, manifests, and completed-validator transcript.
- `bundle://proof/SB07/manifest.md` and `bundle://proof/SB07/semantic-invariants.md` during execution.

## Browser Validation Logging

- Route: `/agents/workflows` (non-artifact local context); viewport: maximized 1600x1000 only.
- Repeat new built-in/plugin executor create/edit/save/reload and run analytics scenario using production DI/store.
- Assert duration/provider/model/all token dimensions/known cost/unknown count and no paged-total truncation.
- Reuse the reviewed production captures `repo://workflow-executors-markdown.png`, `repo://workflow-custom-image-settings.png`, `repo://workflow-plugin-gmail-settings-fixed.png`, and `repo://workflow-analytics-desktop.png`; do not create duplicate evidence files.
- Review clipping/overlap, component consistency, state persistence, diagnostics, console, and failed requests.

## Progression Gate

- Passed. The solution build, scoped unit/component/integration suites, PostgreSQL idempotency proof, EF model convergence, final architecture snapshot, 1600x1000 browser review, and completed-stage bundle validator are recorded in `bundle://proof/SB07/manifest.md`.

## Suggested Agent Prompt

```text
Implement SB07 closure only. Treat every earlier gate as binding, reopen the owner of any defect, run production end-to-end and maximized browser proof, perform the C# architecture review, and do not claim completion until the completed validator and raw-note audit pass.
```
