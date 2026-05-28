# SB13 Proof Manifest

## Status

Completed.

## Goal

Improve operator debugging of process runs.

## Shipped behavior

- `ProcessWorkspaceRunsOperatorConsoleSection.razor` now exposes operator run readback for recovery advice, manager resolution reason/confidence/candidates, artifact obligations, recorded roots, dispatch receipts, invariant diagnostics, approvals, escalations, rework, and attempt timeline.
- `ProcessWorkspace.RunsPresenter.cs` exposes the manager-resolution snapshot as scalar presenter properties so the operator console can display the same typed resolution state used by manager chat without leaking internal resolver types into the Razor surface.
- `ProcessWorkspaceTests.cs` now records a real managed artifact against a persisted runtime artifact expectation and asserts the new operator console sections render.
- Browser proof captured the rendered Control tab at `proof/SB13/browser/operator-console-control-tab.png`.

## Failing-first or adversarial proof

`proof/SB13/transcripts/failing-first.txt`

## Passing proof

`proof/SB13/transcripts/passing.txt`

## Source assertions

`proof/SB13/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB13/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB13/transcripts/changed-file-hashes.txt`

## Browser proof

`proof/SB13/transcripts/browser-validation.txt`

## Closure validator

`proof/SB13/transcripts/closure-validator.txt`
