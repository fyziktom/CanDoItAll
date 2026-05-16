# 23 Calibration Health And Probing Training

## Status

- Ready after `13a-probing-core-regression-calibration`, `21-cognitive-self-model`, and `16-prediction-error-salience-signals`.
- Required before `22-self-regulation-orchestrator` can use stable calibration aggregates for posture decisions.
- Implementation not started.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.

## Objective

Extend probing calibration into aggregate calibration health used by self-regulation, answer posture selection, professor review routing, answer gating, and post-outcome recovery.

## Covered Inputs

- `inputs/07-cognitive-self-regulation-patch-reference.md`.
- FR-059, FR-061, NFR-037, and NFR-039.
- Existing probing calibration FR-038 and probe replayability requirements.

## Prerequisites

- Probe sessions, probe turns, feedback, findings, regression tests, and basic calibration records exist.
- Self-model profiles exist so aggregates can attach to domain/task/model/profile scope.
- Prediction error and salience signal ledgers exist for outcome publication.
- Score geometry exposes calibration health score space and evidence kinds.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\16-probing-regression-and-calibration-loop.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\27-cognitive-self-regulation-layer.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\29-calibration-health-and-probing-training.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\InteractiveMemoryProbingContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.SelfRegulationContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\self-regulation-test-matrix.md

## Deliverables

- Calibration event and aggregate persistence by domain, task type, model profile, risk category, and feature pattern.
- Calibration bins and metrics for expected calibration error or equivalent, Brier/squared loss, signed bias, overconfidence, underconfidence, abstention quality, wrong-scope recurrence, source-insufficient recurrence, professor-review disagreement, and human-review rejection.
- `ICalibrationHealthService` implementation and deterministic fixtures.
- Outcome observation flow that publishes calibration records, prediction errors, salience signals, regression candidates, probing drills, replay jobs, review items, and self-model update proposals.
- Tests proving profile version changes do not reinterpret old traces.

## Dependency Impact

- `22-self-regulation-orchestrator` uses aggregates to classify overconfidence, underconfidence, and professor-review-needed states.
- `19-metamemory-abstention-calibration` uses calibration health for answer readiness.
- `12-epistemic-drive-engine` can consume repeated overconfidence, source weakness, and wrong-scope outcomes as gap evidence.
- `25-self-regulation-ui` displays calibration health, drift, bins, and recommended drills.

## Validation Depth

- Unit tests for binned calibration and aggregate metrics.
- Unit tests for high-confidence wrong answer, low-confidence correct answer, wrong-scope, source-insufficient, and abstention-quality updates.
- Negative tests proving a single event cannot silently retune profile thresholds.
- Versioning tests proving old traces keep old profile/schema interpretation.
- Integration tests proving outcome observation creates expected prediction error, salience, regression/probe/review/replay links.
- Performance review for aggregate update/query paths.

## Implementation Steps

1. Add calibration event, bin, aggregate, and profile-version records.
2. Add aggregate calculation/update service with deterministic fixtures.
3. Extend probe answer metadata and regression results to record predicted posture and actual outcome where available.
4. Add outcome observation path to publish calibration, prediction error, salience, regression, probing drill, replay, and review candidates.
5. Add tests for aggregate metrics, versioning, and negative retuning behavior.
6. Update execution report/workbook proof paths.

## Scope Exceptions

- Do not build the calibration health dashboard in this phase.
- Do not train or tune real production thresholds in this phase.
- Do not allow calibration aggregates to directly change belief state or source truth.

## Do Not Do

- Do not collapse calibration health to one average confidence.
- Do not silently reinterpret old traces after profile changes.
- Do not permanently alter competence from one correction or one success.
- Do not use calibration reinforcement to erase contradiction, source, risk, or redaction dimensions.

## Acceptance Checklist

- Calibration aggregates exist by domain/task/model/risk/feature pattern.
- Metrics include expected calibration error or equivalent, Brier/squared loss, signed bias, overconfidence, underconfidence, abstention quality, wrong-scope, and source-insufficient rates.
- Bins preserve predicted confidence versus actual correctness.
- Outcomes can create calibration, prediction error, salience, regression, probing, replay, review, and self-model proposal evidence.
- Profile versioning prevents silent reinterpretation.

## Proof Required

- Build/test output.
- Aggregate fixture output for overconfidence, underconfidence, wrong-scope, source-insufficient, and abstention cases.
- Versioning test output.
- Integration proof for outcome-to-signal/replay/review/probe publication.
- Execution report and workbook updates with proof paths.

## Browser Validation Logging

- N/A for this backend calibration phase.
- Browser proof is required later in `25-self-regulation-ui`.

## Progression Gate

- Do not proceed to `22-self-regulation-orchestrator` until calibration aggregates, bins, profile versioning, and outcome feedback links are tested.
- Reopen this subbundle if downstream posture selection cannot distinguish overconfidence from source insufficiency, wrong-scope recurrence, underconfidence, or abstention quality.

## Suggested Agent Prompt

Implement Calibration Health as a durable training loop for self-regulation. Aggregate calibration by domain, task, model profile, risk, and feature pattern; preserve bins and profile versions; and convert outcomes into calibration, prediction error, salience, regression, probing, replay, review, and self-model update evidence.
