# SB04: 04-artifact-storage-lineage-dedupe-and-retention-hardening

## Goal

Harden artifact identity, storage, hash, dedupe, and retention.

## Required work

- Audit projection identity hash inputs and external reference key truncation behavior.
- Add race/concurrency tests for repeated artifact records from retry/recovery attempts.
- Ensure old invalid/no-content records cannot block later valid recovered artifacts.
- Add retention/cleanup guidance for tool receipt folders, run artifacts, project-structure assets, and output folders.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB04` are updated and the next dependent workstream can rely on it.
