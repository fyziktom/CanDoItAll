# 16 Prediction Error Salience Signals

## Status

- Ready after `15-cognitive-workspace-attention-router`.
- Critical foundation for recall activation, replay, probing, Epistemic Drive, and calibration.

## Objective

Add prediction expectation/error records and a durable multi-dimensional cognitive signal ledger without collapsing learning evidence into scalar priority.

## Covered Inputs

- Neuro patch FR-045, FR-046 and NFR-027, NFR-033.
- Patch findings C-04, H-04, and M-04.
- Existing v2 activation, probing, consolidation, Epistemic Drive, and confidence calibration design.

## Prerequisites

- `14-neuro-foundation-claim-evidence-ledger` provides evidence anchors, claims, and context frames.
- `15-cognitive-workspace-attention-router` provides workspace and attention decision ids.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\19-prediction-error-salience-signal-ledger.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\14-epistemic-drive-and-learning-orchestration.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\16-probing-regression-and-calibration-loop.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.NeuroPatchContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\EpistemicDriveContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md

## Deliverables

- Prediction expectation records.
- Prediction error records.
- Cognitive signal records and publication/query services.
- Signal consumption rules for activation, replay, probing, Epistemic Drive, procedure maturity, and answer-gate calibration.
- Vector/schema metadata requirements for salience and `KnowledgeNeedVector`.

## Dependency Impact

- Recall activation can consume signals but cannot treat signal score as truth.
- Consolidation consumes prediction errors and signals as evidence.
- Replay scheduler prioritizes jobs from signal vectors.
- Probing publishes prediction errors and calibration-risk signals.
- Epistemic Drive consumes signals as evidence contributors.

## Validation Depth

- Unit tests for expectation/error classification and signal vector preservation.
- Integration tests for probe feedback, workflow failure, stale source, user correction, and confirmed useful procedure publishing expected signals.
- Negative tests proving salience cannot bypass source truth, access policy, or review policy.
- EF query/index tests for signal and prediction-error lists.
- Performance scan for signal publication/query hot paths.

## Implementation Steps

1. Add prediction expectation/error and signal entities/configurations.
2. Add signal publication/query services and deterministic test fixtures.
3. Add evidence anchor, actor, algorithm/profile version, and timestamp requirements.
4. Add integration seams for recall activation, consolidation, probing, replay, Epistemic Drive, and answer gate.
5. Add tests proving no scalar-only salience.

## Scope Exceptions

- Do not implement replay scheduling, probing feedback, or Epistemic Drive proposal generation here.
- Do not tune final activation weights without later profiling and regression evidence.

## Do Not Do

- Do not collapse salience into one authoritative score.
- Do not let high salience create truth, approve memory changes, or bypass access policy.
- Do not store signals only as JSON metadata.
- Do not publish anonymous signals without actor/evidence/version traceability.

## Acceptance Checklist

- Prediction errors capture expected vs observed mismatch.
- Signal records preserve dimensions and evidence.
- Signal consumers are listed and bounded by policy.
- `KnowledgeNeedVector` has schema/version/normalization/evidence metadata.
- Wrong-scope Docker fixture can publish context-separation signal.

## Proof Required

- Build/test output.
- EF model/index proof.
- Signal vector preservation tests.
- Negative policy tests.
- Implementation report with deviations.

## Browser Validation Logging

- N/A for backend foundation.
- Browser proof is required later in dashboards/workbenches that expose signals, prediction errors, or answer-gate warnings.

## Progression Gate

- Do not proceed to recall, consolidation, replay, probing, Epistemic Drive, or answer gate phases until signals and prediction errors are durable, dimensional, and policy-safe.
- Reopen this subbundle if downstream code invents local scalar signal state.

## Suggested Agent Prompt

Implement Prediction Error and Salience Signal Ledger as auditable, dimensional evidence. Preserve source truth and policy boundaries; signals influence prioritization and calibration but never create truth directly.

