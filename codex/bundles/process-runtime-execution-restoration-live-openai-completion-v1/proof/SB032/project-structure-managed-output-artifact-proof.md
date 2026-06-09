# SB032 Project-Structure Generated/Managed Output Artifact Proof

## Status
Completed.

## Objective
Prove that generated/managed process output artifacts project into project structure from scoped managed output paths.

## Source-Backed Proof
- Existing focused Playwright test: `Project_structure_process_run_output_SB012_INV_002_opens_project_processes_from_output_folder_node`
- Source: `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs`
- Fresh transcript: `bundle://proof/SB033/transcripts/project-structure-run-output-test.txt`

## Artifact Lifecycle Proven
- Artifact record route: `/api/processes/artifacts`
- Artifact kind: `Deliverable`
- Managed storage path shape: `output/scopes/project/{projectId}/process-runs/{runId}/SB012BrowserOutput/index.html`
- Projection assertion:
  - projected node id starts with `process-run-output:`,
  - projected node parent is `process-run:{runId}`,
  - projected node title includes `SB012BrowserOutput`.
- Navigation assertion:
  - primary quick action opens the process workspace for the same `processId` and `runId`,
  - selected run summary includes the source work item title.

## Browser Evidence
- `bundle://proof/SB033/screenshots/01-structure-run-output-node-large-desktop.png`
- `bundle://proof/SB033/screenshots/02-run-output-quick-actions-large-desktop.png`
- `bundle://proof/SB033/screenshots/03-run-output-process-workspace-large-desktop.png`

## Closure
SB032 is closed by a fresh Playwright proof that uses a scoped managed output path and verifies project-structure output-node projection plus run navigation.
