# P10-001 — Load path still mutates persisted state via stale projection retirement and layout cleanup

Severity: **Critical**  
Gate: **HG-10-01**  
Module area: **Workbench**

## Problem
The active structure load path still deletes persisted rows during reads. That violates the read-only architecture promise and keeps the repo unsafe for the next plugin wave.

## Primary evidence
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:135`
  - `LoadAsync(...)` still calls `RetireLegacyProjectionRowsAsync(...)`.
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:167-175`
  - `LoadAsync(...)` still deletes stale layout overrides and calls `SaveChangesAsync(...)`.
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:361-388`
  - helper still deletes stale system-managed rows and calls `SaveChangesAsync(...)`.

## Required architectural end-state
`LoadAsync(...)` becomes a pure read/in-memory composition seam. No direct or transitive persistence mutation remains reachable from it.
