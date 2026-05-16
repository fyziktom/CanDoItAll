# 19 Metamemory Abstention Calibration

## Status

- Ready after `13a-probing-core-regression-calibration`, `18-procedural-skill-memory-simulation`, and `05-recall-orchestrator`.
- Required before user/agent answer rendering is considered safe.

## Objective

Add an answer-time metamemory gate that uses source sufficiency, context fit, belief state, confidence calibration, contradiction risk, staleness, redaction, procedure maturity, risk level, and access policy to decide whether to answer, warn, clarify, audit, probe, review, request learning, or abstain.

## Covered Inputs

- Neuro patch FR-051, FR-052 and NFR-031.
- Patch finding H-06.
- Existing v2 recall, probing regression/calibration, MAF, UI, and governance design.

## Prerequisites

- Claim/evidence/belief ledger exists.
- Workspace and attention decisions exist.
- Prediction errors and salience signals exist.
- Recall traces include selected claims/evidence and inhibited candidates.
- Probe calibration records exist.
- Procedure skill maturity exists.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\24-metamemory-confidence-and-abstention.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\05-recall-orchestrator.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\15-interactive-memory-probing.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\16-probing-regression-and-calibration-loop.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\10-security-governance-and-provenance.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.NeuroPatchContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md

## Deliverables

- Answer gate request/decision records and service.
- Answer rendering rules for source-backed claims, synthesis, assumptions, uncertainty, stale/contested points, context boundaries, and next actions.
- Trace integration for answer gate id, decision, warnings, and blocked candidates.
- Probe/learning/review/source-audit action integration.
- Calibration feedback loop from probing and regression outcomes.

## Dependency Impact

- Recall and MAF answer rendering must call the answer gate.
- Dialogue Workbench must show answer-gate warnings and decisions.
- Epistemic Drive can consume abstention/source-audit/probe decisions as gap evidence.
- High-risk procedure answers must respect procedure maturity and validation state.

## Validation Depth

- Unit tests for answer, warning, clarification, source audit, probe, review, learning request, and abstention decisions.
- Negative tests for source-poor high-confidence answers, contested claims, ambiguous Docker context, redaction-limited answers, and high-risk unvalidated procedures.
- Trace tests proving answer gate decisions are persisted and visible.
- Browser proof where the decision/warnings render in Dialogue Workbench or recall trace UI.
- Performance review for answer-gate hot path.

## Implementation Steps

1. Add answer gate entities/configurations and service.
2. Add policy inputs for claims, evidence anchors, context frames, calibration, redaction, risk, and procedure maturity.
3. Add trace fields and renderer contracts.
4. Integrate with recall, probing, MAF context contribution, and Epistemic Drive evidence.
5. Add tests and browser proof for warning/abstention rendering.

## Scope Exceptions

- Do not implement full Dialogue Workbench UI here except minimal proof surfaces needed for answer-gate visibility.
- Do not tune final confidence thresholds without later calibration evidence.

## Do Not Do

- Do not hide uncertainty behind fluent wording.
- Do not let answer gate be a dashboard-only annotation.
- Do not bypass access/redaction policy for source sufficiency.
- Do not answer high-risk unvalidated procedure questions as if they were validated.

## Acceptance Checklist

- Answer gate can answer, warn, clarify, audit, probe, review, request learning, or abstain.
- Source sufficiency, context fit, belief state, calibration, contradiction, staleness, redaction, risk, and procedure maturity affect decisions.
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

- Do not proceed to Dialogue Workbench completion, MAF answer injection completion, or Epistemic Drive answer-gate evidence consumption until answer-gate decisions are persisted, traceable, tested, and visible where relevant.
- Reopen this subbundle if any answer path bypasses the gate.

## Suggested Agent Prompt

Implement Metamemory Answer Gate as an answer-time safety and uncertainty boundary. Use claims, evidence, context, calibration, redaction, procedure maturity, and policy to decide whether to answer, warn, clarify, audit, probe, review, request learning, or abstain.

