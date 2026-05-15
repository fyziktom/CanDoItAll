# UI and Operator Experience

## Goal

The Cognitive Memory module must be inspectable. Users should be able to see why the system remembered something, what sources support it, which memories are uncertain, and what the nightly consolidation changed.

The UI should not expose a raw database view. It should expose a cognitive map:

- source graph,
- semantic topics,
- spatial mindmap clusters,
- episodic timeline,
- procedures,
- decisions,
- review queue,
- recall traces.

## Main Screens

### 1. Memory Overview Dashboard

Purpose: show system health and high-level memory status.

Widgets:

- total memory items by type,
- items needing review,
- stale/superseded records,
- recent consolidation runs,
- Qdrant projection status,
- top active topics,
- contradiction count,
- source ingestion status.

### 2. Project Memory Map

Purpose: project-specific memory exploration.

Views:

- mindmap spatial view,
- semantic cluster view,
- graph relation view,
- timeline/episode view.

Recommended controls:

- switch between semantic/spatial/graph/hybrid clustering,
- show topic halos around clusters,
- show context-separated related topics using dashed lines,
- show confidence as opacity/outline strength,
- show human-validated records with a badge,
- show stale/superseded records with muted style.

### 3. Memory Item Detail

Purpose: inspect one memory item.

Sections:

- summary,
- memory type,
- activation/confidence/stability,
- source references,
- relations,
- projections,
- recall history,
- validation state,
- supersession chain,
- generated context preview.

Actions:

- approve,
- reject,
- mark stale,
- split,
- merge,
- create relation,
- open source,
- rebuild projection,
- create review task,
- create workflow from procedure.

### 4. Human Review Queue

Purpose: prevent silent corruption of memory.

Review item types:

- proposed merge,
- proposed split,
- contradiction,
- high-risk procedure,
- stale record candidate,
- missing source evidence,
- suspicious source/prompt injection,
- cross-project topic promotion.

### 5. Recall Trace Viewer

Purpose: explain agent memory retrieval.

Trace stages:

1. query/goal interpretation,
2. coarse activation candidates,
3. lexical results,
4. semantic results,
5. graph-expanded results,
6. focus selection,
7. context pack construction,
8. agent injection.

This screen is important for debugging why an agent used the wrong context.

### 6. Consolidation Run Viewer

Purpose: inspect the "sleep cycle".

Sections:

- input sources scanned,
- new/changed source hashes,
- generated canonical items,
- generated memory items,
- proposed relations,
- contradictions found,
- projection changes,
- review tasks created,
- errors and skipped items.

### 7. Procedure Library

Purpose: expose procedural memory as actionable workflow assets.

Each procedure should show:

- preconditions,
- steps,
- required tools/plugins,
- known failure patterns,
- success criteria,
- related episodes,
- confidence based on successful runs,
- ability to create a process/workflow instance.

## UX Principles

### Progressive Disclosure

Human memory recall is staged. The UI should mirror that:

```text
Overview -> topic area -> memory item -> source detail
```

### Explainable Retrieval

Every recall context pack must be explainable:

```text
This item was selected because:
- semantic similarity: 0.84
- graph proximity: 0.71
- source was human-approved
- used successfully in 5 previous workflows
- not stale
```

### Memory Correction Is Normal

The UI should make it easy to correct memory. Corrections should create events and not silently rewrite history.

### Separate Similarity From Identity

A visual difference should exist between:

- same topic,
- related topic,
- same context,
- different context,
- possible contradiction,
- supersession.

## Suggested Components

Reuse existing Canvas/BaseLib patterns:

- `MemoryGraphCanvas`
- `MemoryClusterExplorer`
- `MemoryItemCard`
- `MemoryActivationBadge`
- `MemoryConfidenceMeter`
- `MemorySourceReferenceList`
- `RecallTraceTimeline`
- `ConsolidationRunTimeline`
- `HumanReviewDecisionPanel`
- `ProcedureMemoryCard`
- `MemoryProjectionStatusPanel`

## UI Integration Points

| Existing area | New entry point |
|---|---|
| Project Workbench | `Memory` tab and node-level memory sidebar. |
| Process run detail | `Memory generated from this run` panel. |
| Workflow run detail | `Recall traces` and `reflections` panels. |
| Plugin catalog | source ingestion capabilities. |
| Admin/settings | embedding profiles, consolidation schedule, worker registration. |
| Agent configuration | memory access policy and context-pack limits. |

## Minimal V1 UI

V1 should not try to build the full visual brain. Implement:

1. Memory dashboard.
2. Project memory list with filters.
3. Memory item detail with source refs.
4. Human review queue.
5. Consolidation run viewer.
6. Basic recall trace viewer.

The spatial/graph canvas can arrive in V1.5 after the domain model stabilizes.
