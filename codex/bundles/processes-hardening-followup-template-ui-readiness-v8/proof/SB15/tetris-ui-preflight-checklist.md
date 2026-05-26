# SB15 Tetris UI Preflight Checklist

This checklist prepares the browser run; SB15 intentionally does not execute the full UI flow.

## App and Route

- Start the web app with the normal PostgreSQL profile; do not switch to SQLite.
- Suggested local URL: `http://localhost:5095/processes` for global catalog validation, or `http://localhost:5095/projects/{projectId}/processes` for project-scoped runtime validation.
- Use a desktop viewport first, then a narrow viewport if layout defects appear.

## Setup

1. Import or verify the `blazor-app-delivery` process template.
2. Start the `baseline-blazor-wasm-pwa-tetris` run from the template baseline catalog.
3. Open the Runs tab and select the Tetris runtime run.
4. Open the runtime steps dialog from the Activity list.

## Required UI Assertions

- The steps dialog is present: `[data-testid='processes-run-steps-dialog']`.
- Runtime step cards are present: `[data-testid='processes-step-run-card']`.
- Each target step card has:
  - `data-step-run-id`
  - `data-step-definition-id`
  - `data-step-status`
  - `data-operation-target-scope`
  - `data-allowed-operations`
- First Tetris/intake step assertion:
  - `data-allowed-operations` must not contain `MutateProductTarget`.
  - Operation contract badges are visible through `[data-testid='processes-step-operation-contract']`.
- Implementation step assertion:
  - Product mutation is allowed only on implementation/repair steps that explicitly expose `MutateProductTarget`.
- Branch assertion:
  - Branch selector uses `[data-testid='processes-branch-outcome-select'][data-step-run-id='{stepRunId}']`.
  - Expected branch outcome: `Quality accepted` for validation acceptance.
- Blockage assertion:
  - Recovery diagnostics use `[data-testid='processes-step-recovery-diagnostics'][data-step-run-id='{stepRunId}']`.
  - `data-block-reason-code` and `data-recovery-options` must explain whether the run needs artifact recovery, upstream materialization, or operator action.

## Evidence to Capture in the Browser Run

- Screenshot of the selected Tetris run summary and step dialog.
- Screenshot of the first/intake step showing non-mutating operation contract diagnostics.
- Screenshot of validation/quality step branch outcome selection.
- Screenshot of the Evidence tab showing expected managed artifacts.
- Console capture proving no uncaught browser errors during the run.
- Network/API capture or exported run detail proving typed operation contracts, selected branch outcome, artifact records, block/recovery fields, and project-structure writeback evidence.

## Expected Artifact Paths

- Store browser screenshots under `bundle://proof/SB16/screenshots/` during the full UI execution phase.
- Store console/network transcripts under `bundle://proof/SB16/transcripts/`.
- Store any exported run detail or process evidence under `bundle://proof/SB16/artifacts/`.

## Exit Criteria for the Browser Run

- The Tetris process run can be inspected without relying on logs for step mutation boundaries.
- The first step is visibly and selector-proven non-mutating.
- Product mutation appears only on implementation/repair steps with explicit operation contracts.
- Required Tetris artifacts, screenshot proof, console proof, and project-structure writeback evidence are visible and linked to the runtime run.
