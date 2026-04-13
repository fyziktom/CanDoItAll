# Corrective playbook — persistence and concurrency reset

Use this when gate B or equivalent proof shows the mutation core is still unsafe.

## Typical triggers

- Save/publish/transition are still not fully transaction-safe.
- Concurrency tokens are missing, incomplete, or provider-specific.
- Stable child identity is not preserved.
- Rollback or conflict translation proof is weak.

## Mandatory correction scope

- `ProcessesService.Persistence.cs`
- `ProcessesService.Publication.cs`
- `ProcessesService.Runtime.cs`
- `ProcessDefinitionModels.cs`
- `ProcessRuntimeModels.cs`
- `ProcessRuntimeEntityConfigurations.cs`
- both provider snapshots or migrations
- related integration tests
- execution/reporting docs

## Validation rerun minimum

- solution build
- focused integration tests for save/publish/transition behavior
- any migration-related proof
- rerun gate B

## Unblock condition

Gate B passes with explicit conflict, transaction, and stable-identity proof.
