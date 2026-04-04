
# Canonical model review

## Scope statement

This review re-opened the earlier canonical-model bundle and re-ran the same architectural lens against the newer `CanDoItAll-crm-hr-module` snapshot, with special focus on the new CRM/HR integration surfaces.

## Executive verdict

The project still shows **strong architectural instincts** and a lot of real capability, but the CRM/HR wave made one thing much clearer:

> The right stabilization direction is **not** “node is only a view”.
>
> The right direction is **“node is the stable universal carrier for workbench-authored project thinking, while typed behavior, actor assignments, and projections get stricter owners around it.”**

That matters because the current codebase now combines all of these at once:

- graph-like project structure
- typed meeting / participant / work-item semantics
- CRM/HR party identity and staffing
- cross-module responsibility flows
- Gantt/calendar projections
- agent-facing project mutation surfaces

Without a cleaner canonical model, each new feature wave will stack more meaning onto already mixed ownership boundaries.

## Most important findings

- **ACR-001 Persisted system-managed workbench graph acts as a parallel truth** — ProjectWorkbenchService still synchronizes Projects, Resources, Factory, Validation, and TestLab canonical entities into persisted ProjectObjectRecord / ProjectObjectLinkRecord rows, and structure/calendar/Gantt reads still flow through that synced copy. The new CRM/HR overlays would now stack on top of a graph that is already a parallel truth.
- **ACR-005 Workbench reparent flow lacks explicit cycle and parent invariants** — Projects module rejects hierarchy cycles, but workbench reparent flow updates parent/link data without a visible equivalent invariant guard for node graph cycles or self-parenting.
- **ACR-012 Party responsibility truth is duplicated across node metadata, assignment tables, and module-local fields** — CRM/HR responsibility is now stored in more than one editable place. Participant, meeting, and work-item flows write both node metadata and project-party assignments, while Resources, Validation, and TestLab also store module-local responsible-party fields.
- **ACR-013 Node-scoped CRM/HR assignments use a soft NodeKey reference without canonical integrity checks** — ProjectPartyAssignment stores NodeKey as a plain string and SaveAssignmentAsync validates only project and party existence. There is no visible check that the referenced node exists, belongs to the same project, or allows the requested role.
- **ACR-014 Node reclassification and typed lifecycle history are insufficient for note→task/decision evolution** — The product workflow starts with fast brainstorming notes that later become structured tasks, decisions, or other typed nodes. Current reclassification mutates the same row in place, only supports note→block / block→block, and does not preserve typed transition history.

## Current facts

### 1. Workbench still persists a synchronized graph copy

Evidence:

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:348-376` calls `SyncGraphAsync` before structure reads.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:396-414` calls `SyncGraphAsync` before calendar reads.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1666-1943` assembles persisted `ProjectObjectRecord` / `ProjectObjectLinkRecord` rows from Projects, Resources, Factory, Validation, TestLab, and hierarchy data.
- `src/CanDoItAll.Modules.Workbench/ProjectGanttPreviewService.cs:10-17` builds Gantt from workbench structure output.

Interpretation:

- the system-managed graph copy is functioning as a **parallel truth / authoritative read surface**, not merely as an optional cache.

### 2. Node is currently both indispensable and overloaded

Evidence:

- `ProjectObjectRecord` in `ProjectWorkbenchModels.cs:26-60` currently holds:
  - identity
  - route
  - external artifact binding
  - storage/media fields
  - progress
  - marker columns
  - metadata JSON
  - parent key
  - X/Y
  - schedule
- `ProjectObjectMetadataEnvelope` in `ProjectWorkbenchMetadata.cs:165-190` packs many typed families into one JSON envelope.

Interpretation:

- node is already acting like the universal carrier in practice,
- but the current persistence shape is too overloaded to remain healthy as-is.

### 3. CRM/HR introduced duplicated node-level actor truth

Evidence:

- Participant metadata stores `LinkedPartyId` / `LinkedPartyName` in `ProjectWorkbenchMetadata.cs:279-302`.
- Work-item metadata stores `AssigneePartyId` / `AssigneePartyName` in `ProjectWorkbenchMetadata.cs:304-335`.
- Meeting metadata stores `RelatedParties` and `RelatedPartyNames` in `ProjectWorkbenchMetadata.cs:211-234`.
- UI party flows write metadata **and** `ProjectPartyAssignment` rows in `ProjectStructurePage.PartyIntegration.cs:240-307`, `325-352`, and `360-406`.
- Module-native responsibility fields still exist in:
  - `ResourceModels.cs:84-92`
  - `ValidationModels.cs:50-58`
  - `TestLabModels.cs:18-25`

Interpretation:

- the same conceptual truth (“who is linked/assigned/responsible”) now exists in multiple forms.

### 4. Node kinds and transitions are under-specified for the intended workflow

Evidence:

- `ProjectObjectType` is broad in `ProjectObjectContracts.cs:3-31`.
- subtype semantics and field definitions live heavily in `ProjectStructureCanvasCatalog.RichDefinitions.cs:132-144`.
- reclassification is only supported for `Note -> ProjectBlock` and `ProjectBlock -> ProjectBlock` in `ProjectWorkbenchModels.cs:2352-2358` and `ProjectStructurePage.NodeMutations.cs:107-125`.

Interpretation:

- the existing lifecycle does **not** model the real user workflow of brainstorming note → task / decision / richer typed node with preserved history.

### 5. Spatial semantics are canonical, but only half-modeled

Evidence:

- `PositionX` / `PositionY` are persisted in `ProjectObjectRecord`.
- marker data is split between legacy marker columns and metadata marker sets in `ProjectWorkbenchModels.cs:1399-1404` and `ProjectWorkbenchMetadata.cs:557-616`.
- user clarification states X/Y and markers are semantically meaningful, not merely decorative.

Interpretation:

- X/Y belongs on the canonical side of the model,
- but the owner is currently not explicit enough and marker truth is duplicated.

## Inferred intent

The code and the user clarification together imply the following intent:

1. **Mindmap-first authoring** is fundamental.
2. A node starts as the minimal unit of thinking and may become more typed later.
3. The project graph is not just presentation; it is the working medium.
4. CRM/HR is not a separate island; it is being woven into the graph.
5. Future agentic/semantic layers will rely on the graph being semantically stable.

## Recommended target direction

### Keep

- node as the stable carrier for workbench-authored units of thought
- semantically meaningful X/Y and marker signals
- module-native aggregates canonical in their modules
- assembled graph as the read model
- wave-based stabilization rather than big-bang rewrite

### Change

- move typed behavior into **facets**
- move semantics into **NodeKindRegistry**
- move node/project actor truth into one **canonical scoped owner**
- split containment, dependency, and association semantics
- stop treating persisted synchronized rows as authoritative read truth

## Why “node is only a view” is the wrong fix

That direction would break the actual working model:

- brainstorming would no longer start in the same canonical medium that later carries work,
- node transitions would require repeated create/invalidate/rebind operations across tables,
- spatial semantics would lose their natural home,
- history of thought would fragment across temporary records.

The right fix is **not to demote node**.

The right fix is to **discipline node**:

- stable identity
- narrow carrier
- typed facets
- explicit transition history
- explicit actor-assignment owner
- explicit spatial semantic owner

## Findings summary

| ID | Severity | Phase | Title |
| --- | --- | --- | --- |
| ACR-005 | Critical | Phase 0 | Workbench reparent flow lacks explicit cycle and parent invariants |
| ACR-011 | High | Phase 0 | Core canonical-invariant and projection-equivalence tests are missing |
| ACR-013 | High | Phase 0 | Node-scoped CRM/HR assignments use a soft NodeKey reference without canonical integrity checks |
| ACR-012 | Critical | Phase 1 | Party responsibility truth is duplicated across node metadata, assignment tables, and module-local fields |
| ACR-003 | High | Phase 1 | Type and subtype semantics are weak and partly owned by the UI catalog |
| ACR-004 | High | Phase 1 | Relation semantics are blurred and hierarchy is stored twice |
| ACR-001 | Critical | Phase 2 | Persisted system-managed workbench graph acts as a parallel truth |
| ACR-006 | High | Phase 2 | Calendar and Gantt are projections over a persisted projection |
| ACR-014 | High | Phase 2 | Node reclassification and typed lifecycle history are insufficient for note→task/decision evolution |
| ACR-002 | High | Phase 3 | ProjectObjectRecord is an overloaded universal box |
| ACR-008 | High | Phase 3 | Spatial semantics are canonical, but marker ownership is duplicated and under-modeled |
| ACR-015 | High | Phase 3 | Cross-module responsibility model is fragmented and lacks one canonical actor-assignment owner |
| ACR-007 | Medium | Phase 3 | Route, artifact binding, and storage/media concerns leak into node truth |
| ACR-009 | High | Phase 4 | ProjectWorkbenchService is an oversized orchestration hotspot |
| ACR-010 | Medium | Phase 4 | Lease scope granularity does not match mutation granularity |

## Canonical review conclusion

The new CRM/HR wave did not invalidate the previous bundle. It made it **more correct**.

The core stabilization move is now clearer than before:

1. node remains the stable workbench carrier,
2. but it must stop being a universal junk drawer,
3. and CRM/HR-linked actor truth must get one owner per scope before the next feature wave lands.
