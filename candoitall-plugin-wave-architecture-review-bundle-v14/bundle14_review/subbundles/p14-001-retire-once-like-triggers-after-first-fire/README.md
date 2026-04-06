# P14-001 — retire once-like triggers after first fire

## Status

Completed.

## Prerequisites

- bundle14 prepared-stage validator passes
- phase10 and phase13 carry-forward gates still pass

## Dependency impact

- Critical foundation for restart-safe automation semantics.

## Validation depth

- targeted integration tests proving first-fire retirement and restart-safe non-rehydration

## Progression gate

- downstream phase14 work may continue only when once-like triggers cannot be projected again after successful fire

## Closure proof

- `src\CanDoItAll.Modules.Automation\AutomationTriggering.cs` now retires once-like triggers durably and skips consumed ones during projection.
- `One_shot_trigger_is_not_rehydrated_after_it_has_already_fired` passed.
- `Once_like_trigger_is_retired_after_first_fire` passed.
