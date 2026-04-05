# ADR candidates

## ADR-001 — Node remains the stable canonical carrier for workbench-authored graph thinking

**Decision**

Do not demote node to a mere view. Preserve stable node identity across brainstorming, refinement, and typed transitions.

**Why now**

Linked findings: 

## ADR-002 — Assembled canonical graph replaces persisted system-managed graph truth

**Decision**

External/module-native aggregates remain canonical in their own modules and are assembled into the graph on read.

**Why now**

Linked findings: 

## ADR-003 — NodeKindRegistry owns kind semantics, transitions, relation policy, actor policy, and UI descriptors

**Decision**

Subtype strings and UI catalogs may not be the source of truth.

**Why now**

Linked findings: 

## ADR-004 — Containment, dependency, and association semantics are separate concepts

**Decision**

Do not encode hierarchy twice or treat ancestor chain as generic dependency by default.

**Why now**

Linked findings: 

## ADR-005 — Canonical actor-assignment ownership matrix

**Decision**

Project-scoped and node-scoped responsibility must have one owner; module-native aggregate responsibility gets an explicit ownership matrix during migration.

**Why now**

Linked findings: 

## ADR-006 — Spatial semantics are canonical; viewport state is not

**Decision**

XY layout and semantic markers remain meaningful domain-adjacent data; zoom/pan/selection stay in view state.

**Why now**

Linked findings: 

## ADR-007 — Stable node identity with typed facet history

**Decision**

Note→task/decision/etc. is modeled as a transition on the same node plus facet lifecycle/history, not destructive replacement.

**Why now**

Linked findings:
