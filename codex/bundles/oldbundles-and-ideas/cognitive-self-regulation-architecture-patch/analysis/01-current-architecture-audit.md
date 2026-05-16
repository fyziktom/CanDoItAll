# Current Architecture Audit For Self-Regulation

## Reviewed Current Bundle Areas

The uploaded `cognitive-memory-architecture-v2` bundle already contains several mechanisms that partially cover Self-Regulation:

| Area | Current File(s) | Existing Capability |
|---|---|---|
| Active working state | `architecture/18-cognitive-workspace-and-attention-router.md` | Workspace frames, focus slots, inhibited candidates, open questions, context budget, attention decisions. |
| Answer-time uncertainty control | `architecture/24-metamemory-confidence-and-abstention.md` | Answer/warn/clarify/source-audit/probe/review/learning/abstain decisions. |
| Belief discipline | `architecture/20-claim-evidence-belief-ledger.md` | Atomic claims, support/attack evidence, belief states, mutation authority. |
| Learning signals | `architecture/19-prediction-error-salience-signal-ledger.md` | Prediction errors and cognitive signals such as calibration risk, wrong scope, source weakness, surprise, risk, usefulness. |
| Probing and calibration | `architecture/15-interactive-memory-probing.md`, `architecture/16-probing-regression-and-calibration-loop.md` | Dialogue-based probing, correction feedback, regression tests, confidence calibration. |
| Multidimensional scoring | `architecture/26-score-geometry-driver.md` | Typed score spaces, vectors, shapes, missing dimensions, evaluation traces. |
| Neuro-cognitive integration | `architecture/17-neuro-cognitive-integration-layer.md` | Existing umbrella for workspace, attention, salience, prediction error, belief revision, replay, procedural memory, metamemory. |

## What Is Strong Already

The architecture already avoids the most dangerous flat-RAG failure modes:

- Qdrant is treated as a rebuildable projection, not source truth.
- Generated summaries are not allowed to become source truth directly.
- Probe feedback becomes evidence/review/regression input, not direct mutation.
- Answer gating already considers source sufficiency, context fit, belief state, contradiction, staleness, redaction, risk, and access policy.
- Score Geometry prevents hidden weighted-sum decision logic.
- Prediction errors and salience signals are event-like evidence, not truth.

## Current Weakness

The current design has components that regulate cognition, but no explicit `Self-Model` and no single `Self-Regulation Orchestrator` that combines:

- agent/project role identity,
- domain competence profiles,
- known failure patterns,
- historical calibration statistics,
- current self-regulation state,
- humility triggers,
- confidence reinforcement rules,
- escalation/professor-review policy,
- answer posture selection,
- post-answer calibration and recovery.

The result is that answer gating can block unsafe answers, but the system still lacks a durable model of its own competence, limits, and recurring failure modes.

## Architecture-Level Finding

`IMetamemoryAnswerGate` is necessary but not sufficient.

It is currently answer-time control. Cognitive Self-Regulation must be broader:

```text
before operation  -> decide posture, risk, required evidence, escalation
while operating   -> monitor attention, uncertainty, contradiction, overload
before answer     -> gate answer and rendering mode
after outcome     -> update calibration, failure patterns, probing plans, replay/review needs
```

## Contract Audit Note

Codex should still run a normal contract consistency audit before extending the architecture contracts. At minimum, verify enum naming, numeric stability, duplicate semantic concepts, missing score-space values, and references between `CognitiveMemory.NeuroPatchContracts.cs`, `CognitiveMemory.ScoringContracts.cs`, and the new Self-Regulation contracts. Do not assume a specific enum defect unless the current source actually confirms it.
