# Normalized Requirements

| ID | Requirement |
| --- | --- |
| RQ01 | Preserve failed process run evidence and create a reproducible test. |
| RQ02 | Upgrade MAF from 1.3 to 1.6.x before process fixes. |
| RQ03 | Resolve exact package versions and migration changes from official sources/NuGet. |
| RQ04 | Keep CanDoItAll MAF adapter behavior stable: providers, sessions, tools, approvals, finalizer, logs, metrics, handoff, workflows. |
| RQ05 | Fix `StaleOrWrongRun` artifact binding for current-run workspace-written artifacts. |
| RQ06 | Normalize organization-scoped and run-scoped artifact paths consistently. |
| RQ07 | Populate/validate content hash for managed artifacts or classify missing content separately. |
| RQ08 | Use one shared artifact validation service for satisfaction read-model and finalizer completion gate. |
| RQ09 | Recover artifact-binding failures as actionable blocked/recoverable state, not opaque failed process termination. |
| RQ10 | Expose invariant diagnostics and validation details through API/UI. |
| RQ11 | Keep process runtime generic and protect non-software process templates. |
| RQ12 | Rerun the live Tetris/Blazor process only after MAF upgrade and artifact binding tests pass. |
