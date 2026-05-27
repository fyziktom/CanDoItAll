# SB01: 01-current-head-and-previous-proof-audit

## Goal

Re-open current source and prior proof to classify what is truly implemented.

## Required work

- Read previous execution report and proof manifests.
- Verify source files referenced by prior proof exist.
- Classify each previous claim as implemented, proof-only, deferred, or unverified.
- Do not modify production behavior in this subbundle.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB01` are filled and the downstream dependency is safe.
