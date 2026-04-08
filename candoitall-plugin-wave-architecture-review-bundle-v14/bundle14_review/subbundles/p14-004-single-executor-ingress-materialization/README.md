# P14-004 — single-executor ingress materialization

## Status

Completed.

## Prerequisites

- `P14-003` closure proof is trusted

## Dependency impact

- Critical foundation for safe plugin-side materialization and side effects

## Validation depth

- targeted concurrent materialization tests proving single execution and idempotent reread

## Progression gate

- do not continue while plugin materializer code can still run more than once for the same envelope

## Closure proof

- `src\CanDoItAll.Modules.Automation\AutomationIngressService.cs` and `src\CanDoItAll.Modules.Automation\AutomationRuntimeModels.cs` now establish a persisted `Materializing` claim boundary before plugin code runs.
- `Concurrent_materialize_calls_only_run_the_materializer_once` passed.
- `Already_materialized_envelope_returns_existing_snapshot_without_reinvoking_plugin_code` passed.
