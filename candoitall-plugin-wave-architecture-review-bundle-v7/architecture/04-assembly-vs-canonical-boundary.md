# Assembly versus canonical boundary

## Canonical

Canonical Workbench storage owns only real project-authored nodes and their canonical semantic edges.

## Assembled read model

Read-only contributors assemble additional projection nodes and links such as:

- project root / hierarchy overlays
- project phases
- resources
- prompt runs / prompt nodes
- validation runs
- test plans

## Rules

- assembled nodes are never reclassified
- assembled nodes are never valid node-scope targets for canonical assignment unless explicitly promoted
- assembled nodes are not persisted in Workbench canonical tables
- assembled links are not stored as editable hierarchy truth
