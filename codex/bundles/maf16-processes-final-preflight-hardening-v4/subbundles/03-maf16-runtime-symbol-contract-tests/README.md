# SB03: 03-maf16-runtime-symbol-contract-tests

## Goal

Strengthen reflection tests so they cannot produce false confidence.

## Required work

- Check type full names, assembly names, assembly versions, and package expectations.
- Test presence/absence of expected symbols with explicit names and namespaces where possible.
- Add negative test for local stub classes shadowing MAF symbols.
- Persist reflection output as proof.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB03` are filled and the downstream dependency is safe.
