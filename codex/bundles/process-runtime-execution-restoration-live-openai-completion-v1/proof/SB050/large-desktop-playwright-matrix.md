# SB050 Large-Desktop Playwright Matrix

## Status
Completed.

## Objective
Run the release-candidate browser matrix on the required large desktop viewport.

## Browser Matrix
| Flow | Route | Viewport | Actions and assertions | Screenshots |
| --- | --- | --- | --- | --- |
| Process start smoke | `/processes` and `/processes?processId={definitionId}&launchPlanId={launchPlanId}` | 1900x1200 | Imports the business-plan template, publishes it, creates a launch plan through the UI, approves/provisions through API, executes the ready launch from UI, opens run details, and asserts no Blazor error UI. | `bundle://proof/SB050/screenshots/process-start-smoke/01-template-selected-large-desktop.png`; `bundle://proof/SB050/screenshots/process-start-smoke/02-runs-tab-before-launch-large-desktop.png`; `bundle://proof/SB050/screenshots/process-start-smoke/02-launch-plan-created-large-desktop.png`; `bundle://proof/SB050/screenshots/process-start-smoke/03-run-selected-large-desktop.png` |
| Run detail recovery | `/processes?processId={definitionId}&runId={runId}` | 1900x1200 | Creates a blocked run through API, writes an artifact, verifies API health, opens the run UI, asserts blocked/recovery/artifact readback, and asserts no Blazor error UI. | `bundle://proof/SB050/screenshots/process-run-detail-recovery-sb030/01-selected-run-summary-large-desktop.png`; `bundle://proof/SB050/screenshots/process-run-detail-recovery-sb030/02-step-recovery-diagnostics-large-desktop.png`; `bundle://proof/SB050/screenshots/process-run-detail-recovery-sb030/03-artifact-ledger-large-desktop.png` |
| Project-structure run output | `/projects/{projectId}/structure` to `/projects/{projectId}/processes?processId={definitionId}&runId={runId}` | 1900x1200 | Starts a process from a project-structure node, records an output artifact, waits for output-node projection, opens quick actions, opens the process workspace, and asserts selected run readback and no Blazor error UI. | `bundle://proof/SB050/screenshots/project-structure-run-output-sb012/01-structure-run-output-node-large-desktop.png`; `bundle://proof/SB050/screenshots/project-structure-run-output-sb012/02-run-output-quick-actions-large-desktop.png`; `bundle://proof/SB050/screenshots/project-structure-run-output-sb012/03-run-output-process-workspace-before-history-wait-large-desktop.png`; `bundle://proof/SB050/screenshots/project-structure-run-output-sb012/03-run-output-process-workspace-large-desktop.png` |

## Test Proof
- Playwright transcript: `bundle://proof/SB050/transcripts/large-desktop-playwright-matrix.txt`
- Playwright TRX: `bundle://proof/SB050/SB050-large-desktop-playwright.trx`
- Screenshot inventory: `bundle://proof/SB050/transcripts/screenshot-inventory.txt`

## Result
The focused Playwright matrix passed with 3 tests, 0 failures, 0 skipped. Eleven screenshots were copied into the bundle proof folder.
