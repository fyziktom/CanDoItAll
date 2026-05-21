# Portable proof and installed skill sync closure

## Status

- Status: `Completed`
- Completed proof: `bundle://proof/SB02/manifest.md`
- Semantic invariants: `bundle://proof/SB02/semantic-invariants.md`

## Objective

Fix proof portability and active-skill synchronization so completed bundles validate from moved checkouts and never depend on local user-profile paths.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py
- bundle://analysis/03-evidence-notes.md
- bundle://inventories/01-reviewed-files.md
- repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/artifact-proof-machine-specific-paths/proof/SB01/transcripts/passing.txt

## Deliverables

- Reject absolute local paths in proof manifests, changed-file hashes, active-skill sync proofs, and execution report validator commands unless they are explicitly marked as non-artifact working-directory context.
- Require final completed validation from a copied/moved checkout path with portable proof resolution.
- Repair the current completed bundle proof or add a regression fixture proving the old absolute active-skill path fails.
- Document active skill installation proof using hashes and portable repo skill paths, not user-profile artifact references.

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

- Reproduce the moved-checkout failure from the latest completed bundle.
- Strengthen validator path token extraction and portability rules.
- Add fixture that contains `C:\Users\...\.codex\skills` in a manifest and verify completed validation fails.
- Add positive fixture with portable active skill hash proof.

## Do Not Do

- Do not whitelist user-profile paths because they exist on the original machine.
- Do not put active-skill root files into proof artifact tables as if they were portable bundle artifacts.
- Do not close final validation without a moved-checkout transcript.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB02/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB02/semantic-invariants.md`.
- Completed: `bundle://proof/SB02/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB02/transcripts/passing.txt`.
- Completed: `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB02/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Portable proof and installed skill sync closure. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.

