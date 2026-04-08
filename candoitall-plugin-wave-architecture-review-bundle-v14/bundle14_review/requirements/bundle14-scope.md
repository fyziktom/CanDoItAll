# Bundle14 scope

## P14-001 — retire once-like triggers after first fire

### Objective
Make `AutomationTriggerKind.Once`, `Relative`, and `DueDateProjection` restart-safe.

### Required changes
- After a once-like trigger fires successfully, persist canonical state that prevents it from being projected again on restart.
- `QuartzAutomationSchedulerBridge` must skip consumed once-like triggers.
- The chosen design must be explicit and durable; do not rely on Quartz in-memory state.

### Required proof
- `One_shot_trigger_is_not_rehydrated_after_it_has_already_fired`
- `Once_like_trigger_is_retired_after_first_fire`

## P14-002 — return the reloaded canonical trigger snapshot

### Objective
`AutomationTriggerRegistry.SaveAsync(...)` must return the post-projection canonical state, not the stale tracked entity from the pre-sync DbContext.

### Required changes
- Save the trigger.
- Synchronize Quartz.
- Re-read the trigger from persistence and return that snapshot.

### Required proof
- `Trigger_registry_save_returns_reloaded_next_fire_time_after_quartz_projection`

## P14-003 — normalized and atomic ingress cursor upsert

### Objective
Make cursor lookup/save reliable for future plugin pollers.

### Required changes
- Normalize trimmed `sourceKind` / `sourceKey` before both reads and writes.
- Handle concurrent first-write uniqueness conflicts by re-reading and converging on the existing row.

### Required proof
- `Plugin_ingress_cursor_save_trims_keys_before_lookup`
- `Concurrent_first_cursor_save_reuses_the_same_cursor_row`

## P14-004 — single-executor ingress materialization

### Objective
Ensure explicit materialization can be retried/read safely without duplicate plugin side effects.

### Required changes
- Add a durable claim/CAS/in-progress boundary before plugin materializer code runs.
- Concurrent materialization calls must not run plugin code multiple times for the same envelope.
- Repeated calls after successful materialization should return the existing snapshot without re-running plugin code unless an explicit replay API is added.

### Required proof
- `Concurrent_materialize_calls_only_run_the_materializer_once`
- `Already_materialized_envelope_returns_existing_snapshot_without_reinvoking_plugin_code`

## P14-005 — lease-bound direct connector processing

### Objective
Remove the semantic split between worker-driven and manual/direct connector execution.

### Required changes
- `ConnectorOutboxService.ProcessAsync(Guid ...)` must claim the durable lease boundary before executing the command, or delegate into the same claim-first path used by `ProcessPendingAsync(...)`.
- A pending command must never have a non-leased execution path.

### Required proof
- `Direct_process_async_claims_a_lease_before_execution`
- `Concurrent_direct_process_calls_do_not_execute_the_same_command_twice`

## Execution notes for Codex

- Keep the design canonical and durable-first.
- Do not fix this by weakening tests or by hiding the public API without preserving the intended operator/manual flow.
- Prefer one execution semantic per runtime surface.
- Validate both SQLite and PostgreSQL paths where the code already splits behavior.
