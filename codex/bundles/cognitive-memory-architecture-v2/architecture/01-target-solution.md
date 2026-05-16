# Target Solution

## Summary

Create a Cognitive Memory capability as a module-backed domain with durable source manifests, canonical memory records, explicit relation graph, projection lifecycle, staged recall, consolidation, human review, and optional distributed compute.

The target architecture treats memory as durable CanDoItAll state and treats Qdrant, relational search, context packs, and UI maps as projections.

## Core Runtime Boundary

```text
Source adapters
  -> source manifests and source items
  -> canonicalization
  -> durable memory items and relations
  -> projections
  -> recall orchestrator
  -> context packs, tools, UI, workflow executors
```

## Phase Foundation Boundary

The first implementation-ready capability after prerequisite validation is not source ingestion or recall. It is the common driver/helper/EF guardrail layer used by all later phases. That layer should provide deterministic source, embedding, vector/search, policy, clock, id, hashing, paging, serialization, and EF query-shape test helpers.

This is intentionally a small shared foundation, not a new platform inside the platform. It should contain only cross-phase contracts and helpers that prevent duplicated test drivers, stringly typed state, unbounded query surfaces, hidden JSON persistence, and inconsistent provider-failure behavior.

## Neuro-Cognitive Foundation Boundary

The next foundation after common guardrails is the neuro-cognitive foundation: claim/evidence/belief ledger, evidence anchors, entity/context binding, and memory mutation authority. This must land before ingestion, taxonomy/projection, recall, consolidation, probing, or learning phases create durable memory semantics.

The design rule is:

```text
source anchor -> evidence anchor -> atomic claim -> belief state -> memory item/procedure/projection
```

`MemoryItem` remains useful as a chunk/container and summary surface, but it is no longer the only belief unit. Authoritative memory changes go through mutation commands with idempotency, evidence checks, optimistic concurrency, review policy, audit events, and projection invalidation.

## Minimum Viable Project Shape

Start smaller than the earlier project split implied. The first implementation must prove module composition, EF model registration, source snapshot consumption, mutation authority, and recall traces before creating a wide set of sibling projects.

| Project | Responsibility |
|---|---|
| `CanDoItAll.Modules.CognitiveMemory` | Initial owning module: EF entities/configurations, application services, source adapter registration, mutation authority, recall/consolidation services, workflow registration, and UI route registration. |
| `CanDoItAll.CognitiveMemory.Abstractions` | Add only if another product project must reference stable contracts without depending on the module implementation. Candidate contracts: MAF context contribution, workflow executors, source adapter contracts, and typed recall/query DTOs. |

Deferred splits:

- `CanDoItAll.CognitiveMemory.Core` only after policy-independent domain logic is large enough to justify separate test/build ownership.
- `CanDoItAll.CognitiveMemory.Rag` only after the RAG adapter needs independent packaging or provider-specific test isolation.
- `CanDoItAll.CognitiveMemory.Semantics` only after SemanticCompletion wrapping becomes more than a thin adapter.
- `CanDoItAll.CognitiveMemory.Maf` only if MAF integration requires a separate dependency direction from the module.
- `CanDoItAll.Modules.CognitiveMemory.Components` only if UI components need to be reused outside the module.

This keeps the first change set maintainable and prevents premature project sprawl.

## First Vertical Slice

```text
Workbench source snapshot
  -> source manifest/items
  -> evidence anchors and context frames
  -> atomic claims through mutation authority
  -> canonical memory item and relation metadata
  -> lexical/relational projection first, Qdrant optional after adapter gate
  -> score-geometry-backed recall trace
  -> workspace focus and inhibition update
  -> metamemory answer gate decision
  -> recall context pack and trace/review UI
```

Do not skip the claim/evidence, score-geometry, workspace, and answer-gate surfaces just to reach a visible recall demo faster. A projection-first demo would prove the wrong architecture.

## Critical Boundaries

- Source adapters own translation from existing modules into memory source items.
- Cognitive Memory owns canonical memory, relations, projection metadata, recall traces, review items, and consolidation runs.
- RAG/Qdrant owns vector storage mechanics only.
- SemanticCompletion owns embeddings, ranking, and classification utilities only.
- MAF owns runtime context execution, tools, workflows, and handoff mechanics only.
- Workbench, Processes, Workflows, Plugins, and Storage remain authoritative for their own raw records.

## Source Of Truth

| Layer | Authority |
|---|---|
| Raw source records and immutable snapshots | Highest |
| Source manifest and source item hashes | High |
| Canonical source items | Medium-high |
| Memory items and relations | Medium |
| Qdrant/search projections | Low, rebuildable |
| Recall context packs | Low, task-scoped |

## Required Prepared-State Decision

The prerequisite boundary refactor must be validated in the target branch before any implementation subbundle starts. The supplied current code already shows the required MAF context-contribution boundary and source snapshot providers, so the first gate is now a compatibility validation gate rather than a redesign gate. If a future branch lacks these contracts, this architecture must be reopened or the prerequisite bundle must be re-applied.

## Interactive Probing Extension

Add a probing boundary after the first recall/consolidation/review vertical slices:

```text
probe session
  -> recall request with trace
  -> answer + confidence + source explanation
  -> user confirmation/correction/challenge
  -> probe finding / evidence / review item / regression test
  -> consolidation and Epistemic Drive consume evidence
```

Interactive probing is a memory maintenance path. It is not a separate chatbot and not a direct mutation path. It should use the same source-of-truth hierarchy, recall trace store, access policy, review queue, and projection boundary as the rest of Cognitive Memory.

Recommended first probing slice:

```text
manual question -> recall trace -> answer/trace UI -> correction feedback -> review item + regression test candidate
```
