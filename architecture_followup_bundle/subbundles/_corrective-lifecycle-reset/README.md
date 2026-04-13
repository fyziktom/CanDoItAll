# Corrective playbook — lifecycle reset

Invoke this when lifecycle singularity or allocator safety still depends on ordering logic.

## Trigger examples

- more than one draft/published row can still exist per definition;
- `ActivePublishedVersionId` is still weakly protected;
- `MAX + 1` version allocation remains.

## Mandatory repair moves

- move lifecycle assumptions into DB-backed invariants;
- replace weak allocators;
- rerun publish/save/start-run proof before reopening Gate C.
