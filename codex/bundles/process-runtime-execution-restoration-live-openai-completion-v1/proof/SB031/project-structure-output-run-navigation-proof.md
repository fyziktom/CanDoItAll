# SB031 Project-Structure Output And Run Navigation Proof

## Status
Completed.

## Objective
Prove that project-structure output nodes can navigate back to the correct process run.

## Source-Backed Proof
- Existing focused Playwright test: `Project_structure_process_run_output_SB012_INV_002_opens_project_processes_from_output_folder_node`
- Source: `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs`
- Fresh transcript: `bundle://proof/SB033/transcripts/project-structure-run-output-test.txt`
- TRX: `bundle://proof/SB033/SB033-project-structure-run-output.trx`

## Behavior Proven
- Creates a real project through `/api/project-structure/projects`.
- Creates a real project-structure work item node.
- Creates, publishes, links, and starts a real process definition from that project node through `/api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start`.
- Records a managed output artifact with a scoped path under `output/scopes/project/{projectId}/process-runs/{runId}/SB012BrowserOutput/index.html`.
- Opens `/projects/{projectId}/structure`, waits for the projected output node, and proves:
  - parent id is `process-run:{runId}`,
  - projected node id starts with `process-run-output:`,
  - quick action opens the run-specific process workspace route,
  - popup URL is `/projects/{projectId}/processes?processId={definitionId}&runId={runId}`,
  - selected run summary includes the originating project work item title.

## Browser Evidence
- Viewport: `1900x1200`
- Screenshots:
  - `bundle://proof/SB033/screenshots/01-structure-run-output-node-large-desktop.png`
  - `bundle://proof/SB033/screenshots/02-run-output-quick-actions-large-desktop.png`
  - `bundle://proof/SB033/screenshots/03-run-output-process-workspace-before-history-wait-large-desktop.png`
  - `bundle://proof/SB033/screenshots/03-run-output-process-workspace-large-desktop.png`

## Closure
SB031 is closed by a fresh Playwright run that proves output-node projection and navigation to the correct run-specific process workspace.
