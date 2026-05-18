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

The prerequisite boundary refactor must be approved before any implementation subbundle starts. If rejected, this architecture must be reopened and simplified to a direct-module implementation with explicit debt and coupling risks.
