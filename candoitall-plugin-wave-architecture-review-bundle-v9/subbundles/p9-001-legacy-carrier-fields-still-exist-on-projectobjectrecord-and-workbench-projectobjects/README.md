# P9-001 — Legacy carrier fields still exist on ProjectObjectRecord and Workbench_ProjectObjects

Severity: **Critical**  
Gate: **HG-01**  
Module area: **Workbench**

## Problem
The node carrier is still polluted by transport / binding concerns. New plugins will keep pushing external-identity details into the core node entity, so the universal carrier remains leaky and migrations remain expensive.

## Required architectural end-state
Retire the legacy carrier fields and DB columns from ProjectObjectRecord / Workbench_ProjectObjects. Keep binding state only in ProjectNodeBindingRecord (or an equivalent binding facet table) and compose it only in dedicated read models at the edge.

## Primary evidence
- `src/CanDoItAll.Modules.Workbench/ProjectObjectRecord.LegacyCarrier.cs` lines 3-17: Route / ExternalArtifact* / Media* / StorageObjectReferenceJson still live on ProjectObjectRecord.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` lines 10-26: Legacy carrier columns are still declared as required ProjectObject columns.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` lines 50-67: Workbench_ProjectObjects CREATE TABLE still persists legacy carrier columns.
