# 24 Professor Review Escalation

## Status

- Completed
- Completion detail: Passed on 2026-05-16 follow-up implementation.
- Closure proof: professor review creation/completion APIs persist governed challenge-review suggestions and outcomes without direct truth mutation; verified by focused unit/integration tests and PostgreSQL smoke counts at `validation/postgres-smoke/evidence/20260516-231507/postgres-advanced-table-counts.csv`.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.

## Objective

Formalize large-model or expert professor review as a governed challenge/audit/escalation mechanism that can propose probes, source audits, regressions, learning tasks, review items, and mutation candidates without becoming source truth.

## Covered Inputs

- `inputs/07-cognitive-self-regulation-patch-reference.md`.
- FR-060, NFR-037, NFR-040, and NFR-041.
- User intent to use stronger models as professor/challenger/auditor rather than unquestioned authority.

## Prerequisites

- Self-regulation assessment can classify `ProfessorReviewNeeded`.
- Answer posture selection can emit `ProfessorReviewRequired`.
- Access/redaction policy and memory access context are available.
- Claim/evidence/belief, review policy, mutation authority, source audit, probing, and regression candidate flows exist.
- Score geometry exposes professor-review routing score space and evidence kinds.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\27-cognitive-self-regulation-layer.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\30-professor-review-and-escalation.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\20-claim-evidence-belief-ledger.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\10-security-governance-and-provenance.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.SelfRegulationContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\self-regulation-test-matrix.md

## Deliverables

- `IProfessorReviewService` implementation boundary and request/result persistence.
- Review modes for Socratic challenge, contradiction hunt, architecture review, calibration review, source sufficiency review, alternative hypothesis review, failure mode review, and learning expansion.
- Governance layer that routes suggestions to probes, source audits, review items, regressions, learning proposals, or mutation candidates.
- External knowledge and access-level trace metadata.
- Tests proving professor output cannot directly create truth, bypass mutation authority, or inspect redacted content.

## Dependency Impact

- `19-metamemory-abstention-calibration` can enforce professor-review-required posture before rendering high-impact synthesis.
- `12-epistemic-drive-engine` can consume professor suggestions as learning-expansion evidence only through governance.
- `25-self-regulation-ui` displays professor review status, critique, missing evidence, recommended posture, and resulting governed actions.
- Calibration health consumes validated professor disagreement/confirmation outcomes.

## Validation Depth

- Unit tests for each review mode and escalation trigger.
- Negative tests proving professor results cannot directly mutate claims, memory items, procedures, or belief states.
- Access/redaction tests proving professor context excludes restricted evidence unless policy allows it.
- Trace tests for model profile, prompt/profile version, review mode, input context ids, output hash, and resulting actions.
- Integration tests proving suggestions route through source audit, probe, regression, review, learning, or mutation authority.

## Implementation Steps

1. Add professor review request/result records and service boundary.
2. Add review mode routing and score-geometry escalation traces.
3. Add governed action conversion for suggested probes, source audits, regressions, learning proposals, review items, and mutation candidates.
4. Integrate with self-regulation posture and answer gate required operations.
5. Add access/redaction, governance, scalar-only, and direct-truth-mutation negative tests.
6. Update execution report/workbook proof paths.

## Scope Exceptions

- Do not require a real external model provider in unit tests; use deterministic fake professor profiles.
- Do not build the UI in this phase.
- Do not let professor review tune calibration without outcome validation.

## Do Not Do

- Do not treat professor review as source truth.
- Do not bypass source anchors, review policy, redaction, or mutation authority.
- Do not send redacted or restricted context to an external model by default.
- Do not let professor output become active memory without governed evidence.

## Acceptance Checklist

- Professor review request/result records include review mode, model profile, prompt/profile version, access context, input ids, output hash, trace, and human-review requirement.
- Escalation triggers include weak competence, high-impact novelty, contradiction pressure, poor calibration health, repeated probing failures, and complex architecture synthesis.
- Professor suggestions convert only to governed actions.
- Negative tests prove no direct truth mutation and no redaction/access bypass.

## Proof Required

- Build/test output.
- Review-mode fixture output.
- Governance routing proof for probe/source-audit/regression/review/learning/mutation-candidate suggestions.
- Negative test output for direct truth mutation and redaction bypass.
- Execution report and workbook updates with proof paths.

## Browser Validation Logging

- N/A for this backend escalation phase.
- Browser proof is required later in `25-self-regulation-ui`.

## Progression Gate

- Do not proceed to self-regulation UI or high-impact answer rendering that depends on professor review until professor request/result traces, governance routing, and policy-bypass negative tests pass.
- Reopen this subbundle if downstream code treats professor review output as canonical truth or uses it to bypass access/redaction/mutation policy.

## Suggested Agent Prompt

Implement Professor Review as governed challenge and audit input. Preserve model/profile/access traces, route suggestions through existing review/probe/source-audit/regression/learning/mutation-authority paths, and prove professor output cannot become source truth directly.
