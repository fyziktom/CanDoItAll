# SB18: 18-final-governance-red-team-and-release-readiness

## Goal

Final red-team and release readiness.

## Required work

- Red-team: wrong output root, stale artifact, wrong manager, artifact folder noise, missing tool/skill, workflow artifact mismatch, non-software process, recovery loop no-progress, docs/API drift.
- Run build and named test categories.
- Produce final release-readiness report with GO/NO-GO for broader real process testing.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB18` are updated and the next dependent workstream can rely on it.
