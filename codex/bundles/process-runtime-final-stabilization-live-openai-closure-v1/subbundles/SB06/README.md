# SB06: Final stabilization decision and handoff

## Status
Prepared.

## Objective
Produce a final stabilization decision based on functional evidence, live OpenAI result, UI proof, and boundary scans.

## Exact Source References
- `repo://codex/bundles/process-runtime-stabilization-release-closure-v1/reviews/02-release-decision.md`
- `repo://codex/bundles/process-template-ui-live-e2e-runtime-readiness-v1/reviews/01-execution-report.md`
- New transcripts from SB01-SB05

## Implementation Steps
- Run build.
- Run full unit suite.
- Run focused integration matrix.
- Run final Playwright rerun.
- Record live OpenAI result.
- Produce final decision: `merge-ready-for-stabilization`, `runtime-stable-live-blocked`, or `not-runtime-stable`.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- Build 0 warnings/0 errors.
- Unit suite green.
- Focused process runtime matrix green.
- Playwright launch-to-completed green.
- Live OpenAI classified as pass/fail/skipped with reason.
- Final decision is explicit.

## Proof Required
- Build transcript.
- Unit transcript.
- Focused integration transcript.
- Playwright transcript.
- Live OpenAI transcript.
- Final release decision markdown.

## Browser Validation Logging
Required final Playwright rerun if SB04 affected UI or previous UI proof is part of release decision.

## Progression Gate
Bundle completes only when final decision is explicit and no skipped live test is reported as pass.
