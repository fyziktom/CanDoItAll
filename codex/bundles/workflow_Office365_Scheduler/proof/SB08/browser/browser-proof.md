# SB08 Browser Proof

Command context: Playwright Core with system Chrome against the local CanDoItAll web app.

## Scheduler Page

- Route: Scheduler page.
- Desktop screenshots:
  - `bundle://proof/SB08/browser/scheduler-office365-form-desktop.png`
  - `bundle://proof/SB08/browser/scheduler-office365-configured-desktop.png`
  - `bundle://proof/SB08/browser/scheduler-raw-json-sync-after-change-desktop.png`
  - `bundle://proof/SB08/browser/scheduler-required-validation-desktop.png`
- Narrow screenshot: `bundle://proof/SB08/browser/scheduler-office365-form-narrow.png`
- Assertions:
  - Office365 Email Watch Summary To Project is selectable as a workflow target.
  - Typed fields render for Office365 connection, watched email address, project, parent node, processed category, and lookback hours.
  - Every-two-hours CRON preset updates the schedule.
  - Advanced JSON remains visible.
  - Raw JSON edits synchronize back to email, processed category, and lookback controls.
  - Clearing watched email blocks save with required-field validation.

## Workflows Page

- Route: Workflows page.
- Screenshots:
  - `bundle://proof/SB08/browser/workflows-templates-desktop.png`
  - `bundle://proof/SB08/browser/workflows-templates-narrow.png`
  - `bundle://proof/SB08/browser/workflows-office365-toolbox-expanded-desktop.png`
- Assertions:
  - Templates tab lists Office365 Email Watch Summary To Project and Office365 Email Watch Tasks To Project.
  - Seed marker 2026-05-office365-email-watch-schema-v1 is visible.
  - Workflow editor toolbox exposes Office365 mark processed, Office365 messages by category, and Office365 unprocessed message by address under Office365 Mail.

## Process Cleanup

- The local web app process started for browser proof was stopped after capture.
- Only the shared MCP shadow dotnet processes remained.
