# Normalized Requirements

## Requirements

- R001: The process workspace must avoid repeated full-detail reads for every active process run during live refresh.
- R002: Active-run summary loading must batch process-side counts for the active run set.
- R003: AgentFramework execution observation must avoid per-active-run storage scans where a single bounded scan can be reused.
- R004: Runs-tab refresh must not reload analytics unless the Analytics tab is active.
- R005: Existing process execution behavior, transitions, artifact validation, subprocess handling, manager directives, and process APIs must keep working.
- R006: Core-side timing must be captured before and after the optimization.
- R007: UI-side response timing must be captured with Playwright after the core optimization.
- R008: Browser proof must include the `/processes` route, viewport, actions, assertions, screenshot path, and result.

## Acceptance Criteria

- Active summary read model does not call `GetRunDetailsAsync` once per active run.
- A targeted test proves active-run health metrics for multiple runs are batched and mapped correctly.
- A targeted process test run passes.
- Local `/processes` route renders successfully after the change.
- Playwright timing is recorded in `reviews/01-execution-report.md`.
