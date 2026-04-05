# P9-007 — Read-time normalization still performs writes in the hot load path

Severity: **High**  
Gate: **HG-06**  
Module area: **Workbench**

## Problem
Loading the graph still mutates persisted state. That hides unfinished migrations in the hot path, makes reads non-idempotent, and complicates concurrency and debugging. It also makes the final architecture harder to reason about.

## Required architectural end-state
Move normalization to a dedicated one-shot migration/repair step. After the repair passes, delete the write-on-read logic from LoadAsync.

## Primary evidence
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs` lines 154-166: LoadAsync(...) still calls binding and marker NormalizeAndHydrateAsync(...) on reads.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 158-238: NormalizeAndHydrateAsync(...) still persists changes via SaveChangesAsync.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs` lines 78-109: NormalizeAndHydrateAsync(...) still persists changes via SaveChangesAsync.
