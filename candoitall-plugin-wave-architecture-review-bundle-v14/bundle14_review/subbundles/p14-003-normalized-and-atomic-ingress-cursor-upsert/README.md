# P14-003 — normalized and atomic ingress cursor upsert

## Status

Completed.

## Prerequisites

- phase14 prepared-stage validator passes

## Dependency impact

- Foundation for future plugin pollers and for ingress materialization retry safety

## Validation depth

- targeted integration tests for normalization and concurrent first-write convergence

## Progression gate

- do not continue while cursor methods still depend on raw keys or read-then-insert first-write races

## Closure proof

- `src\CanDoItAll.Modules.Automation\AutomationIngressService.cs` now normalizes required cursor keys for reads and writes and recovers from concurrent first-write uniqueness conflicts.
- `Plugin_ingress_cursor_save_trims_keys_before_lookup` passed.
- `Concurrent_first_cursor_save_reuses_the_same_cursor_row` passed.
