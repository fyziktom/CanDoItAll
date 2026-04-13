# Corrective playbook — workspace and shared-infrastructure reset

Use this when gate D or equivalent proof shows consolidation or UI decomposition went in the wrong direction.

## Typical triggers

- Shared extraction created a dumping ground.
- Major duplication is still active.
- `ProcessWorkspace` is still effectively monolithic.
- Browser proof is weak or layout regressed.
- Provider snapshots or schema files are not coherent.

## Mandatory correction scope

- touched shared helpers or extraction location
- `ProcessWorkspace*`
- `ProcessCanvasSurfaceFactory*`
- touched schema/configuration files
- component tests, browser proof, build/migration proof
- execution/reporting docs

## Validation rerun minimum

- focused component tests
- refreshed browser proof on `/processes`
- build and migration proof if schema changed
- rerun gate D

## Unblock condition

Gate D passes with explicit evidence that ownership, UI decomposition, and schema hygiene are all acceptable.
