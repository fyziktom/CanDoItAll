# SB18: 18-final-red-team-and-release-readiness

## Goal

Final red-team closure before real testing.

## Required work

- Run full build and focused tests.
- Red-team: stale artifact, unreadable content, empty content hash, wrong execution run, QA product mutation, A2A/handoff mismatch, workflow artifact mismatch, operator decision substituting deliverable.
- Write final release-readiness report and list deferred MAF 1.6 features.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB18` are updated and downstream subbundles can rely on the behavior.
