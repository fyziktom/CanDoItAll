# P9-003 — Marker truth is still dual represented and normalized from legacy scalar fields on reads

Severity: **High**  
Gate: **HG-02**  
Module area: **Workbench**

## Problem
You explicitly treat markers as canonical analytical data. With both scalar primary-marker fields and MarkersJson alive, drift is inevitable and downstream analytics / similarity models can be poisoned by inconsistent marker state.

## Required architectural end-state
Choose one canonical representation. The cleanest current path is to keep MarkersJson canonical and derive primary marker display data outside the persisted node entity. Remove scalar marker fields from persistence or demote them to non-persisted computed values only.

## Primary evidence
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` lines 40-43: ProjectObjectRecord still persists MarkerIcon / MarkerTone / MarkerLabel plus MarkersJson.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs` lines 43-76: ResolveLegacyJson(...) and HydrateLegacyFields(...) keep both representations active.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs` lines 78-109: NormalizeAndHydrateAsync(...) writes marker normalization during runtime.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` lines 19-22: Schema initializer still requires scalar marker columns.
