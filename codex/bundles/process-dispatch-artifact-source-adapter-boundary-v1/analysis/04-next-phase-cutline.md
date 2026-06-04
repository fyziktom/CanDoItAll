# Next Phase Cutline

This bundle may create internal helper/adaptor classes under `CanDoItAll.Modules.Processes/Automation/Dispatch` or a nearby Processes-owned runtime folder.

Allowed:

- Internal DTO snapshots for artifact expectations, projection sources, and write requests.
- Pure source adapters for artifact projection sources.
- A small write coordinator/facade for storage placement + `RecordArtifactAsync` delegation.
- Tests and architecture guardrails proving behavior parity.

Forbidden:

- `CanDoItAll.Processes.Core` project.
- Process driver packs.
- EF entity movement.
- UI/Razor movement.
- MAF/Tooling reference to product modules.
- Small/medium/mobile proof.
