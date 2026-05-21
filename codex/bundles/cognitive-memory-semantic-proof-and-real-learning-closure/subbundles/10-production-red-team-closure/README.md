# Production red-team end-to-end closure

## Status

- Status: `Ready`

## Objective

Prove the complete cognitive-memory learning loop through production paths and portable proof.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryModuleRegistrationTests.cs
- repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py
- repo://codex/bundles/cognitive-memory-production-signal-and-deep-synthesis-followup/reviews/01-execution-report.md

## Deliverables

- End-to-end red-team scenario: Czech professor teaches naturally, temporary anchor is captured, dream/cluster compares with independent evidence, a task-facing recall answer uses derived memory, outcome feedback emits accepted-use, assimilation/fading occurs, and reference-on-demand resolves exact lineage.
- Final completed-stage validator transcript from a moved checkout path.
- Execution report with claim-to-code matrix for every semantic capability label.
- Anti-stub audit across production and tests.
- Browser/Playwright evidence if any UI route/component changed.

## Dependency Impact

- Update downstream subbundles, tests, traceability, and proof artifacts if this subbundle changes contracts or service boundaries.
- Re-run prepared-stage validation if this README, requirements, or phase gates are edited.
- Preserve compatibility with existing persistence unless this subbundle explicitly requires schema changes.

## Validation Depth

- Add failing-first proof before production behavior changes.
- Add focused passing tests for the behavior and affected regression tests.
- Include source assertions that prove production behavior, not only tests.
- Include anti-stub audit and red-team negative cases.
- Use portable `repo://` and `bundle://` references only in proof artifacts.

## Implementation Steps

- Run focused unit tests for each semantic invariant.
- Run affected cognitive-memory test suite.
- Run completed-stage bundle validation in original and moved checkouts.
- Search final proof for forbidden absolute paths and meta dream text.
- Update raw note closure with proof links only after all gates pass.

## Do Not Do

- Do not count a direct service call as end-to-end workflow proof unless the service is invoked by the actual outcome handler.
- Do not hide unresolved gaps in warnings.
- Do not ship if any capability label is only supported by class names or report prose.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB10/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB10/semantic-invariants.md`.
- Completed: `bundle://proof/SB10/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB10/transcripts/passing.txt`.
- Completed: `bundle://proof/SB10/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB10/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Production red-team end-to-end closure. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.
