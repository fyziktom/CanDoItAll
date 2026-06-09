# SB008 Large-Desktop Process Launch Proof

## Status
Completed.

## Objective
Run focused Playwright proof on the global `/processes` route at large desktop size and capture the required template-selected, launch-plan, and run-selected screenshots.

## Browser Flow
The focused Playwright test `Process_start_SB015_INV_001_large_screen_imports_template_and_executes_ready_launch_from_ui` used a 1900 x 1200 viewport and proved:

- `/processes` returns a successful response.
- The template library opens from the global process workspace.
- The `Business plan development` process template can be selected and imported.
- The imported definition can be published.
- The Runs tab can create a launch plan from the UI.
- A ready launch plan can be executed from the UI.
- The resulting process run appears in the run history and selected-run summary.
- `#blazor-error-ui` is not visible at the end of the flow.

## Screenshots
| Required proof | Screenshot |
| --- | --- |
| Template selected | `bundle://proof/SB008/screenshots/01-template-selected-large-desktop.png` |
| Runs tab before launch | `bundle://proof/SB008/screenshots/02-runs-tab-before-launch-large-desktop.png` |
| Launch plan created | `bundle://proof/SB008/screenshots/02-launch-plan-created-large-desktop.png` |
| Run selected | `bundle://proof/SB008/screenshots/03-run-selected-large-desktop.png` |

## Validation
- Test transcript: `bundle://proof/SB008/transcripts/large-desktop-process-launch-playwright.txt`
- Test result: `bundle://proof/SB008/test-results/SB008-large-desktop-process-launch.trx`
- Source assertions: `bundle://proof/SB008/transcripts/large-desktop-process-launch-source-assertions.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB008/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No transient bundle path scan: `bundle://proof/SB008/transcripts/no-transient-bundle-path-scan.txt`
- No unexpected UI/media source drift scan: `bundle://proof/SB008/transcripts/no-unexpected-ui-media-drift-scan.txt`

## Changed Files
SB008 made no production source changes and no long-lived test source changes. It added only proof artifacts under `bundle://proof/SB008` and updated bundle execution documentation.
