# P9-006 — Node reference model is still closed-world and requires core edits for each new relation

Severity: **High**  
Gate: **HG-05**  
Module area: **Workbench**

## Problem
Each new plugin-defined relation (for example email thread, LinkedIn account, external contact, connector-owned object) still requires new enum members, new fixed properties, and core code edits. That is not compatible with a real extension platform.

## Required architectural end-state
Move to an open reference model: namespace/key/target-kind/target-id/order/metadata, or an equivalent extensible facet model. Keep typed helpers at the edge, not as the core persistence contract.

## Primary evidence
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 8-22: ProjectNodeReferenceKind is still a fixed enum.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 59-67: ProjectNodeReferenceRecord.ReferenceId is still a Guid-only local identifier.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 108-148: ProjectNodeReferenceSet is still a fixed property bag for current relation kinds.
