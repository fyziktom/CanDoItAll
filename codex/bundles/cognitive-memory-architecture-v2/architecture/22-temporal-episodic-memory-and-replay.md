# 22 Temporal Episodic Memory And Replay Scheduler

## Purpose

Make episodic memory sequence-aware and make consolidation/replay priority-driven.

The existing architecture already uses process/workflow runs as episodic inputs. This patch adds explicit temporal structure, causal links, prediction error links, and replay scheduling.

## Episodic Memory Record

An episode should represent a bounded experience:

- project event,
- workflow run,
- process step,
- agent handoff,
- user correction session,
- probe session,
- learning task,
- deployment/test/debugging event.

## Episode Fields

| Field | Meaning |
|---|---|
| `EpisodeId` | Stable id. |
| `ProjectId` | Project scope. |
| `EpisodeKind` | Workflow, process, probe, review, deployment, etc. |
| `StartedAtUtc` / `EndedAtUtc` | Time boundary. |
| `Actors` | Users, agents, roles, tools. |
| `ContextFrameIds` | Context validity. |
| `Goal` | What the episode attempted. |
| `Steps` | Ordered event steps. |
| `Decisions` | Decision points and alternatives. |
| `Artifacts` | Produced or consumed artifacts. |
| `ExpectedOutcome` | Prediction or goal. |
| `ActualOutcome` | What happened. |
| `PredictionErrorIds` | Errors observed. |
| `ClaimIds` | Claims produced/supported/attacked. |
| `ProcedureSkillIds` | Procedures used or updated. |
| `ReviewItemIds` | Human review links. |

## Episode Step

Each step should store:

- sequence index,
- timestamp,
- actor,
- action kind,
- input refs,
- output refs,
- tool/plugin used,
- source/evidence refs,
- success/failure,
- warning/error,
- related claim/procedure ids.

## Causal Links

Episodic memory should support causal links:

- step A caused step B,
- decision D led to artifact A,
- failure F caused rework R,
- source S superseded claim C,
- probe correction attacked claim C,
- workflow success reinforced procedure P.

These links help answer:

- why did we decide this?
- what failed before?
- which source changed the plan?
- which procedure was last validated?

## Replay Scheduler

Replay is a scheduled cognitive maintenance operation.

### Replay Job Kinds

| Kind | Purpose |
|---|---|
| `ConsolidateEpisode` | Convert episodes into claims/procedures/relations. |
| `RehearseClaim` | Re-evaluate source-backed claim and activation. |
| `ReplayProbeRegression` | Re-run important failed probes. |
| `ValidateProcedure` | Re-check a procedure against evidence/test results. |
| `RefreshSourceAnchors` | Re-anchor claims after source change. |
| `ResolveContradiction` | Re-evaluate contested claims. |
| `SpacedRecall` | Maintain important but infrequently used knowledge. |
| `ContextBoundaryDrill` | Test semantically similar but separated contexts. |
| `CrossProjectAnalogyReview` | Explore reusable patterns without leaking private data. |

## Replay Priority Inputs

- prediction error magnitude,
- risk level,
- usefulness/reward,
- user interest,
- staleness,
- contradiction pressure,
- confidence weakness,
- procedure maturity,
- source trust change,
- strategic alignment,
- recurrence/frequency.

Replay must use the shared `ReplayPriority` score space. The scheduler compares replay vectors against urgency shapes such as high-risk stale procedure, repeated wrong-scope context boundary, failed regression, source-anchor refresh, or contradiction resolution. A queue priority number may be cached for scheduling, but the replay job must retain the evaluation trace and scalar projection kind.

## Replay Safety

Replay jobs can produce:

- draft claim updates,
- review items,
- regression results,
- learning proposals,
- projection invalidation requests,
- updated activation signals.

Replay jobs must not directly promote truth. Promotion uses `IMemoryMutationAuthority` and review policy.

## Relationship To Distributed Idle Compute

Distributed workers can run deterministic replay subjobs:

- embedding refresh,
- clustering,
- regression replay,
- source hash checks,
- feature extraction.

Workers cannot approve memory changes, write canonical truth, or bypass access policy.

## Required Tests

- episode steps preserve order and causal links,
- replay priority increases after repeated overconfident wrong answers,
- high-risk stale procedure gets replay before low-risk stable topic,
- distributed replay result with wrong input hash is rejected,
- replay creates review candidates rather than direct truth mutation,
- replay of context-boundary drill catches production/test Docker confusion.
