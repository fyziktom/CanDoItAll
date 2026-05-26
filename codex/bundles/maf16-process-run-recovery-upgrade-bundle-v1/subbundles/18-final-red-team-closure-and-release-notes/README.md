# SB18: 18-final-red-team-closure-and-release-notes

## Goal

Final closure and release notes.

## Required work

- Run full build and focused test suites.
- Run red-team scenarios: stale artifact, wrong run, org-scoped current artifact, missing content hash, pending manager approval, QA mutation attempt, A2A/workflow mapping failure.
- Update process/agent skills and docs for MAF 1.6.
- Write a final release note: packages changed, APIs touched, process failure fixed, remaining risks.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB18` are updated and the next subbundle can safely depend on it.
