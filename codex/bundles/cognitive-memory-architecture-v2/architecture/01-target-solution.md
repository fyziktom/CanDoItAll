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

## Recommended Project Shape

| Project | Responsibility |
|---|---|
| `CanDoItAll.CognitiveMemory.Abstractions` | Contracts shared by source adapters, MAF, workflows, plugins, and tests. |
| `CanDoItAll.CognitiveMemory.Core` | Source hashing, canonicalization orchestration, activation, recall, consolidation, relation detection, policy-independent logic. |
| `CanDoItAll.CognitiveMemory.Rag` | Adapter over `IRagDriver` and Qdrant projection behavior. |
| `CanDoItAll.CognitiveMemory.Semantics` | Adapter over SemanticCompletion embeddings, ranking, and classification. |
| `CanDoItAll.CognitiveMemory.Maf` | MAF context contributor, memory tools, workflow executor integration. |
| `CanDoItAll.Modules.CognitiveMemory` | EF entities, configurations, repositories, application services, source adapter registration, UI route registration. |
| `CanDoItAll.Modules.CognitiveMemory.Components` | Blazor components for dashboard, detail, trace, review, and run viewers. |

## First Vertical Slice

```text
Workbench source snapshot
  -> source manifest/items
  -> canonical memory item
  -> relation/projection metadata
  -> Qdrant or lexical projection
  -> recall context pack
  -> recall trace viewer
```

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
