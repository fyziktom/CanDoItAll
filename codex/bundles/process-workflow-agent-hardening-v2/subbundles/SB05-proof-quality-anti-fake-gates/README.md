# SB05 Proof-quality anti-fake gates

## Status

Ready for implementation.  
Critical foundation: **Yes**

## Objective

Teach the bundle/proof validators to reject the exact proof bypass that V1 accepted.

## Covered Inputs

R10, R14; source evidence E09-E14.

## Prerequisites

SB04 proof shape defined. Read bundle validator and execution skill rules.

## Exact Source References

- `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-execution/SKILL.md`
- `repo://codex/bundles/process-workflow-agent-hardening-v1/proof/SB09/final-red-team-report.md`
- `repo://codex/bundles/process-workflow-agent-hardening-v1/reviews/01-execution-report.md`

## Deliverables

- Proof-quality checker integrated into completed-stage bundle validation.
- Rules that classify proof as production-path, fixture-only, migration/backfill, browser-only, or manual API proof.
- Hard failure for critical E2E proof with manual transitions, suppressed automation, empty execution runs, missing tool receipts, missing usage observations, or harness-generated source.
- Expected-failure transcript against the old V1 SB08 proof.
- Passing transcript against the new SB04 proof.

## Dependency Impact

This subbundle affects downstream proof and must be treated as a dependency exactly as modeled in `bundle://plan/01-phase-plan.md`. If this subbundle fails, all downstream subbundles that depend on its runtime behavior or proof contract must be reopened.

## Validation Depth

Critical subbundle validation requires semantic adequacy proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, changed-file hashes, and command/browser transcripts where applicable.

## Implementation Steps

1. Create a validator fixture that points at V1 SB08 proof and assert it fails for the known reasons.
2. Add production-path proof schema requirements for process E2E.
3. Add source scan that detects app source generation inside proof scripts when the proof claims app-generation behavior.
4. Add current-run binding checks for processRunId, stepRunId, executionRunId, artifact id/path, and provider response id when applicable.
5. Update bundle skills or references so future Codex agents cannot mark fixture-only proof complete for production behavior.
6. Capture active skill-root sync hashes when skill text changes.

## Scope Exceptions

None planned. If implementation discovers a legacy compatibility exception, record it in this file and in `traceability/` before continuing.

## Do Not Do

Do not weaken the validator to accept “usage unavailable” as a pass for provider-required process E2E. Do not allow status/count-only proof for critical behavior.

## Acceptance Checklist

- [ ] Source references were reopened before editing.
- [ ] Implementation is the smallest correct change set for this subbundle.
- [ ] Failing-first proof was captured for behavior-changing critical work.
- [ ] Passing proof was captured after implementation.
- [ ] Anti-stub audit was run.
- [ ] Raw notes owned by this subbundle were closed or explicitly blocked.
- [ ] Downstream dependency impact was reviewed before moving on.

## Proof Required

Old V1 SB08 expected-failure transcript, new SB04 passing transcript, fake-proof red-team notes, active skill sync hash proof, completed-stage validator output.

## Browser Validation Logging

N/A except screenshots used by SB04/SB08 proof. This subbundle validates proof quality.

## Progression Gate

No later refactor subbundle can close until the proof checker protects the critical production path.

## Suggested Agent Prompt

You are implementing `SB05 Proof-quality anti-fake gates` in `fyziktom/CanDoItAll` on branch `development`. Read this subbundle README, the root README, `plan/01-phase-plan.md`, `traceability/`, and all exact source references before editing. Implement only this subbundle. Do not close it without the required semantic proof, transcripts, changed-file hashes, anti-stub audit, and raw-note closure update.
