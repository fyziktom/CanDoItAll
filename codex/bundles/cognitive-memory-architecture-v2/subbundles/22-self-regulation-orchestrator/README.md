# 22 Self-Regulation Orchestrator

## Status

- Ready after `21-cognitive-self-model` and `23-calibration-health-and-probing-training`.
- Critical foundation for `24-professor-review-escalation`, `19-metamemory-abstention-calibration`, `13-interactive-memory-probing-workbench`, `25-self-regulation-ui`, and `12-epistemic-drive-engine`.
- Implementation not started.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.

## Objective

Implement self-regulation assessment, humility trigger evaluation, confidence reinforcement evaluation, and answer posture selection as traceable score-geometry-backed services.

## Covered Inputs

- `inputs/07-cognitive-self-regulation-patch-reference.md`.
- FR-056, FR-057, FR-058, NFR-037, and NFR-041.
- Patch requirement that Self-Regulation coordinates workspace, attention, probing, calibration, metamemory, and professor review without becoming a truth source.

## Prerequisites

- Active self-model, competence, known failure pattern, and policy profiles exist.
- Calibration health aggregates exist or the missing-aggregate behavior is explicit and traceable.
- Score geometry exposes self-regulation assessment and answer posture score spaces.
- Workspace, claims/evidence, source sufficiency, recall traces, redaction/access context, and risk categories are available as assessment inputs.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\27-cognitive-self-regulation-layer.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\28-self-model-and-epistemic-identity.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\29-calibration-health-and-probing-training.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\18-cognitive-workspace-and-attention-router.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\24-metamemory-confidence-and-abstention.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.SelfRegulationContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.NeuroPatchContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\self-regulation-test-matrix.md

## Deliverables

- `ISelfRegulationOrchestrator` implementation.
- `IHumilityTriggerEngine` implementation.
- `AnswerPostureDecision` persistence and trace integration.
- Assessment state classification for calibrated, exploratory, overconfident, underconfident, defensive, fragmented, source-poor, high-risk-unverified, and professor-review-needed states.
- Integration points for attention routing and metamemory answer gate inputs.
- Tests for humility triggers, posture selection, scalar-only rejection, and trace/audit metadata.

## Dependency Impact

- Attention routing must consume assessment and posture ids before choosing answer, clarification, source audit, probe, review, learning, replay, or abstention.
- Metamemory answer gate cannot render user/agent answers safely until this subbundle closes or is explicitly bypassed by a blocker decision.
- Professor review routing depends on assessment states and humility triggers.
- Probing, calibration health, and Epistemic Drive consume assessment/posture outcomes as evidence.

## Validation Depth

- Unit tests for each humility trigger: source-poor high-risk, high contradiction, wrong-scope pattern, recent correction, generated summary primary support, weak domain, high-impact unvalidated procedure, redaction prevents proof, stale volatile source, and cognitive load saturation.
- Unit tests for confidence reinforcement from probes, regression, human review, workflow validation, independent sources, and stable project decisions.
- Unit tests for every posture kind.
- Negative tests proving display confidence alone cannot select posture.
- Trace tests proving assessment and posture preserve evidence refs, score trace, actor/model profile, algorithm/profile version, and timestamp.
- Integration tests proving attention and answer gate receive assessment/posture ids.

## Implementation Steps

1. Add persistence for self-regulation assessments, humility triggers, confidence reinforcements, and answer posture decisions.
2. Implement assessment input assembly from workspace, claim/evidence, recall, calibration, access/redaction, and risk context.
3. Implement humility trigger and confidence reinforcement evaluation through score geometry.
4. Implement posture selection with required operations and warnings.
5. Wire assessment/posture ids into attention routing and answer-gate request paths.
6. Add contract, unit, integration, and negative tests.

## Scope Exceptions

- Do not implement the professor review service here; only route to `ProfessorReviewRequired` and required operation metadata.
- Do not implement UI here.
- Do not tune final threshold values from production behavior; use deterministic fixtures.

## Do Not Do

- Do not use scalar confidence as the decision model.
- Do not hide missing dimensions by defaulting them to neutral.
- Do not let self-regulation override access/redaction/source policy.
- Do not mutate canonical memory from an assessment or posture decision.

## Acceptance Checklist

- Assessment records include self-model id, competence profiles, known failure pattern matches, score trace, state, warnings, required operations, and evidence refs.
- Humility triggers and confidence reinforcements are persisted and traceable.
- Posture decision supports all defined posture kinds.
- Attention Router consumes assessment/posture.
- Metamemory Answer Gate can consume assessment/posture.
- Scalar-only posture selection is rejected.

## Proof Required

- Build/test output.
- Fixture output for all self-regulation states and posture decisions.
- Integration proof showing attention and answer-gate request records reference assessment/posture ids.
- Negative test output for scalar-only, missing-dimension, access-bypass, and direct truth mutation attempts.
- Execution report and workbook updates with proof paths.

## Browser Validation Logging

- N/A for this backend orchestration phase.
- Browser proof is required later in `19-metamemory-abstention-calibration`, `13-interactive-memory-probing-workbench`, and `25-self-regulation-ui` when these decisions become visible.

## Progression Gate

- Do not proceed to `24-professor-review-escalation` or reopen `19-metamemory-abstention-calibration` until assessment/posture persistence, trigger evaluation, attention integration, answer-gate input integration, and scalar-only rejection tests pass.
- Reopen this subbundle if any downstream answer path can render without assessment/posture or without a documented exception trace.

## Suggested Agent Prompt

Implement the Self-Regulation Orchestrator as the traceable control layer between workspace/attention/calibration and the answer gate. Use score geometry, structured self-model data, humility triggers, and evidence-backed reinforcement. Prove every posture path and reject scalar-only confidence decisions.
