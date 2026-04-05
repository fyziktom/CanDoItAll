# ACR-005 — Workbench reparent flow lacks explicit cycle and parent invariants

- Severity: **Critical**
- Skill source: `feature-block-architecture-review`
- Category: Invariant drift
- Phase: **Phase 0**
- Timing: **Now**
- Dependencies: Depends on ACR-011. Interacts with ACR-003 and ACR-004.

## Problem statement

Projects module rejects hierarchy cycles, but workbench reparent flow updates parent/link data without a visible equivalent invariant guard for node graph cycles or self-parenting.

## Why this matters now

This is a graph-integrity issue that can silently poison later refactors and agent mutations.

## Deliverables

- ProjectStructureInvariantService (or equivalent)
- Reparent/link/create guardrail tests for graph cycles and illegal parents
- Shared invariant checks across UI, MCP, and service entry points

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAgentService.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
