# 21 Cognitive Self-Model

## Status

- Completed
- Completion detail: Passed on 2026-05-16 follow-up implementation.
- Closure proof: structured self-model, role/profile, risk, known-gap, and policy profile records are implemented through typed value objects and advanced persistence; verified by focused unit/integration tests and PostgreSQL smoke evidence at `validation/postgres-smoke/evidence/20260516-231507/99-summary.json`.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.

## Objective

Implement the durable, scoped, evidence-backed self-model records that describe operating principles, allowed/restricted task categories, competence profiles, weak domains, known failure patterns, and self-regulation policy.

This is not a prompt persona. It is structured control data used by later self-regulation assessment and answer posture decisions.

## Covered Inputs

- `inputs/07-cognitive-self-regulation-patch-reference.md`.
- FR-055 and NFR-038 through NFR-039.
- Patch requirement for healthy artificial confidence without anthropomorphic consciousness claims.

## Prerequisites

- Score geometry driver exposes self-model competence and failure-pattern shape evaluation.
- Claim/evidence/belief ledger and evidence anchors exist.
- Workspace frames and attention traces exist.
- Prediction error and salience signal records exist.
- Probe/core regression and calibration records exist so initial competence and failure patterns can be evidence-backed.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\27-cognitive-self-regulation-layer.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\28-self-model-and-epistemic-identity.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\29-calibration-health-and-probing-training.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.SelfRegulationContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.ScoringContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\self-regulation-test-matrix.md

## Deliverables

- EF records/configurations for cognitive self-model, domain competence profile, known failure pattern, and self-regulation policy profile.
- Application service for loading active self-model data by project, role, model profile, task type, and domain/knowledge region.
- Evidence-backed update proposal path for self-model and failure-pattern changes.
- Contract tests for scoping, evidence requirements, profile versioning, and non-anthropomorphic data shape.
- Seed profile fixtures for normal assistance, strict safety, probing exam, workflow automation, exploratory research, professor review, and high-risk procedure modes.

## Dependency Impact

- `22-self-regulation-orchestrator` cannot assess a request without active self-model and failure-pattern records.
- `23-calibration-health-and-probing-training` attaches aggregates to domain competence and self-model profile versions.
- `24-professor-review-escalation` uses weak competence and known failure patterns as escalation inputs.
- `19-metamemory-abstention-calibration` consumes posture constraints derived from this self-model.
- `25-self-regulation-ui` displays self-model scope, competence, known failure patterns, and profile version.

## Validation Depth

- Unit tests for project/domain/task/model-profile scoping.
- Unit tests proving self-model update without evidence is rejected.
- Unit tests for known failure pattern matching against score shapes.
- Persistence tests proving profile version and algorithm version are stored.
- Negative tests proving prompt persona, user praise, or generated summary cannot update competence or truth state.
- Architecture review proving no self-model path bypasses mutation authority, access policy, redaction, or review policy.

## Implementation Steps

1. Add strongly typed entity/model records for self-model, competence profile, known failure pattern, and policy profile.
2. Add EF configuration and indexes for project, model profile, role, domain, task type, policy key, and profile version.
3. Add `ICognitiveSelfModelStore` implementation and deterministic test fakes.
4. Add evidence-backed update proposal handling without direct canonical truth mutation.
5. Add seed/test fixtures for strong domain, weak domain, known wrong-scope pattern, and generated-summary primary-support pattern.
6. Add tests and update execution report/workbook proof paths.

## Scope Exceptions

- Do not tune final competence thresholds from production data in this phase.
- Do not build operator UI in this phase.
- Do not implement professor review service in this phase.

## Do Not Do

- Do not model self-model as prompt text, persona, emotion, or consciousness.
- Do not let the self-model mark claims true.
- Do not let user praise or a single success change competence.
- Do not hide known failure patterns because they are inconvenient.
- Do not use stringly typed status/mode/state values where enums/options are appropriate.

## Acceptance Checklist

- Cognitive self-model is structured, scoped, evidence-backed data.
- Domain competence profiles reference score geometry and calibration evidence where available.
- Known failure patterns include trigger kinds, score shapes, mitigation, regression/probe links, and review requirement.
- Policy profiles define allowed postures and review/probe/abstention thresholds without scalar-only behavior.
- Updates are versioned and auditable.
- Negative tests reject persona-only and evidence-free updates.

## Proof Required

- Build/test output.
- EF model/index proof.
- Contract/model test output for self-model scoping and profile versioning.
- Negative test output for persona-only, praise-only, and generated-summary-only updates.
- Execution report and workbook updates with proof paths.

## Browser Validation Logging

- N/A for this backend foundation phase.
- Browser proof is required later in `25-self-regulation-ui`.

## Progression Gate

- Do not proceed to `22-self-regulation-orchestrator` until active self-model loading, competence profile lookup, known failure pattern lookup, and evidence-backed update rejection tests pass.
- Reopen this subbundle if downstream orchestration needs prompt-persona fallback, unversioned competence state, or direct truth mutation from self-model updates.

## Suggested Agent Prompt

Implement the Cognitive Self-Model foundation as structured, scoped, evidence-backed records. Preserve source truth and mutation authority. Prove profile versioning, competence scoping, known failure pattern matching, and rejection of persona-only or evidence-free updates before downstream self-regulation orchestration starts.
