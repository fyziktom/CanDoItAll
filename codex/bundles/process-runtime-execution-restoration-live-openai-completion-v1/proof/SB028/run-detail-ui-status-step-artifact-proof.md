# SB028 Run Detail UI Status/Step/Artifact Proof

## Status
Completed.

## Objective
Prove that run detail UI surfaces selected run status, step diagnostics, and artifact evidence for a durable process run.

## Source-Backed Proof
- Added focused Playwright coverage in `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`:
  - `Process_run_detail_recovery_SB030_large_screen_displays_blocked_recovery_and_artifact_readback`
- The test creates a real process definition through `/api/processes/definitions`, publishes it, starts a run through `/api/processes/runs/start`, blocks the step through `/api/processes/runs/{runId}/steps/{stepRunId}/transition`, records an artifact through `/api/processes/runs/{runId}/steps/{stepRunId}/artifacts`, then opens the run detail route.
- Browser route: `/processes?processId={definitionId}&runId={runId}`
- Viewport: `1900x1200` large desktop only.

## Browser Assertions
- Selected run summary contains the generated run name.
- Selected run summary contains `Blocked`.
- Selected run summary contains `recommended: Recover artifacts only`.
- Run steps dialog renders `processes-step-recovery-diagnostics` with `data-block-reason-code="ArtifactContractUnsatisfied"`.
- Run steps dialog renders `data-recovery-options` containing `RecoverArtifactsOnly`.
- Evidence tab artifact ledger renders `SB030 blocked recovery evidence`, `Satisfied`, and the durable artifact record id.

## Screenshot Evidence
- `bundle://proof/SB030/screenshots/01-selected-run-summary-large-desktop.png`
- `bundle://proof/SB030/screenshots/02-step-recovery-diagnostics-large-desktop.png`
- `bundle://proof/SB030/screenshots/03-artifact-ledger-large-desktop.png`

## Test Transcript
- `bundle://proof/SB030/transcripts/run-detail-recovery-ui-test.txt`
- TRX: `bundle://proof/SB030/SB030-run-detail-recovery-ui.trx`

## Source Assertions
- `bundle://proof/SB030/transcripts/source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB030/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB030/transcripts/anti-stub-and-runtime-host-drift-scan.txt`

## Closure
SB028 is closed by a large-desktop Playwright proof that exercises real public API setup/readback and browser-visible run detail UI. This is not report-only proof.
