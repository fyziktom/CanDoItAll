# Memory Consolidation Engine

## Purpose

The consolidation engine is the software equivalent of sleep/rest memory processing. It replays recent sources, process runs, workflow runs, and agent outputs to update canonical memory, relations, projections, activation, and human-review tasks.

## Triggers

- Quartz schedule through Automation module.
- Idle detection from runtime host.
- Manual user action.
- After process/workflow completion.
- After large source import.
- After embedding model/projection version change.

## Consolidation Modes

| Mode | Purpose |
|---|---|
| `IncrementalRecent` | Process changed sources and recent runs. |
| `ProjectNightly` | Project-wide nightly consolidation. |
| `CrossProjectWeekly` | Merge reusable topics across projects. |
| `ProjectionRebuild` | Rebuild Qdrant projections after model/schema changes. |
| `ContradictionReview` | Focus on stale/conflicting records. |
| `ProcedureMining` | Extract reusable procedures from successful episodes. |
| `FailureLearning` | Extract lessons from failed/reworked episodes. |

## Pipeline

```text
Start consolidation run
  -> acquire project/global lease
  -> select source scope
  -> diff source hashes
  -> load changed source items
  -> canonicalize new/changed items
  -> extract episodes from process/workflow runs
  -> extract decisions/procedures/reflections
  -> detect duplicates and merge candidates
  -> detect contradictions and supersession
  -> update memory graph
  -> update activation/staleness
  -> update Qdrant/search projections
  -> create human review tasks
  -> write report artifact
  -> release lease
```

## Safety Rules

1. Never delete raw source during consolidation.
2. Do not overwrite human-validated canonical records automatically unless a policy allows it.
3. Create draft supersession candidates when unsure.
4. Record algorithm version, model version, prompt version, source hash, and output hash.
5. Every generated record must include source refs.
6. Large generated reports go to storage/IPFS; DB stores references.
7. Consolidation must be resumable and idempotent.

## Consolidation Agents

Recommended MAF agents/workflows:

| Agent | Role |
|---|---|
| `Memory Curator Agent` | Canonicalization, merge proposals, source grounding. |
| `Episode Summarizer Agent` | Converts process/workflow/agent runs into episodes. |
| `Procedure Miner Agent` | Extracts repeatable procedures from successful runs. |
| `Decision Librarian Agent` | Extracts reasons, alternatives, decisions, constraints. |
| `Contradiction Analyst Agent` | Finds stale/conflicting records. |
| `Projection Builder Agent` | Prepares Qdrant/search projection updates. |
| `Memory QA Agent` | Checks source refs, confidence, and hallucination risk. |

## Human Review Queue

Create review tasks when:

- confidence below threshold,
- canonicalization merged unrelated contexts,
- contradiction detected,
- human-validated record would be superseded,
- source is sensitive/high-risk,
- procedure affects production deployment/security/finance/legal decisions.

## Activation Updates

Consolidation should update:

- recency score,
- usage count,
- importance,
- confidence,
- validation boost,
- risk boost,
- failure/rework impact boost,
- staleness penalty,
- contradiction penalty,
- dormancy state.

## Output Report

Each run writes a report with:

- input scope,
- source changes,
- created/updated memory items,
- relation changes,
- projection changes,
- contradictions,
- human review tasks,
- errors/retries,
- performance metrics,
- next recommended run.
