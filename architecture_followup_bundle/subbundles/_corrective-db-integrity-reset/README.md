# Corrective playbook — DB integrity reset

Invoke this when FK or uniqueness hardening is incomplete or had to be rolled back.

## Trigger examples

- representative orphan rows are still insertable;
- duplicate unconditional dependencies still slip through;
- FK work was abandoned because the current save order temporarily dislikes the tighter model.

## Mandatory repair moves

- redesign ownership and save ordering rather than weakening integrity;
- document delete behaviors explicitly;
- rerun schema/invariant proof and migration scripts before reopening Gate B.
