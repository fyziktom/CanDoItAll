# SB08 Semantic Invariants

## Invariants

- `SB08-INV-001`: Missing usage, unknown usage, estimates, zero cost, and known actual cost must be distinct display states.
- `SB08-INV-002`: Unknown or missing provider usage must not render as precise actual `0.000000 USD`.
- `SB08-INV-003`: Process run detail must show invariant diagnostics and recommended action.
- `SB08-INV-004`: Step detail must show target scope, allowed operations, block code, next recovery, recovery options, and policy-denied context when available.
- `SB08-INV-005`: Workflow executor editor must show side-effect and preview/commit status for side-effecting executors.
- `SB08-INV-006`: Desktop and mobile browser proof must render process and workflow surfaces with no blocking console/page/network failures.

## Evidence

- `bundle://proof/SB08/transcripts/passing-component-adapters.txt`
- `bundle://proof/SB08/transcripts/passing-web-build.txt`
- `bundle://proof/SB08/transcripts/browser-proof-live-passing-attempt-3.txt`
- `bundle://proof/SB08/browser/browser-validation-summary.json`
- `bundle://proof/SB08/browser/workflow-executor-editor-mobile.png`
- `bundle://proof/SB08/browser/workflow-executor-editor-desktop.png`
- `bundle://proof/SB08/browser/live-process-detail-mobile.png`
- `bundle://proof/SB08/browser/live-process-detail-desktop.png`

## Residual Risk

The live process page content depends on whatever process runs exist in the local development database. The proof test handles an empty run list by recording workflow proof, but the captured run-detail screenshots in this pass include real active process cards with missing usage and process detail state.
