# SB04 Semantic Invariants

## Invariant SB04_INV_001
- Invariant ID: `SB04_INV_001`
- Source raw note: RN-001 requires checking whether process flows still work like before, and RN-004 requires stabilization before more runtime extraction.
- Expected behavior: A project-structure-linked process template can launch from the browser, resolve assignments, complete through automation dispatch, show completed status, show artifact/evidence readback, show completed/skipped steps, and expose runtime-host operator readback for the completed run.
- Disallowed shallow implementation: Reusing a non-project-scoped run, relying only on API success without browser screenshots, or proving runtime-host readback only on a blocked/manual recovery scenario while omitting the completed project-structure run.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt` proves baseline `HEAD` lacked the completed-run runtime-host readback screenshot token.
- Passing test: `bundle://proof/SB04/transcripts/playwright-project-structure-completed-run.txt` exits zero after asserting completed status, artifacts/evidence, completed steps, and runtime-host operator readback.
- Changed source files: `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs` before SHA-256 `db3f59b5c6a7296839864674bed77369f54665651a4535aaf7551273e802194a`, after SHA-256 `5ce79272eaf506274e8881c807d7d9a0dc7bfa7e9cac2fdf7fb94481b1c5b3b0`.
- Production assertions: `bundle://proof/SB04/screenshots/06-project-run-completed-summary-large-desktop.png`, `bundle://proof/SB04/screenshots/07-project-run-artifacts-readback-large-desktop.png`, `bundle://proof/SB04/screenshots/08-project-run-runtime-host-readback-large-desktop.png`, and `bundle://proof/SB04/screenshots/09-project-run-completed-steps-large-desktop.png` capture the operator-visible proof.
- Red-team negative case: `bundle://proof/SB04/transcripts/source-assertions.txt` prevents losing the completed-run runtime-host assertion while still passing older launch/artifact screenshots.
- Downstream dependency check: SB05 may proceed because the UI path is green and there is no remaining browser/UI blocker.
