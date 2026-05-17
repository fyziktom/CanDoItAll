# 19 Metamemory Abstention Calibration

## Status

- Completed
- Completion detail: Passed on 2026-05-16 follow-up implementation.
- Closure proof: answer-gate decision API persists posture-aware decisions, required actions, warnings, and intervention counts consumed by review UI; verified by focused unit/integration tests and PostgreSQL smoke counts at `validation/postgres-smoke/evidence/20260516-231507/postgres-advanced-table-counts.csv`.
- Required before user/agent answer rendering is considered safe.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
Add an answer-time metamemory gate that uses source sufficiency, context fit, belief state, confidence calibration, self-regulation assessment, answer posture, contradiction risk, staleness, redaction, procedure maturity, risk level, professor-review requirement, and access policy to decide whether to answer, warn, clarify, audit, probe, review, request professor review, request learning, or abstain.

## Covered Inputs

- Neuro patch FR-051, FR-052 and NFR-031.
- Cognitive Self-Regulation FR-056, FR-058, FR-060 and NFR-037 through NFR-041.
- Patch finding H-06.
- Existing v2 recall, probing regression/calibration, MAF, UI, and governance design.

## Prerequisites

- Claim/evidence/belief ledger exists.
- Workspace and attention decisions exist.
- Prediction errors and salience signals exist.
- Recall traces include selected claims/evidence and inhibited candidates.
- Probe calibration records exist.
- Procedure skill maturity exists.
- Self-regulation assessment and answer posture records exist.
- Calibration health aggregates exist or missing aggregate behavior is explicit.
- Professor review escalation exists for high-impact professor-review-required paths.
- `01b-score-geometry-driver` provides answer-gate score spaces, abstention/warning shapes, and scalar confidence projection policy.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\24-metamemory-confidence-and-abstention.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\05-recall-orchestrator.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\15-interactive-memory-probing.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\16-probing-regression-and-calibration-loop.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\27-cognitive-self-regulation-layer.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\29-calibration-health-and-probing-training.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\30-professor-review-and-escalation.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\10-security-governance-and-provenance.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.NeuroPatchContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.SelfRegulationContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md

## Deliverables

- Answer gate request/decision records and service.
- Answer rendering rules for source-backed claims, synthesis, assumptions, uncertainty, stale/contested points, context boundaries, and next actions.
- Trace integration for answer gate id, decision, warnings, and blocked candidates.
- Probe/learning/review/source-audit action integration.
- Self-regulation assessment/posture enforcement.
- Professor-review-required enforcement for high-impact novelty, poor calibration, or weak competence.
- Calibration feedback loop from probing and regression outcomes.

## Dependency Impact

- Recall and MAF answer rendering must call the answer gate.
- Dialogue Workbench must show answer-gate warnings and decisions.
- Epistemic Drive can consume abstention/source-audit/probe decisions as gap evidence.
- High-risk procedure answers must respect procedure maturity and validation state.
- Self-Regulation UI must show posture, triggers, warnings, professor-review status, and required next actions.
- Answer rendering must not become looser than self-regulation without a new score trace.

## Validation Depth

- Unit tests for answer, warning, clarification, source audit, probe, review, learning request, and abstention decisions.
- Negative tests for source-poor high-confidence answers, contested claims, ambiguous Docker context, redaction-limited answers, and high-risk unvalidated procedures.
- Trace tests proving answer gate decisions are persisted and visible.
- Integration tests proving assessment/posture are consumed and enforced.
- Negative tests proving answer gate cannot become looser than self-regulation without a new trace.
- Professor-review-required tests for high-impact novelty and poor local competence.
- Browser proof where the decision/warnings render in Dialogue Workbench or recall trace UI.
- Performance review for answer-gate hot path.
- Score geometry tests for source-poor, contested, ambiguous, redacted, and high-risk answer-gate shapes.

## Implementation Steps

1. Add answer gate entities/configurations and service.
2. Add policy inputs for claims, evidence anchors, context frames, calibration, redaction, risk, and procedure maturity.
3. Add self-regulation assessment/posture and professor-review inputs.
4. Add trace fields and renderer contracts.
5. Integrate with recall, probing, MAF context contribution, Epistemic Drive evidence, and self-regulation UI evidence.
6. Add tests and browser proof for warning/abstention/professor-review rendering.

## Scope Exceptions

- Do not implement full Dialogue Workbench UI here except minimal proof surfaces needed for answer-gate visibility.
- Do not tune final confidence thresholds without later calibration evidence.
- Do not use display confidence as the decision model.
- Do not implement self-model, calibration health, or professor review here; consume their closed contracts.

## Do Not Do

- Do not hide uncertainty behind fluent wording.
- Do not let answer gate be a dashboard-only annotation.
- Do not bypass access/redaction policy for source sufficiency.
- Do not answer high-risk unvalidated procedure questions as if they were validated.
- Do not downgrade self-regulation-required review/professor-review/abstention to a normal answer without a new trace.

## Acceptance Checklist

- Answer gate can answer, warn, clarify, audit, probe, review, request learning, or abstain.
- Source sufficiency, context fit, belief state, calibration, contradiction, staleness, redaction, risk, and procedure maturity affect decisions.
- Self-regulation assessment and answer posture affect decisions.
- Professor-review-required posture is enforced.
- Gate can become stricter than self-regulation but not looser without new trace.
- Answer-gate decisions preserve score vectors, matched shapes, missing dimensions, and derived confidence projection.
- Decision is persisted in recall/probe trace.
- UI/workbench can show warnings and required next actions.
- Abstention can become Epistemic Drive evidence without creating truth.

## Proof Required

- Build/test output.
- EF model/index proof.
- Answer-gate decision fixture output.
- Browser screenshot when warnings/abstention are UI-visible.
- Implementation report with deviations.

## Browser Validation Logging

- Required when answer-gate decisions are visible.
- Record route, viewport, Playwright actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.
- Required visual checks: warnings are readable, source sufficiency is visible, blocked/abstained state is not styled as normal answer, dense viewport remains usable.

## Progression Gate

- Do not proceed to Dialogue Workbench completion, MAF answer injection completion, Epistemic Drive answer-gate evidence consumption, or Self-Regulation UI closure until answer-gate decisions are persisted, traceable, tested, and visible where relevant.
- Reopen this subbundle if any answer path bypasses the gate, ignores self-regulation posture, becomes looser without a new trace, or uses scalar confidence as the decision model.

## Suggested Agent Prompt

Implement Metamemory Answer Gate as an answer-time safety and uncertainty boundary. Use claims, evidence, context, calibration, self-regulation assessment, answer posture, professor review requirement, redaction, procedure maturity, and policy to decide whether to answer, warn, clarify, audit, probe, review, request professor review, request learning, or abstain.
