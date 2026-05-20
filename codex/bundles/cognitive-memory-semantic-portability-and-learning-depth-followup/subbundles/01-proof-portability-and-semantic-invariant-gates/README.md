# SB01 - Proof Portability And Semantic Invariant Gates

## Status

- Status: `Completed`
- Criticality: `Critical`
- Execution order: `SB01`

## Objective

Make the bundle workflow skill and completed-stage validator portable and invariant-backed before any more production work continues.

## Covered Inputs

- R-01
- R-02
- R-16

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB01/semantic-invariants.*` before closure.

## Exact Source References

- repo://codex/skills/bundles/candoitall-bundle-workflow/SKILL.md
- repo://codex/skills/bundles/candoitall-bundle-execution/SKILL.md
- repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md
- repo://codex/skills/bundles/candoitall-subbundle-validator/SKILL.md
- repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md
- repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py
- repo://codex/bundles/cognitive-memory-followup/reviews/01-execution-report.md

## Deliverables

- Add portable reference resolution for repo:// and bundle:// paths, plus optional --repo-root and --bundle-root arguments.
- Fix Windows absolute path handling in exact source references without relying on the current OS Path implementation.
- Require semantic invariant contracts for completed critical subbundles.
- Add fake-proof fixtures that fail when paths are machine-specific, invariant contracts are missing, or invariant IDs are not cited by transcripts.

## Dependency Impact

- Upstream invariants from earlier subbundles must remain green.
- Downstream cognitive-memory services that consume changed contracts, entities, options, or generated records must be retested.
- Persistence changes require SQLite and PostgreSQL migration/model-snapshot proof where applicable.

## Validation Depth

- Add or use failing-first semantic tests for the owned invariants.
- Add targeted passing tests and at least one adversarial negative test.
- Run anti-stub audit against changed production files.
- For backend-only changes, browser validation can be N/A with an explicit reason; UI changes require Playwright evidence.

## Implementation Steps

- Reproduce the portability failure by validating the current completed bundle after moving or checking it from a non-Windows path.
- Update skills to prefer repo:// and bundle:// references over machine-local absolute paths.
- Update validate_bundle.py to resolve portable references and validate semantic invariant contracts.
- Add validator fixtures for Windows paths, POSIX paths, relocated bundle roots, missing invariant contract, and mismatched test/invariant IDs.
- Install the updated skills before executing SB02.

## Do Not Do

- Do not remove artifact-backed proof requirements.
- Do not accept report prose as a substitute for proof artifacts.
- Do not require every verifier to run on the original developer machine path.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics: `Completed`.
- Semantic invariant contract exists and is cited by the proof manifest: `bundle://proof/SB01/semantic-invariants.md`.
- Failing-first and passing transcripts exist for targeted tests: `bundle://proof/SB01/transcripts/fake-proof-fixtures.txt` and `bundle://proof/SB01/transcripts/positive-portable-fixture.txt`.
- Changed source files are hashed and mapped to invariant IDs: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`.
- No economic-governance scope creep is introduced: no cognitive-memory economic-governance files were touched in SB01.

## Proof Required

- Completed-stage validator transcript for fake fixtures and positive fixture: `bundle://proof/SB01/transcripts/fake-proof-fixtures.txt`, `bundle://proof/SB01/transcripts/positive-portable-fixture.txt`.
- Transcript showing a copied fixture validates through portable references after path normalization: `bundle://proof/SB01/transcripts/positive-portable-fixture.txt`.
- Hashes for modified skill and validator files: `bundle://proof/SB01/transcripts/changed-file-hashes.txt` and `bundle://proof/SB01/transcripts/active-skill-sync-hashes.txt`.

## Browser Validation Logging

- Backend-only. SB01 changed bundle workflow validation scripts, skill instructions, and proof fixtures only; no UI routes/components changed.

## Progression Gate

- Completed. The proof manifest, semantic invariant contract, targeted transcripts, anti-stub audit, active skill sync hashes, and downstream prepared-bundle validation are present under `bundle://proof/SB01/`.

## Suggested Agent Prompt

Implement SB01 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
