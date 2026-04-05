# P9-002 — Binding layer still hydrates legacy carrier state back into the node runtime model

Severity: **Critical**  
Gate: **HG-01**  
Module area: **Workbench**

## Problem
Even after introducing a binding table, the runtime model still behaves as if the node owns binding truth. That preserves dual semantics and makes it easy for future code to accidentally depend on legacy carrier fields again.

## Required architectural end-state
Stop hydrating binding values into legacy node fields. Resolve / compose binding data only in binding-specific or projection-specific DTOs. Remove fallback-from-node logic entirely.

## Primary evidence
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 283-296: Apply(...) writes binding data back into node.Route / ExternalArtifact* / Media* / StorageObjectReferenceJson.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 356-366: ResolveBinding(...) still falls back from binding state to legacy carrier properties on the node.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 392-400: HasLegacyCarrierPayload(...) still treats the node carrier fields as active payload.
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs` lines 71-92: Projection assembly still copies legacy carrier values into node.Binding.
