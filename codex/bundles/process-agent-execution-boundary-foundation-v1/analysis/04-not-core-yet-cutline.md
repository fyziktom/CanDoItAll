# Not-Core-Yet Cutline

## Allowed In This Bundle

- Add `CanDoItAll.Processes.Contracts` or `CanDoItAll.Processes.Abstractions` only as a minimal boundary project.
- Add process automation execution facade/client.
- Move direct execution start/detail/adoption/recovery calls from dispatcher partials to the facade.
- Add source scans and architecture tests.
- Add process-filtered integration proof.
- Add large-screen-only validation instructions.

## Not Allowed In This Bundle

- No full `CanDoItAll.Processes.Core`.
- No EF entity migration.
- No domain driver packs.
- No DotNet/SWDev/Rust/business-analysis drivers.
- No broad dispatcher rewrite.
- No mobile/small/medium UI validation.
- No unrelated docs or bundle-history churn.

## Exit Condition

After this bundle, it should be possible to plan real process-core extraction with much lower risk because AgentFramework execution behavior will be isolated behind a process-facing boundary.
