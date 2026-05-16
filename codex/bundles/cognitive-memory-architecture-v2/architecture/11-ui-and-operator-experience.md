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
- night reflection and learning opportunities.

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

### 8. Night Reflection / Cognitive Briefing

Purpose: show what the system learned from idle/night consolidation and which knowledge areas deserve human attention.

Sections:

- top knowledge improvement opportunities,
- topic coverage maps,
- weak subtopics,
- evidence summary,
- related active project directions,
- suggested learning actions,
- estimated effort,
- expected outputs,
- source trust summary,
- required approvals,
- probing-before-learning option.

Example rows:

1. Docker operational knowledge
   Priority: High
   Why: frequently used in workflows, several failures, incomplete non-happy paths
   Suggested action: study approved Docker docs and generate runbooks
   Estimated cost: 1 hour
   Approval: required

2. Microsoft Graph mail categories
   Priority: Medium
   Why: Office365 plugin work depends on it, current memory is weaker than Gmail
   Suggested action: inspect official Graph docs
   Estimated cost: 35 minutes

3. Plugin ZIP installation lifecycle
   Priority: Medium
   Why: active refactoring area, high architectural impact
   Suggested action: generate probing questions and review implementation

4. WebSocket proxy handling
   Priority: Low
   Why: older unresolved area, not currently active

### 9. Knowledge Coverage Map

Purpose: inspect topic regions and subregions.

The map should show:

- region hierarchy,
- coverage,
- confidence,
- staleness,
- risk,
- source count,
- open question count,
- contradiction pressure,
- project direction intersections.

The UI can include a scalar priority label for sorting, but the detail view must expose vector dimensions and evidence.

### 10. Learning Proposal Detail

Purpose: let the operator decide whether to improve knowledge.

Sections:

- topic and subtopic coverage map,
- why this topic,
- why now,
- weak subareas,
- evidence refs,
- active project direction intersections,
- suggested sources and trust levels,
- estimated effort,
- expected outputs,
- proposed depth,
- risks,
- suggested probing questions,
- acceptance criteria,
- audit history.

Actions:

- approve,
- reject,
- snooze,
- narrow scope,
- expand scope,
- add source,
- request probing first,
- turn into a Codex bundle,
- assign to human,
- assign to approved agent workflow.

### 11. Learning Outcome Review

Purpose: inspect outputs from approved learning tasks before memory promotion.

Sections:

- sources actually read,
- source refs and trust levels,
- draft canonical records,
- draft procedures/runbooks,
- non-happy-path notes,
- probing questions,
- QA findings,
- high-risk validation requirements,
- projection refresh status after acceptance.

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

Learning proposals must be equally explainable:

```text
This proposal exists because:
- Docker networking is high-risk for workflow executor sandboxing
- recall traces show repeated uncertainty
- Compose failures appeared in workflow runs
- official Docker docs are available
- expected reuse is high across plugin isolation and local development
```

### Memory Correction Is Normal

The UI should make it easy to correct memory. Corrections should create events and not silently rewrite history.

### Approval Is A Workflow

Learning proposal actions must be explicit and auditable. Approving a proposal authorizes only the selected scope and sources. Expanding scope or using new external sources should require another policy check.

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
- `NightReflectionSummary`
- `KnowledgeCoverageMap`
- `KnowledgeNeedVectorPanel`
- `LearningProposalDetail`
- `LearningApprovalDecisionPanel`
- `LearningOutcomeReviewPanel`
- `ProbingQuestionSetPanel`

## UI Integration Points

| Existing area | New entry point |
|---|---|
| Project Workbench | `Memory` tab and node-level memory sidebar. |
| Process run detail | `Memory generated from this run` panel. |
| Workflow run detail | `Recall traces` and `reflections` panels. |
| Plugin catalog | source ingestion capabilities. |
| Admin/settings | embedding profiles, consolidation schedule, worker registration. |
| Agent configuration | memory access policy and context-pack limits. |
| Nightly consolidation report | Night Reflection summary and learning proposal links. |
| Human review queue | learning proposal approval and learning outcome review. |

## Minimal V1 UI

V1 should not try to build the full visual brain. Implement:

1. Memory dashboard.
2. Project memory list with filters.
3. Memory item detail with source refs.
4. Human review queue.
5. Consolidation run viewer.
6. Basic recall trace viewer.
7. Night Reflection summary.
8. Learning proposal detail with approval/snooze/request-probing actions.

The spatial/graph canvas can arrive in V1.5 after the domain model stabilizes.

## Cognitive Memory Dialogue Workbench

Add a dedicated UI for interactive probing.

Recommended layout:

```text
left: mode selector, knowledge regions, question queue
center: dialogue and answer stream
right: recall trace, source refs, confidence, warnings, findings
bottom/right actions: confirm, correct, mark missing, wrong scope, create review item, create regression test
```

The UI should make uncertainty visible. The operator should quickly see whether an answer is source-backed, stale, contradicted, inferred, generic, or wrong-scope.

Important screens/components:

- `MemoryProbeWorkbench`
- `MemoryProbeQuestionQueue`
- `MemoryProbeTracePanel`
- `MemoryProbeFeedbackActions`
- `MemoryProbeCorrectionDialog`
- `MemoryRegressionTestEditor`
- `MemoryCalibrationDashboard`
