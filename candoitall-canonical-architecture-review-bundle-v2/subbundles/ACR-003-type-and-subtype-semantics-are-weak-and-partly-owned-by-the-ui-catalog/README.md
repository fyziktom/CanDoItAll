# ACR-003 — Type and subtype semantics are weak and partly owned by the UI catalog

- Severity: **High**
- Skill source: `canonical-model-review`
- Category: Naming / concept drift
- Phase: **Phase 1**
- Timing: **Now**
- Dependencies: Foundational for ACR-001, ACR-002, ACR-004, ACR-005, ACR-012, ACR-013, and ACR-014.

## Problem statement

ProjectObjectType is broad, subtypes are strings, metadata validation is shallow, and the canvas catalog carries substantial semantic truth about participants, work items, decisions, and other node kinds. CRM/HR role semantics are partly inferred from subtype strings rather than a canonical registry.

## Why this matters now

Party assignments, typed transitions, and agentic rules all become unstable until node kind semantics are canonical.

## Deliverables

- NodeKindRegistry with definition objects per kind/subtype
- Transition policy (e.g. note -> task / decision / block) owned by the registry
- Actor role policy per node kind
- UI catalog generated from registry or clearly derived from it

## Likely files touched

- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `tests/CanDoItAll.Tests.Unit/*NodeKind*`
