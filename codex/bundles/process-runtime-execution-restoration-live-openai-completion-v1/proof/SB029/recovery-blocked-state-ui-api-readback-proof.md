# SB029 Recovery/Blocked-State UI And API Readback Proof

## Status
Completed.

## Objective
Prove that blocked-state recovery is visible in the run UI and preserved in API readback.

## API Readback Assertions
The Playwright proof reads `/api/processes/runs/{runId}?includeWorkBriefs=false&includeExecutionRuns=false&includeDirectMessages=false` after creating the blocked run.

Assertions:
- `run.status == Blocked`
- `health.recommendedAction == RecoverArtifactsOnly`
- single step has `status == Blocked`
- single step has `blockReasonCode == ArtifactContractUnsatisfied`
- single step has `nextRecoveryAction == RecoverArtifactsOnly`
- single step `recoveryOptions` contains `RecoverArtifactsOnly`
- recorded artifact id is present in the API artifact collection

## UI Readback Assertions
- Route: `/processes?processId={definitionId}&runId={runId}`
- Viewport: `1900x1200`
- The selected run summary renders `Blocked` and `recommended: Recover artifacts only`.
- The step recovery diagnostics element renders `ArtifactContractUnsatisfied` and `RecoverArtifactsOnly` as typed attributes, not only free text.
- The Evidence tab artifact ledger renders the satisfied artifact obligation and durable artifact record id.

## Proof Artifacts
- Browser/test transcript: `bundle://proof/SB030/transcripts/run-detail-recovery-ui-test.txt`
- Source assertions: `bundle://proof/SB030/transcripts/source-assertions.txt`
- Screenshots:
  - `bundle://proof/SB030/screenshots/01-selected-run-summary-large-desktop.png`
  - `bundle://proof/SB030/screenshots/02-step-recovery-diagnostics-large-desktop.png`
  - `bundle://proof/SB030/screenshots/03-artifact-ledger-large-desktop.png`
- TRX: `bundle://proof/SB030/SB030-run-detail-recovery-ui.trx`

## Negative Proof
`bundle://proof/SB030/red-team/shallow-ui-only-proof-rejected.md` rejects proof that only loads `/processes` or only asserts static text without API readback and typed recovery attributes.

## Closure
SB029 is closed by the same large-desktop Playwright/API proof used by Gate J because it explicitly covers blocked-state recovery and durable API readback.
