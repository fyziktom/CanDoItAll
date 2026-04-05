# ACR-010 — Lease scope granularity does not match mutation granularity

- Severity: **Medium**
- Skill source: `architecture-drift-audit`
- Category: Runtime / operational drift
- Phase: **Phase 4**
- Timing: **Later**
- Dependencies: Defer until ACR-005 and ACR-011 are implemented and the graph mutation rules are stable.

## Problem statement

Scope kinds include ProjectNode, but many agent mutation flows still take project-wide leases, which may over-serialize work and obscure future permission boundaries.

## Why this matters now

Relevant for multi-agent future, but not the first canonical-model blocker.

## Deliverables

- Lease scope policy matrix
- Telemetry around lease contention
- Selective node-scope adoption

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectStructureAgentContracts.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureLeaseService.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAgentService.cs`
- `tests/CanDoItAll.Tests.Integration/*`
