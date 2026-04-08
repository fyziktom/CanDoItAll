# P14-005 — lease-bound direct connector processing

## Status

Completed.

## Prerequisites

- phase13 lease-based outbox proof is still trusted

## Dependency impact

- Extends the single-executor connector semantic boundary to manual and operator-driven flows

## Validation depth

- targeted direct-processing lease tests

## Progression gate

- do not close phase14 while pending connector commands still have a non-leased public execution path

## Closure proof

- `src\CanDoItAll.Modules.Workspace\ConnectorOutboxService.cs` now routes direct execution through the same claim-first lease-bound path used by workers.
- `Direct_process_async_claims_a_lease_before_execution` passed.
- `Concurrent_direct_process_calls_do_not_execute_the_same_command_twice` passed.
