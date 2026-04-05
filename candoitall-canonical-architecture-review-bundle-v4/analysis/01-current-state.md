# Current State

- `v3` repaired the active split-truth bug by making the structure-page party editor read from canonical assignments and by reconciling assignment rows on delete and subtree transfer.
- Remaining risk is concentrated in three seams:
  - Workbench saves structure mutations before CRM/HR reconciliation, so failure handling is still weak.
  - Workbench metadata still stores participant/work-item party ids and meeting linked-party objects even though those are no longer canonical.
  - The Workbench-to-Projects-to-CRM/HR node-scoped bridge still passes raw node-key strings.
- The universal Workbench node model is still broad, but that is an architecture-governance problem first, not an immediate data-corruption bug.
