# SB06: Final stabilization decision and handoff

## Status
- Current status: Completed

## Objective
Produce a final stabilization decision based on functional evidence, live OpenAI result, UI proof, and boundary scans.

## Covered Inputs
- RN-001: Check whether processes now work like before.
- RN-002: If not, identify what refactoring broke and prepare a follow-up bundle.
- RN-003: Run a test with OpenAI using env and safe defaults.
- RN-004: Stabilize process functionality before further runtime extraction.

## Prerequisites
- SB01 through SB05 closure gates must be completed or honestly blocked.
- Final validation commands must use fresh transcripts.

## Exact Source References
- `repo://codex/bundles/process-runtime-stabilization-release-closure-v1/reviews/02-release-decision.md`
- `repo://codex/bundles/process-template-ui-live-e2e-runtime-readiness-v1/reviews/01-execution-report.md`
- `bundle://reviews/01-execution-report.md`

## Deliverables
- Final build transcript.
- Final unit transcript.
- Final focused integration transcript.
- Final Playwright transcript or explicit reuse decision backed by SB04 proof.
- Final release decision markdown.

## Dependency Impact
- This subbundle closes the bundle.
- If any prerequisite subbundle is blocked, final decision must be `runtime-stable-live-blocked` or `not-runtime-stable`, not merge-ready.

## Validation Depth
- Entry gate: verify SB01-SB05 gate rows and proof paths.
- Closure gate: build/unit/integration/UI/live evidence reconciled with final decision and final bundle validator.
- Semantic Adequacy Gate: record shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note closure, and red-team fake-proof audit in `bundle://proof/SB06/semantic-invariants.md`.

## Implementation Steps
- Run build.
- Run full unit suite.
- Run focused integration matrix.
- Run final Playwright rerun.
- Record live OpenAI result.
- Produce final decision: `merge-ready-for-stabilization`, `runtime-stable-live-blocked`, or `not-runtime-stable`.

## Scope Exceptions
- None planned. Any skipped live test must be classified as non-proof and cannot support merge-ready status.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- Build 0 warnings/0 errors or warnings are explicitly classified from transcript.
- Unit suite green.
- Focused process runtime matrix green or blocker classified.
- Playwright launch-to-completed green or blocker classified.
- Live OpenAI classified as pass/fail/skipped with reason.
- Final decision is explicit.

## Proof Required
- Build transcript.
- Unit transcript.
- Focused integration transcript.
- Playwright transcript.
- Live OpenAI transcript.
- Final release decision markdown.
- `bundle://proof/SB06/manifest.md` with changed-file hashes and portable artifact references.
- `bundle://proof/SB06/semantic-invariants.md` with invariant IDs cited by transcripts.
- Red-team or verifier artifact auditing fake-proof resistance.

## Browser Validation Logging
- Required final Playwright rerun if SB04 affected UI or previous UI proof is part of release decision.

## Progression Gate
- Bundle completes only when final decision is explicit and no skipped live test is reported as pass.

## Suggested Agent Prompt
- Reconcile all transcripts, browser proof, live smoke result, and boundary scans into one final stabilization decision. Run final validators and update raw note closure.
