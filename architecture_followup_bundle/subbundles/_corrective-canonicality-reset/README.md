# Corrective playbook — canonicality reset

Invoke this when core dependency meaning is still ambiguous.

## Trigger examples

- legacy scalar dependency mirrors still exist on core entity/editor/runtime types;
- runtime or UI still relies on a single-primary-dependency shortcut;
- compatibility logic still mutates core models.

## Mandatory repair moves

- remove or quarantine the mirror fields;
- move old-format compatibility to boundary DTOs/adapters;
- rerun canonical round-trip, import compatibility, and runtime/component proof before reopening Gate A.
