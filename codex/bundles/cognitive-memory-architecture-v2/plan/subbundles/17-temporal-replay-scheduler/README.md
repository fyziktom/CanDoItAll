# 17 Temporal Replay Scheduler

## Status

- Ready after `01b-score-geometry-driver`, `06-consolidation-engine`, and `16-prediction-error-salience-signals`.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
Make episodic memory sequence-aware and add prioritized replay/rehearsal scheduling driven by signals, prediction errors, risk, staleness, usefulness, contradiction pressure, and procedure maturity.

## Covered Inputs

- Neuro patch FR-047, FR-048 and NFR-029.
- Patch findings H-03, H-04, and M-06.
- Existing v2 consolidation, process/workflow source, probing regression, and distributed idle compute design.

## Prerequisites

- Source ingestion and consolidation basics exist.
- Prediction errors and salience signals are durable.
- `01b-score-geometry-driver` provides replay-priority score spaces, urgency shapes, and scalar queue projection policy.
- Mutation authority is available for any replay output that proposes authoritative change.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\22-temporal-episodic-memory-and-replay.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\06-consolidation-engine.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\09-distributed-idle-compute.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\16-probing-regression-and-calibration-loop.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.NeuroPatchContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md

## Deliverables

- Temporal episode and episode step records.
- Causal link guidance for steps, decisions, artifacts, prediction errors, claims, and procedures.
- Replay job records, kinds, priorities, and state transitions.
- Replay safety policy and distributed worker boundary.
- Replay-to-review/projection/regression integration.

## Dependency Impact

- Process/workflow/probe sessions become sequence-aware episodic inputs.
- Consolidation can enqueue replay jobs instead of only nightly summarization.
- Procedure skill memory can use episodes and replay evidence for maturity.
- Distributed idle compute can run deterministic replay subjobs only after coordinator validation.

## Validation Depth

- Unit/integration tests for episode order, actors, artifacts, decisions, outcomes, and causal links.
- Replay priority tests for high-risk stale procedure, repeated wrong-scope errors, failed probe regressions, and source anchor refresh.
- Score geometry tests proving replay queue priority is derived from replay vectors and urgency shapes.
- Negative tests proving replay creates draft/review/projection invalidation only.
- Distributed replay hash/version/policy rejection tests.
- EF/performance tests for episode timelines and replay queues.

## Implementation Steps

1. Add temporal episode, episode step, causal link, and replay job entities/configurations.
2. Add episode service and replay scheduler service.
3. Add consolidation integration for replay job planning.
4. Add deterministic replay fixtures including Docker context-boundary drill.
5. Add distributed replay acceptance boundaries without enabling direct mutation.

## Scope Exceptions

- Do not implement procedure skill maturity or simulation here.
- Do not implement distributed execution beyond coordinator-safe replay job boundaries.

## Do Not Do

- Do not let replay directly promote authoritative truth.
- Do not make distributed workers write canonical memory, Qdrant points, or review decisions.
- Do not lose episode step order or actor identity.
- Do not store replay target links only in JSON.

## Acceptance Checklist

- Episode steps preserve order and actors.
- Episodes link prediction errors, claims, procedures, decisions, and artifacts.
- Replay jobs are prioritized by signal vectors and risk.
- Replay jobs preserve priority evaluation traces and derived scalar queue projections.
- Replay output remains draft/review/projection invalidation until mutation authority/review policy applies.
- Distributed replay output is validated by hashes, versions, source scope, and policy.

## Proof Required

- Build/test output.
- EF model/index proof.
- Replay priority fixture output.
- Distributed replay rejection proof.
- Implementation report with deviations.

## Browser Validation Logging

- N/A for backend replay foundation.
- Browser proof is required later if replay queues/timelines are exposed in operator UI.

## Progression Gate

- Do not proceed to procedural skill memory, distributed replay, or Epistemic Drive replay consumption until episode sequence and replay safety tests pass.
- Reopen this subbundle if replay output can mutate truth directly, cannot cite evidence/signals, or stores only scalar priority.

## Suggested Agent Prompt

Implement temporal episodic memory and replay scheduling. Preserve episode order and causality, prioritize replay from prediction errors and signal vectors, and ensure replay output cannot promote truth without mutation authority and review policy.
