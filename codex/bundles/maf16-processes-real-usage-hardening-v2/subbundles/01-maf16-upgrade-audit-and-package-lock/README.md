# SB01: 01-maf16-upgrade-audit-and-package-lock

## Goal

Audit package versions, lock/restore state, and remaining 1.3 references.

## Required work

- Confirm actual package versions in csproj/assets/lock files.
- Record whether A2A package is preview and why.
- Add or update a package matrix doc.
- Fail if any active src/test project references MAF 1.3.
- Run restore/build proof.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB01` are updated and downstream subbundles can rely on the behavior.
