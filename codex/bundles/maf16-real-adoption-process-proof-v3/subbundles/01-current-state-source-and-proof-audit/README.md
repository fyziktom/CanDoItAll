# SB01: 01-current-state-source-and-proof-audit

## Goal

Verify current head, claimed proof, and actual source state.

## Required work

- Open current head, previous bundle execution report, package files, MAF adapter files, process artifact files, and live-run profile files.
- Create a short audit table: claim, source proof, test proof, confidence, remaining risk.
- Fail this subbundle if any previous proof references a file/path that does not exist.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB01` are updated and downstream subbundles can rely on it.
