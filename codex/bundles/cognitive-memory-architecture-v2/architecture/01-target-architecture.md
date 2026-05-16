# Target Architecture

## High-Level Goal

Create `CanDoItAll.Modules.CognitiveMemory`, a module that turns project mindmaps, files, repositories, plugins, process runs, workflow runs, and agent outputs into a long-lived, queryable, self-improving project memory.

The module must support:

- fuzzy recall,
- detailed source lookup,
- source-grounded canonicalization,
- multi-view clustering,
- explicit associations,
- sleep-like consolidation,
- MAF workflow integration,
- distributed idle compute,
- human review and auditability.

## Core Architecture

```text
Sources
  mindmaps / workbench nodes / files / repos / emails / plugin sources / process runs / workflow runs / artifacts
        |
        v
Source Manifest + Raw Source Items
        |
        v
Canonicalization Engine
        |
        +--> Canonical Memory Store
        +--> Memory Relation Graph
        +--> Episodic / Procedural / Decision / Reflection Records
        |
        v
Projection Engine
        |
        +--> Qdrant vector projections
        +--> relational search projections
        +--> graph indexes
        |
        v
Recall Orchestrator
        |
        +--> MAF Context Provider
        +--> Workflow Executors
        +--> Agent Tools
        +--> UI Search / Memory Explorer
        |
        v
Reflection + Consolidation Engine
        |
        +--> activation updates
        +--> summaries
        +--> contradiction detection
        +--> human-review queue
        +--> projection rebuilds
```

## Proposed Projects

### Minimal V1

```text
src/CanDoItAll.CognitiveMemory.Abstractions
src/CanDoItAll.Modules.CognitiveMemory
```

### Recommended V1.1 Split

```text
src/CanDoItAll.CognitiveMemory.Abstractions
src/CanDoItAll.CognitiveMemory.Core
src/CanDoItAll.CognitiveMemory.Rag
src/CanDoItAll.CognitiveMemory.Semantics
src/CanDoItAll.CognitiveMemory.Maf
src/CanDoItAll.Modules.CognitiveMemory
```

### Optional Later Split

```text
src/CanDoItAll.CognitiveMemory.Clustering
src/CanDoItAll.CognitiveMemory.Distributed
src/CanDoItAll.CognitiveMemory.Plugins
```

## Design Principles

1. Source of truth is not vector DB.
2. Every generated memory item must have source references.
3. Every projection must be rebuildable.
4. Every consolidation run must be auditable.
5. Raw source data must not be silently rewritten.
6. Semantic similarity must not imply same context.
7. Memory retrieval must be goal-aware.
8. Agents should receive compact context packs, not massive unfiltered chunks.
9. Human validation should strengthen memory confidence.
10. Idle distributed workers must produce verifiable job outputs, not mutate global truth directly.

## Main Runtime Components

| Component | Responsibility |
|---|---|
| `IKnowledgeSourceRegistry` | Register/list source providers and source instances. |
| `IKnowledgeSourceScanner` | Scan sources and create/update source item manifests. |
| `ICanonicalizationEngine` | Convert source items into canonical memory candidates. |
| `IMemoryStore` | Durable store for canonical memory items and relations. |
| `IMemoryGraphService` | Store/query explicit relation graph. |
| `IMindMapSourceAdapter` | Read workbench/mindmap nodes and links with spatial metadata. |
| `IMemoryFeatureExtractor` | Extract semantic, graph, spatial, metadata, and temporal features. |
| `IMemoryClusterer` | Build clustering results from multi-view features. |
| `IQdrantProjectionService` | Maintain Qdrant collections/points and projection metadata. |
| `IRecallOrchestrator` | Multi-stage recall, scoring, association expansion, focus, and context pack output. |
| `IMemoryConsolidationEngine` | Idle/night source replay, canonical updates, summaries, contradictions, activation changes. |
| `IMemoryActivationService` | Recency, importance, confidence, staleness, validation, and salience scoring. |
| `IMemoryReflectionService` | Convert process/workflow/agent outcomes into episodes and learnings. |
| `IMemoryReviewQueue` | Human review tasks for uncertain merges, contradictions, and stale knowledge. |
| `IDistributedMemoryJobCoordinator` | Issue deterministic idle compute jobs to LAN devices. |

## Data Stores

| Store | Use |
|---|---|
| CanDoItAll relational DB | Memory metadata, source manifests, canonical items, relations, activations, jobs, recall traces. |
| Storage/IPFS | Large raw snapshots, canonical bundles, generated summaries, evidence packs. |
| Qdrant | Vector projections for semantic/episodic/procedural/topic recall. |
| Existing relational search index | Lexical search and UI search support. |
| Process/workflow stores | Existing episodic source data. |

## Source Lifecycle

```text
Detected source item
  -> source hash calculated
  -> source manifest updated
  -> canonicalization job queued
  -> canonical memory candidates created
  -> relation detection updates graph
  -> projection records created/updated
  -> activation initialized or updated
  -> human review if uncertain
```

## Recall Lifecycle

```text
Recall request
  -> classify intent and scope
  -> activate candidate memory areas
  -> expand associations
  -> focus by task/project/role/current process
  -> fetch details only for selected items
  -> build bounded context pack
  -> persist recall trace
  -> feedback updates activation
```

## Consolidation Lifecycle

```text
Idle/night trigger
  -> select changed/recent/stale sources
  -> replay recent episodes
  -> extract decisions/procedures/reflections
  -> update canonical summaries
  -> detect contradictions/supersession
  -> update clusters/projections
  -> update activation decay/boosts
  -> generate human review tasks
  -> persist consolidation report
```


## Interactive Memory Probing And Epistemic Drive Addendum

The target architecture now includes two metacognitive maintenance loops on top of recall and consolidation:

| Component | Responsibility |
|---|---|
| `IEpistemicDriveEngine` | Detect important knowledge gaps, preserve multi-dimensional knowledge-need vectors, and create reviewable learning proposals. |
| `IMemoryProbeSessionService` | Run dialogue-based memory probing sessions with trace-backed answers and feedback. |
| `IMemoryProbeQuestionGenerator` | Generate probe questions from weak coverage, stale records, contradictions, active project directions, context-separation risks, and controlled serendipity. |
| `IMemoryProbeAssessmentService` | Classify probe answers as confirmed, missing, wrong-scope, overconfident, source-poor, contradictory, or regression-worthy. |
| `IMemoryRegressionTestService` | Convert important probe failures into replayable memory regression tests. |
| `IMemoryProbeEvidencePublisher` | Publish probe outcomes as knowledge-gap evidence without mutating active memory directly. |

Additional lifecycle:

```text
Probe question
  -> recall request with trace
  -> answer + source/confidence explanation
  -> user feedback or correction
  -> probe finding and calibration record
  -> optional review item / regression test / learning proposal evidence
  -> consolidation and Epistemic Drive consume evidence later
```

Core invariant:

```text
Probe feedback is evidence, not direct truth mutation.
```

This addendum is implemented in detail by `architecture/15-interactive-memory-probing.md` and `architecture/16-probing-regression-and-calibration-loop.md`.
