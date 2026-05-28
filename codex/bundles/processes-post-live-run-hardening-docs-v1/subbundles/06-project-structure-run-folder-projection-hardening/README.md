# SB06: 06-project-structure-run-folder-projection-hardening

## Goal

Harden project-structure process run folder projection.

## Required work

- Define explicit run-folder projection policy: run artifact root, generated product root, external output root, ignored tool receipt internals, multiple workspace roots.
- Extract folder projection helper if needed.
- Add tests for date-based receipt paths, multiple generated product folders, external final delivery folders, and no per-artifact child noise.
- Document projection behavior.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB06` are updated and the next dependent workstream can rely on it.
