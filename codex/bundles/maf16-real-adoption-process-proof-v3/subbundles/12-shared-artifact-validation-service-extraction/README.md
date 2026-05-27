# SB12: 12-shared-artifact-validation-service-extraction

## Goal

Extract/reuse one artifact validation service across runtime surfaces.

## Required work

- Move validation logic out of dispatch partials if practical.
- Use the same validation service for finalizer, read model, API/manual transition, recovery, and health diagnostics.
- Prevent duplicate partial implementations and divergent semantics.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB12` are updated and downstream subbundles can rely on it.
