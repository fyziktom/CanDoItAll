# SB13: 13-artifact-dedupe-and-content-hash-race-hardening

## Goal

Harden artifact dedupe/content hash under concurrency and retry.

## Required work

- Add concurrency tests for two attempts recording same projection identity.
- Ensure collision errors are actionable and do not poison recovery.
- Verify content hash is computed before identity hash and identity changes when content changes if intended.
- Verify old no-content records cannot block later valid recovery artifacts.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB13` are filled and the downstream dependency is safe.
