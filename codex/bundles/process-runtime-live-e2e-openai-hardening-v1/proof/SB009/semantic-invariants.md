# SB009 Semantic Invariants

## Status
Completed.

## Invariant SB009_INV_001
The global `/processes` UI proof must start a real current process run through the launch-plan execution path and verify that run through API/service readback. A seeded baseline run, a template live-run profile, a stale screenshot, or report-only claim is not acceptable proof.

## Shallow-Pass Trap
A shallow implementation could open `/processes`, show a template, or display an old live-run profile and claim launch success. SB009 rejects that by requiring:

- A unique launch/run name generated at test time.
- A UI click on `processes-launch-execute-button`.
- A success notification for `Launch plan executed into a process run.`
- API readback from `/api/processes/runs?definitionId=...`.
- Selected-run summary assertions that contain the unique launch name and API-returned step count.

## Adversarial Negative Proof
`bundle://proof/SB009/transcripts/red-team-seeded-baseline-rejection.txt` rejects a fake proof that only cites a seeded baseline profile and an old screenshot. The fake proof missed all seven required real-run tokens, while the focused Playwright source missed none.

## Semantic Positive Proof
- Web build passed with 0 warnings and 0 errors: `bundle://proof/SB009/transcripts/web-build-no-restore.txt`
- Fresh large-desktop Playwright proof passed: `bundle://proof/SB009/transcripts/global-ui-real-run-playwright.txt`
- The Playwright test generated a unique run name, executed a ready launch from the UI, read the created run from the process API, and asserted the selected-run summary matched the API result.
- Source assertions found the real-run proof tokens in `AppSmokeTests.ProcessStartSmoke.cs`, `ProcessesApi.cs`, and process runtime sources: `bundle://proof/SB009/transcripts/global-ui-real-run-source-assertions.txt`
- Browser screenshots captured the required template selected, launch plan created, and run selected states under `bundle://proof/SB009/screenshots`.
- Anti-stub/runtime-host drift scan passed: `bundle://proof/SB009/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No transient bundle path scan passed: `bundle://proof/SB009/transcripts/no-transient-bundle-path-scan.txt`
- No unexpected UI/media source drift scan passed: `bundle://proof/SB009/transcripts/no-unexpected-ui-media-drift-scan.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| Unique launch name | Playwright flow | UI launch-plan form and API run query | Generated per test run to prevent stale or seeded proof reuse | `bundle://proof/SB009/transcripts/global-ui-real-run-source-assertions.txt` |
| Ready launch execution | `ProcessWorkspaceRunsLaunchSection` and `ProcessWorkspace.Launch` | `ProcessesService.ExecuteLaunchPlanAsync` | UI executes only after launch-plan readiness and delegates to normal run start | `bundle://proof/SB009/transcripts/global-ui-real-run-playwright.txt` |
| Process run API readback | `/api/processes/runs` | Playwright test and selected-run summary | API returns the current run matching the unique name | `bundle://proof/SB009/transcripts/global-ui-real-run-source-assertions.txt` |
| Selected-run UI summary | `ProcessWorkspaceRunsTab` | Browser proof | Displays the current run name and step count returned by API readback | `bundle://proof/SB009/screenshots/03-run-selected-large-desktop.png` |
| Seeded baseline rejection | Red-team proof | Critical gate | Rejects template profile/stale screenshot proof without unique current-run API readback | `bundle://proof/SB009/transcripts/red-team-seeded-baseline-rejection.txt` |

## Boundary
SB009 made no production source changes and no long-lived test source changes. It preserves the existing process-service launch path and does not introduce a generic process-driver runtime host, driver registry, selector, DI auto-registration, manager command, scheduler hook, workflow hook, process mutation through read-only drivers, or Process Core runtime orchestration.
