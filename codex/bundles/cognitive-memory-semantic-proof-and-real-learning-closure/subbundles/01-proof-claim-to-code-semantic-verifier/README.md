# Proof claim-to-code semantic verifier

## Status

- Status: `Ready`

## Objective

Harden the bundle workflow and validator so execution report capability labels must be backed by literal production source behavior, tests, and negative fixtures.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://codex/skills/bundles/candoitall-bundle-workflow/SKILL.md
- repo://codex/skills/bundles/candoitall-bundle-execution/SKILL.md
- repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md
- repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md
- repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py
- repo://codex/bundles/cognitive-memory-production-signal-and-deep-synthesis-followup/reviews/01-execution-report.md

## Deliverables

- Add claim-to-code semantic verification rules for capability labels: embedding-backed, Czech/diacritic, provider-backed, automatic, scheduled, claim-specific, line-level, domain synthesis, and portable proof.
- Add fake completed-bundle fixtures that claim Czech/diacritic support while using English-only source proof, and embedding-backed support while using lexical-only provider proof; both must fail completed validation.
- Update workflow/execution/validator skills to require installing the updated active skill before feature subbundles continue.
- Add a proof claim-to-code matrix template and require it in critical completed proof manifests when semantic capability labels are used.

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

- Extract capability labels from completed execution reports and semantic proof sections.
- Implement deterministic label-specific validators for common shallow claims.
- Add negative fixtures and a positive fixture that uses literal source patterns.
- Record active skill synchronization hashes using portable references only.

## Do Not Do

- Do not accept a source assertion that merely repeats the report claim.
- Do not treat class names as proof of behavior when dependencies/calls contradict the name.
- Do not allow broad proof tokens such as `.cs` or `test` to satisfy a semantic label by themselves.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB01/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB01/semantic-invariants.md`.
- Completed: `bundle://proof/SB01/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB01/transcripts/passing.txt`.
- Completed: `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB01/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Proof claim-to-code semantic verifier. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.
