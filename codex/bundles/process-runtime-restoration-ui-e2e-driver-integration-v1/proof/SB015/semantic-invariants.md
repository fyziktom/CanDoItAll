# SB015 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

The gate is not satisfied by opening `/processes`, by a route-only screenshot, by a template list item existing, or by a report-only claim. The focused smoke must import a real process template, publish it, create a UI launch plan, advance that plan to `Ready`, execute it through the UI, observe a persisted run through API, and select that run from Activity so the selected-run summary reflects the created run.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- `/processes` no longer renders `processes-workspace-shell`;
- the large-desktop browser context is replaced by a smaller viewport;
- the business-plan process template is missing from the template library;
- imported definitions are not persisted or have no steps;
- the launch plan controls are missing from the Runs > Launch tab;
- the launch lifecycle endpoints no longer transition a UI-created plan to `Ready`;
- the UI execute button no longer creates a durable process run;
- the run history no longer exposes the persisted run or selection no longer updates `processes-selected-run-summary`;
- the Blazor error UI becomes visible.

## Semantic Positive Proof

`bundle://proof/SB015/transcripts/focused-large-screen-process-start-playwright.txt` proves the real Playwright app fixture can run the large-desktop UI process-start smoke successfully. `bundle://proof/SB015/transcripts/large-desktop-screenshot-inventory.txt` records the generated visual artifacts, including the 1900x1200 template-selection capture and selected-run summary capture.

## Anti-Stub Proof

`bundle://proof/SB015/transcripts/anti-stub-no-small-viewport-scan.txt` proves the test uses the real browser fixture and API calls and rejects mock/test-server/fake/stub/bundle-path/sleep/small-viewport patterns in the new proof source.

## Raw-Note Closure

- RN-003 is partially closed for the `/processes` UI process-start path: the gate proves template import, launch plan creation, UI execution, and durable run selection. Project-structure process start remains planned for SB016-SB018.
- RN-008 is partially closed for this process-start route with large-desktop-only proof. Final large-screen coverage remains planned for SB046-SB048.

## Production Behavior Artifact Matrix

No new production signals were introduced in SB013-SB015. Existing route, template library, launch-plan, process API, and run history behavior is covered by source assertions, the focused Playwright test, and screenshots.
