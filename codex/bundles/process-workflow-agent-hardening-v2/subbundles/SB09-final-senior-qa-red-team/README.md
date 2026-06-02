# SB09 Final senior QA red-team and release gate

## Status

Ready for implementation.  
Critical foundation: **Yes**

## Objective

Perform hostile final validation before the follow-up bundle can be marked completed.

## Covered Inputs

R14 and all raw user notes.

## Prerequisites

SB01-SB08 completed or explicitly blocked with user-visible blocker.

## Exact Source References

- `bundle://reviews/01-execution-report.md`
- `bundle://proof/SB01/manifest.md through bundle://proof/SB08/manifest.md`
- `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md`

## Deliverables

- Final red-team report.
- Fake-proof resistance report.
- Billing reconciliation review.
- Tool registry drift review.
- Contract fail-closed review.
- Real E2E proof review.
- Completed-stage bundle validator output.

## Dependency Impact

This subbundle affects downstream proof and must be treated as a dependency exactly as modeled in `bundle://plan/01-phase-plan.md`. If this subbundle fails, all downstream subbundles that depend on its runtime behavior or proof contract must be reopened.

## Validation Depth

Critical subbundle validation requires semantic adequacy proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, changed-file hashes, and command/browser transcripts where applicable.

## Implementation Steps

1. Reread raw user request and traceability matrix.
2. Run prepared/completed validators.
3. Run proof-quality checker against old V1 proof and new proof.
4. Verify all critical manifests cite existing paths and include failing-first plus passing proof.
5. Run at least one adversarial process case with missing operation contract.
6. Run at least one adversarial unknown/command tool case.
7. Verify provider usage and OpenAI reconciliation report.
8. Review screenshots manually against UI questions.
9. Mark raw notes Solved/Partially solved/Blocked with proof links.

## Scope Exceptions

None planned. If implementation discovers a legacy compatibility exception, record it in this file and in `traceability/` before continuing.

## Do Not Do

Do not pass the gate from prose-only proof. Do not mark external billing reconciliation solved without provider/export evidence. Do not accept old SB08-style fixture proof for real process E2E.

## Acceptance Checklist

- [ ] Source references were reopened before editing.
- [ ] Implementation is the smallest correct change set for this subbundle.
- [ ] Failing-first proof was captured for behavior-changing critical work.
- [ ] Passing proof was captured after implementation.
- [ ] Anti-stub audit was run.
- [ ] Raw notes owned by this subbundle were closed or explicitly blocked.
- [ ] Downstream dependency impact was reviewed before moving on.

## Proof Required

Final red-team report, validation transcripts, manifest audit, proof-quality expected-failure/pass reports, raw note closure table, browser analytics, completed-stage validator output.

## Browser Validation Logging

Review all SB04/SB08 screenshots and add visual inspection notes.

## Progression Gate

Bundle can be marked completed only when every P0 is solved or explicitly blocked by missing external credential/environment and the blocker is not hidden.

## Suggested Agent Prompt

You are implementing `SB09 Final senior QA red-team and release gate` in `fyziktom/CanDoItAll` on branch `development`. Read this subbundle README, the root README, `plan/01-phase-plan.md`, `traceability/`, and all exact source references before editing. Implement only this subbundle. Do not close it without the required semantic proof, transcripts, changed-file hashes, anti-stub audit, and raw-note closure update.
