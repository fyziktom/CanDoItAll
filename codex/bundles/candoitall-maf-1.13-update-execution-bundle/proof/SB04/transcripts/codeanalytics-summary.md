# CodeAnalytics SB04 Summary

Snapshot: `snap-20260708010020-ca7eff1f`

Scope:

- `CanDoItAll.AgentFramework.Hosting`
- `CanDoItAll.AgentFramework.Maf`
- `CanDoItAll.AgentFramework.Workflows.MafAdapter`
- `CanDoItAll.Modules.Processes`
- `CanDoItAll.Processes.Application`
- `CanDoItAll.Processes.Runtime`

Result:

- Snapshot build: pass.
- Blocking errors: none.
- Dependency cycles in scoped dependency query: none (`cycles: []`).
- `CanDoItAll.AgentFramework.Maf` references `CanDoItAll.AgentFramework.Workflows.MafAdapter`; it does not gain process project references.
- `CanDoItAll.AgentFramework.Workflows.MafAdapter` has no project references.
- `CanDoItAll.Modules.Processes` references `CanDoItAll.Processes.Application` and `CanDoItAll.Processes.Runtime`.
- `CanDoItAll.Processes.Application` references `CanDoItAll.Processes.Runtime`.
- `CanDoItAll.Processes.Runtime` has no project references.
- No process project references the MAF implementation projects in the scoped inventory.

Known diagnostics:

- Existing `Microsoft.OpenApi` 2.0.0 NU1903 advisory warnings surfaced during workspace load.
- Existing complexity findings on large MAF/process files remain; this update did not add a new runtime partial split or move process-domain behavior into MAF.
