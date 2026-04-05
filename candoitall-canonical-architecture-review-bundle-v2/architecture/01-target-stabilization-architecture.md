
# Target stabilization architecture

## Core decision

The target architecture for CanDoItAll is:

- **Node stays the stable universal carrier for workbench-authored project thinking**
- **Node is not merely a view**
- **Typed behavior moves into explicit facets and policies**
- **Module-native aggregates stay canonical in their own modules**
- **The assembled graph becomes the single read model**
- **Spatial semantics remain canonical**
- **Viewport state remains UI-only**

This is the smallest design that respects the user’s real workflow.

## Why this is the right direction

A workbench-native node is the natural carrier for:

- quick notes
- brainstorm fragments
- later-promoted tasks
- later-promoted decisions
- manually curated project structure

If node were reduced to a disposable wrapper, the system would lose:

- continuity of thought
- continuity of XY placement and markers
- continuity of links and project context
- cheap brainstorming followed by later refinement

So the stabilization goal is **not to demote node**. It is to **discipline node**.

## Target model

### 1. Workbench-native node carrier

The carrier owns only stable workbench-native truth such as:

- node identity
- project identity
- current node kind
- core text fields that are always meaningful (title/body/summary)
- canonical containment reference (or equivalent canonical parent owner)
- timestamps / deletion state
- backing/origin metadata

It must **not** keep swallowing:

- storage/media transport details
- route/navigation hints
- marker duplication
- arbitrary typed payload families
- every future integration concern

### 2. Node kind registry

`NodeKindRegistry` (or equivalent) becomes the owner of:

- allowed transitions
- allowed relation kinds
- actor-role policy
- time semantics
- execution semantics
- UI descriptors and field schema
- whether the kind is workbench-native or external-projection-only

This replaces the current split across enum, subtype strings, metadata families, and UI catalog code.

### 3. Typed facets

Typed node behavior moves into explicit facet records/classes, for example:

- `TaskFacet`
- `DecisionFacet`
- `MeetingFacet`
- `ParticipantFacet`
- `RepositoryFacet`
- `FileFacet`
- `PromptFacet`
- `EnvironmentFacet`
- etc.

A workbench-native node has one stable identity and may gain/retire facets over time according to transition policy.

### 4. Node transition history

Introduce an explicit history owner, e.g. `NodeTransitionHistory`, that records:

- node identity
- previous kind
- new kind
- timestamp
- actor/source
- optional reason / note / snapshot reference

This preserves the story of brainstorming → structured work without forcing destructive delete/recreate.

### 5. Canonical spatial semantics

Because the user explicitly relies on mindmap semantics, the target keeps:

- X
- Y
- possibly ordering / branch side / future spatial attributes
- semantic markers

on the canonical side of the model.

Recommended split:

- `NodeSpatialSemanticState` (canonical)
- `NodeMarkerSet` (canonical)
- `ViewState` (zoom, pan, selection, UI expansion, ephemeral editor state only)

### 6. Canonical actor-assignment model

Introduce a scoped actor-assignment ownership model.

Important distinction:

- `Party` / `AI agent profile` remain identity sources
- `ProjectActorAssignment` (or equivalent) owns cross-cutting project/node assignment truth
- module-native aggregates may retain ownership in some scopes during the migration wave, but only via an explicit ownership matrix

The bundle recommends these scope categories:

- `Project`
- `Node`
- later if needed:
  - `Resource`
  - `ValidationRun`
  - `TestPlan`
  - `PromptRun`
  - other aggregate scopes

### 7. External/module-native projections

Projects, Resources, Factory runs, Validation runs, Test plans, and similar module-native aggregates remain canonical in their own modules.

They should appear in the graph as **assembled external projections**, not as persisted system-managed workbench truth.

That creates a clean rule:

- **Workbench-native nodes can evolve via node-kind transitions**
- **External projection nodes cannot be casually reclassified**
- if the user wants a follow-up task/decision from an external projection node, create a **derived workbench-native node** linked back to the external projection

### 8. Assembled graph

`CanonicalGraphAssembler` (or equivalent) builds one read model:

- workbench-native nodes and their facets
- canonical external projections from other modules
- canonical containment and non-hierarchy edges
- scoped actor overlays
- derived projection-ready schedule/spatial metadata

Outputs may look like:

- `AssembledProjectGraph`
- `AssembledNode`
- `AssembledEdge`
- `AssembledActorLink`

### 9. Projections

All projections consume the assembled graph:

- structure/mindmap view
- calendar
- Gantt/timeline
- Mermaid
- summaries
- future semantic retrieval / analytics

If a cache exists, it is disposable and non-authoritative.

## Explicit answers to the user’s design questions

### Should node be the center?

**Yes, for workbench-authored project thinking.**

But not in the current overloaded form.

The right target is:

- stable node carrier
- typed facets around it
- canonical spatial semantics around it
- canonical actor links around it
- explicit transition history around it

### Should changing block type invalidate one record and create another?

**Not by default for workbench-native nodes.**

Default rule:

- keep the same node identity
- add a transition history record
- retire/create facets as needed
- preserve spatial semantics
- preserve or intentionally remap actor links according to policy

This gives the wanted history without fragmenting the user’s graph.

### What about text history and brainstorm traces?

Keep them.

Text is cheap.
Continuity is valuable.
This is especially true for later analytics, semantic retrieval, and agentic improvement workflows.

## Minimum viable target families

- `NodeCarrier`
- `NodeKindDefinition`
- `NodeKindRegistry`
- `NodeContainment` (or canonical parent owner)
- `NodeEdge`
- `NodeSpatialSemanticState`
- `NodeMarkerSet`
- `NodeScheduleFacet`
- `NodeTransitionHistory`
- `NodeFacet_*`
- `ProjectActorAssignment` / `NodeActorLink`
- `CanonicalGraphAssembler`
- `AssembledProjectGraph`

## Guardrails

1. Node must not collapse back into a mixed-role junk drawer.
2. Spatial semantics must not get demoted into disposable UI state.
3. Metadata/display names must not remain authoritative for live actor assignment truth.
4. External projections must not quietly become editable canonical workbench truth.
5. Transition history must preserve the user’s mindmap-first workflow.
