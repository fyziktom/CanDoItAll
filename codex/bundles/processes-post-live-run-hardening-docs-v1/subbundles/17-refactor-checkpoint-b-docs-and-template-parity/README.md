# SB17: 17-refactor-checkpoint-b-docs-and-template-parity

## Goal

Docs/template parity checkpoint.

## Required work

- Compare docs/skills/templates/API examples against source enums and DTOs.
- Run template pack validator.
- Run docs source assertions.
- Ensure examples use live-run profiles, not seeded baseline artifacts, for live tests.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB17` are updated and the next dependent workstream can rely on it.
