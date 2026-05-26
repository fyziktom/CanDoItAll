# SB15 Proof Manifest

## Status

Completed.

## Summary

SB15 prepares the Tetris Blazor WASM PWA UI run without executing the browser workflow. The runtime steps dialog now exposes stable test hooks and visible diagnostics for step operation contracts, branch selections, block reason codes, and recovery options. A component regression creates a strict Tetris-like process run and proves the first step is inspectable as non-mutating while still exposing branch and recovery diagnostics.

The actual browser run remains deliberately deferred to the downstream UI execution phase; this subbundle records the preflight checklist and selectors needed for that run.

## Semantic invariant

See `proof/SB15/semantic-invariants.md`.

## Production Behavior Artifact Matrix

| Signal / artifact | Producer | Consumer / lifecycle | Proof |
| --- | --- | --- | --- |
| Step operation-contract diagnostics | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunStepsDialog.razor` renders `data-operation-target-scope`, `data-allowed-operations`, and operation badges from `ProcessStepRunViewModel`. | Playwright and component tests can verify whether the selected step may mutate product files before starting the Tetris run. | `bundle://proof/SB15/transcripts/source-assertions.txt`; `bundle://proof/SB15/transcripts/passing.txt` |
| Stable step and branch hooks | The steps dialog emits `data-step-run-id` and `data-step-definition-id` on step cards, contract diagnostics, recovery diagnostics, and branch selectors. | The upcoming browser test can target exact runtime steps instead of brittle visible-text selectors. | `bundle://proof/SB15/transcripts/source-assertions.txt` |
| Block/recovery diagnostics | The steps dialog emits `data-block-reason-code` and `data-recovery-options` while rendering recovery badges. | Operators and tests can distinguish artifact recovery from generic blocked text during Tetris execution. | `bundle://proof/SB15/transcripts/passing.txt` |
| Tetris UI preflight checklist | `bundle://proof/SB15/tetris-ui-preflight-checklist.md` | SB16 and the next browser run use the same route, selector, screenshot, console, and artifact expectations. | `bundle://proof/SB15/tetris-ui-preflight-checklist.md` |

## Failing-first or adversarial proof

`proof/SB15/transcripts/failing-first.txt`

## Passing proof

`proof/SB15/transcripts/passing.txt`

## Source assertions

`proof/SB15/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB15/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB15/transcripts/changed-file-hashes.txt`

- `C06FE82800D70997BE30DBA5BB213E6C99F5391569A1D32BBD717FC5C6FA2BFF` `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunStepsDialog.razor`
