# Recall Orchestrator

## Purpose

The recall orchestrator is the core brain-like retrieval component. It receives a goal-aware request and returns a bounded context pack. It should not behave like a simple vector search.

## Recall Stages

### Stage 1: Intent And Scope Understanding

Input:

- natural-language request,
- active project id,
- active process/workflow run,
- agent role,
- requested memory kinds,
- risk level,
- allowed source types,
- detail budget.

Output:

- recall intent,
- project/global scope,
- detail depth,
- source constraints,
- safety constraints.

### Stage 2: Coarse Candidate Activation

Channels:

- exact source refs,
- lexical search via `ISearchIndexService`,
- vector search via `IRagDriver`,
- graph neighborhood search via `IMemoryGraphService`,
- active working memory,
- recent episodes,
- high-activation procedures/decisions.

Output:

- candidate memory areas,
- candidate items,
- preliminary score vectors,
- channel evidence.

### Stage 3: Association Expansion

Expand from candidates through relation graph:

- `DecisionFor`,
- `ProcedureFor`,
- `EpisodeFor`,
- `SemanticallyRelatedContextSeparated`,
- `Supersedes`,
- `Contradicts`,
- `ValidatedBy`,
- `FailedBecauseOf`.

This stage preserves related-but-not-current context as side context.

### Stage 4: Focus Selection

Select what enters the context pack based on:

- current task intent,
- current role,
- process step needs,
- risk,
- confidence,
- activation,
- human validation,
- staleness,
- contradiction state,
- token/context budget.

### Stage 5: Detail Retrieval

Only selected items load detailed source content from:

- canonical DB text,
- storage/IPFS artifacts,
- process/workflow stores,
- workbench nodes,
- file/repo tools.

### Stage 6: Context Pack Rendering

Return a structured context pack:

```json
{
  "summary": "...",
  "selectedMemories": [],
  "sideContext": [],
  "sourceRefs": [],
  "openQuestions": [],
  "uncertainties": [],
  "doNotConfuseWith": [],
  "recommendedTools": [],
  "traceId": "..."
}
```

### Stage 7: Trace And Feedback

Persist recall trace. Later feedback can update activation:

- used in final answer,
- ignored,
- caused wrong answer,
- user corrected it,
- agent needed additional source details.

## Recall Modes

| Mode | Behavior |
|---|---|
| `QuickAssociative` | Fast fuzzy candidate areas, low detail. |
| `FocusedTaskContext` | Best mode for agent execution. Balanced source-grounded context. |
| `DeepSourceGrounded` | Loads detailed sources and citations. |
| `CrossProjectAnalogy` | Searches global/cross-project topic memory. |
| `ProcedureLookup` | Prioritizes procedural memory and validation evidence. |
| `DecisionLookup` | Prioritizes decisions, reasons, alternatives, evidence. |
| `IncidentLearning` | Prioritizes failures, contradictions, recovery records. |

## Scoring Model

Recall must use the generic Score Geometry Driver. It must not own a local weighted-sum formula.

The recall score space should preserve dimensions such as:

- semantic similarity,
- lexical match,
- graph proximity,
- spatial proximity,
- metadata fit,
- temporal recency,
- workspace focus fit,
- context fit,
- source sufficiency,
- evidence support,
- contradiction pressure,
- staleness pressure,
- activation,
- calibration risk,
- access/redaction pressure.

Example evaluation:

```text
candidate evidence
  -> RecallCandidate score vector
  -> compare with task/workspace/context shapes
  -> record matched focus region or inhibited boundary
  -> optional display rank projection for UI/order only
```

The score space schema, normalization profile, matched shapes, missing dimensions, evidence refs, scalar projection, and algorithm version must be persisted or referenced in recall traces. A scalar display rank may be cached, but it is not authoritative and must be reproducible from the trace.

## Recall Evidence For Epistemic Drive

Recall traces feed Epistemic Drive, but recall must not directly mutate authoritative knowledge.

Trace records should expose typed evidence for:

- low-confidence selected candidates,
- missing or weak source references,
- repeated user corrections,
- failed answer validation,
- repeated fallback to broad/generic sources,
- uncertain contradiction resolution,
- stale records repeatedly used for active tasks,
- budget exclusions that repeatedly hide detail needed by agents,
- probing failures attached to the recall goal.

Epistemic Drive consumes these signals during consolidation. A single weak trace should usually lower confidence only slightly; repeated traces across active work can create a `KnowledgeGapRecord` or `EpistemicTensionRecord`.

Recall evidence must include:

- trace id,
- project id,
- intent,
- selected/excluded item ids,
- confidence and source-coverage signals,
- user/agent feedback,
- linked process/workflow/probing ids where available,
- a compact explanation of the weak signal.

## Output Boundaries

The recall orchestrator must never hide uncertainty. Include:

- source coverage gaps,
- stale records,
- contradicted records,
- related-but-separate topics,
- weaker analogies,
- reason for selected focus.

## MAF Context Provider Behavior

For MAF agent runs:

1. agent invocation begins,
2. context provider receives current request messages,
3. context provider calls `IRecallOrchestrator`,
4. context pack is rendered as a compact system/developer message,
5. tools remain available for detailed source fetches,
6. recall trace is linked to execution run/workflow run/process step.

## Example: Docker Testing vs Production

Query: `How should we use Docker for deployment?`

Recall should return:

- primary: production deployment if active task is production deployment,
- side context: test Docker simulation as related-but-context-separated,
- warning: do not mix test simulation config with production config,
- source refs: both node ids,
- suggested next action: ask for target scope or inspect project deployment procedure.

## Recall Requirements For Probing

Interactive probing needs recall traces with enough detail to support explanation, feedback, and regression tests. Recall traces should include:

- selected and excluded candidates,
- score vector components, shape matches, and scalar projections when present,
- access/redaction decisions,
- staleness and contradiction warnings,
- budget exclusions,
- source refs used in the answer,
- projection/fallback status,
- context-separation warnings when semantic similarity is high but graph/spatial/scope signals disagree.

Probe answers should call recall with trace enabled and persist the trace id on every probe turn. A probing failure without a trace is not actionable.

## Neuro-Cognitive Recall Updates

Recall is now a tool used by the attention router, not the whole cognitive loop.

Required pre-stage:

```text
request
  -> load/create cognitive workspace frame
  -> run self-regulation assessment when answer/tool behavior is likely
  -> run attention router
  -> decide recall, answer-from-workspace, clarification, source audit, probe, review, replay, learning proposal, or abstention
```

When the router chooses recall, the recall orchestrator must:

- receive or create a `WorkspaceFrameId`,
- preserve `SelfRegulationAssessmentId` and `AnswerPostureDecisionId` when present,
- consider focus slots and goal stack,
- use entity/context binding before final candidate ranking,
- rank claim-level candidates where claims exist,
- record candidates inhibited by context boundaries,
- include selected claim ids and evidence anchor ids in trace details,
- preserve score vectors, matched/inhibited shapes, scalar projections, and answer-gate inputs,
- send the final recall result to the metamemory answer gate before rendering.

Recall traces must include:

- workspace frame id,
- self-regulation assessment id,
- answer posture decision id,
- attention decision id,
- selected claim ids,
- selected source/evidence anchor ids,
- inhibited candidates and reasons,
- source sufficiency and redaction limitations,
- context-boundary decisions,
- answer gate decision id,
- projection/fallback status and budget exclusions.

The Docker context-separation fixture is a mandatory first validation case. A production Docker query must inhibit local/test/CI Docker procedures as authoritative answers unless the user explicitly asks for those contexts.
