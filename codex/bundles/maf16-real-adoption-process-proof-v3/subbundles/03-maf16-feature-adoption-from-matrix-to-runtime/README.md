# SB03: 03-maf16-feature-adoption-from-matrix-to-runtime

## Goal

Ensure each Adopted MAF feature has production runtime use.

## Required work

- For each feature marked Adopted, point to production code and a test.
- For each feature marked Deferred, ensure a reason and safe fallback are documented.
- Do not mark context-provider fallback as IChatMessageInjector adoption unless the injector symbol is actually used.
- Add tests where claims are currently source-only.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB03` are updated and downstream subbundles can rely on it.
