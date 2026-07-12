# SB07 Fake-Proof Resistance Review

## Decision

`Pass`. Architecture, browser, final scoped validation, EF convergence, and the completed-stage bundle validator passed.

## Critical Subbundle Audit

| Subbundle | Shallow proof rejected | Durable negative/positive evidence | Result |
|---|---|---|---|
| SB01 | Descriptor files or duplicate contracts that merely compile | Real DI duplicate/mismatch/missing-implementation negatives plus build/integration proof in `bundle://proof/SB01/manifest.md` | Pass |
| SB02 | Renamed partials or copied MarkItDown/image implementations | Real-format conversion, delegation, failure/cancellation, and source-boundary proof in `bundle://proof/SB02/manifest.md` | Pass |
| SB04 | Registered but unused launch bridge, completion-only lifecycle, or idempotency key without enforcement | Caller/agent/process consumption, controllable lifecycle negatives, persistence, and crash-safe claim proof in `bundle://proof/SB04/manifest.md` and `bundle://proof/SB04/workflow-launch-idempotency.md` | Pass |
| SB05 | Event-JSON or recent-window arithmetic presented as analytics | Canonical observation production, immutable persistence, DB aggregate, unknown-pricing, API, and process-rollup proof in `bundle://proof/SB05/manifest.md` | Pass |
| SB06 | Catalog counts or screenshots without an interactive settings path | Component negatives plus production toolbox, trusted image settings, Gmail schema dialog, analytics, console review, and visual inspection in `bundle://proof/SB06/manifest.md` | Pass |

## Browser Evidence Review

- The four evidence files exist and their hashes are recorded in `bundle://proof/SB07/manifest.md`.
- Each capture has a named visual question and answer in `bundle://proof/SB06/browser-validation.md`.
- The browser proof records the exact large-screen viewport and intentionally excludes small/medium work.
- The fixed Gmail capture, not the earlier obscured dialog capture, is the closure artifact.

## Reopen Rules

- Reopen SB01 if descriptor/catalog/invocation identity or plugin metadata parity fails.
- Reopen SB02/SB03 if an executor copies a tool implementation or a planned unsafe command becomes runnable.
- Reopen SB04 if any caller bypasses the launch service or idempotency can reserve two runs.
- Reopen SB05 if analytics parse events, truncate totals to recent rows, or collapse unknown pricing into zero.
- Reopen SB06 if a custom renderer can be activated without trust metadata or a desktop dialog is obscured again.
- Reopen SB07 if the completed-stage validator or any final scoped validation gate regresses.
