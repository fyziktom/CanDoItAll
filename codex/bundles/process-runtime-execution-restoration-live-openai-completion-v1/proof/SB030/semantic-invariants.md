# SB030 Semantic Invariants

## Status
Completed.

## Invariant SB030_INV_001
- Invariant ID: `SB030_INV_001`
- Source raw note: run detail/recovery UI must prove real runtime restoration, not report-only completion.
- Expected behavior: A process run selected by `runId` displays persisted blocked status, typed recovery diagnostics, and artifact evidence in the large-desktop UI; the same state is available through the process run detail API.
- Disallowed shallow implementation: Loading `/processes` without a selected run, asserting static text only, skipping API readback, skipping typed recovery attributes, or claiming screenshots without persisted artifact evidence.
- Failing-first/negative proof: `bundle://proof/SB030/red-team/shallow-ui-only-proof-rejected.md`
- Passing test: `Process_run_detail_recovery_SB030_large_screen_displays_blocked_recovery_and_artifact_readback` passed in `bundle://proof/SB030/transcripts/run-detail-recovery-ui-test.txt`.
- Changed source files: Playwright proof test only, captured in `bundle://proof/SB030/manifest.md`.
- Production assertions: `bundle://proof/SB030/transcripts/source-assertions.txt`
- Downstream dependency check: Project-structure output/navigation proof may start because run detail and blocked recovery readback are browser-proven.

## Shallow-Pass Trap
A fake Gate J closure could cite the existing `/processes` page, a run-history card, or a static screenshot. SB030 rejects that by requiring public API setup/readback, selected run route loading, typed recovery attributes, and artifact-ledger evidence.

## Semantic Positive Proof
- `bundle://proof/SB030/transcripts/run-detail-recovery-ui-test.txt`
- `bundle://proof/SB030/transcripts/source-assertions.txt`
- `bundle://proof/SB030/screenshots/01-selected-run-summary-large-desktop.png`
- `bundle://proof/SB030/screenshots/02-step-recovery-diagnostics-large-desktop.png`
- `bundle://proof/SB030/screenshots/03-artifact-ledger-large-desktop.png`

## Adversarial Negative Proof
- `bundle://proof/SB030/red-team/shallow-ui-only-proof-rejected.md`

## Anti-Stub Audit
- `bundle://proof/SB030/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB030/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No matches for active bundle paths in `src`/Playwright source and no matches for execution-capable process runtime driver host surfaces.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Selected blocked run | Public process API | Run summary and API detail | Started, transitioned, selected by query, rendered as `Blocked` | Static `/processes` load is rejected |
| Typed recovery diagnostics | Step transition/read query | Run steps dialog | `ArtifactContractUnsatisfied` and `RecoverArtifactsOnly` are asserted through API and data attributes | Text-only proof is rejected |
| Artifact ledger record | Step artifact API | Evidence tab | Recorded artifact satisfies the expectation and renders durable artifact id | Screenshot-only artifact claim is rejected |
