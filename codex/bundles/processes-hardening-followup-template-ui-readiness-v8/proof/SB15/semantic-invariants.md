# SB15 Semantic Invariants

- Invariant ID: SB15-INV-001
- Source raw note: F02 Blazor boundary visibility and RQ04 Tetris WASM PWA UI preflight.
- Expected behavior: the process runtime UI exposes enough stable, production-backed state to preflight a Tetris Blazor WASM PWA process run before the actual browser execution: step operation contracts, branch selectors, blocked diagnostics, recovery options, expected screenshot/console/artifact evidence, and route/selector checklist.
- Disallowed shallow implementation: a checklist with no UI selectors, selectors that only identify repeated generic cards, a UI that hides `AllowedOperations` or `OperationTargetScope`, hardcoded Tetris behavior in runtime components, or a test fixture that never renders the production steps dialog.
- Failing-first test: `bundle://proof/SB15/transcripts/failing-first.txt` describes the selector/diagnostic gaps that would fail the component regression.
- Passing test: `bundle://proof/SB15/transcripts/passing.txt` proves the production steps dialog renders contract, branch, block, and recovery diagnostics for a strict Tetris-like process.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunStepsDialog.razor`, `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`, and checklist proof listed in `bundle://proof/SB15/transcripts/changed-file-hashes.txt`.
- Production assertions: runtime step cards expose target scope, allowed operations, branch selectors, block reason codes, and recovery options through stable DOM attributes and visible diagnostics.
- Red-team negative case: a checklist-only or repeated-card selector cannot prove the first Tetris step is non-mutating.
- Downstream dependency check: SB16 and the next Tetris browser run can rely on stable selectors and diagnostics without inventing a separate UI proof surface.
- Required proof: failing-first/adversarial proof, passing component-path proof, source assertions, anti-stub audit, changed-file hashes, and the preflight checklist.

## Production Behavior Artifact Matrix

| Invariant surface | Required behavior | Negative case protected | Proof |
| --- | --- | --- | --- |
| First-step mutation visibility | The steps dialog renders target scope and allowed operations from the runtime view model. | The UI can claim a first step is non-mutating while exposing no machine-checkable operation contract. | `bundle://proof/SB15/transcripts/passing.txt` |
| Branch selector targeting | Branch selectors include stable step identifiers. | The browser test chooses a branch by repeated visible labels or by the wrong step. | `bundle://proof/SB15/transcripts/source-assertions.txt` |
| Block/recovery debugging | Block reason code and recovery options are visible and selector-addressable. | A blocked Tetris run cannot be diagnosed without reading logs or API payloads. | `bundle://proof/SB15/transcripts/passing.txt` |
| Browser-run handoff | The checklist defines route, viewport, selectors, screenshots, console proof, and artifact paths. | SB16 invents a new UI test flow that drifts from the implemented UI hooks. | `bundle://proof/SB15/tetris-ui-preflight-checklist.md` |
