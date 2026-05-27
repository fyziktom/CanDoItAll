# SB10: 10-process-artifact-dedupe-scope-correctness

## Goal

Fix/prove artifact dedupe scope correctness.

## Required work

- Inspect `RecordArtifactAsync` projection identity and external reference dedupe queries.
- Ensure dedupe is scoped to process run + compatible step run + compatible artifact expectation, or returns a collision error.
- Add tests: same run/different step same identity, same run/different expectation same external reference, same step/same expectation same identity.
- Do not rely on projection identity hash alone unless it includes step and expectation and tests prove it.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB10` are updated and downstream subbundles can rely on it.
