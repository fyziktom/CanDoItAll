# Corrective playbook — runtime and query reset

Use this when gate C or equivalent proof shows the service split is still too concentrated.

## Typical triggers

- Publication and clone responsibilities remain coupled.
- Runtime transition logic is still effectively one hotspot.
- Query splitting is superficial and broad-load assumptions remain.
- The refactor produced renamed monoliths rather than real seams.

## Mandatory correction scope

- `ProcessesService.Publication.cs`
- `ProcessesService.Runtime.cs`
- `ProcessesService.Reads.cs`
- any new extracted publication/runtime/query services
- related integration and MCP tests
- architecture docs and execution logs

## Validation rerun minimum

- focused integration tests for publish/runtime/query behavior
- MCP process tests where contracts changed
- rerun gate C

## Unblock condition

Gate C passes with explicit evidence that publication, runtime, and query responsibilities are materially healthier.
