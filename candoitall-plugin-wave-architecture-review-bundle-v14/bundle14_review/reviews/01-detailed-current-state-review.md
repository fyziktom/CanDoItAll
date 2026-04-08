# Bundle14 detailed review

## Verdict

The current repository is **fully OK for bundle14 scope**.

The earlier gate closures remain intact:
- phase10 hard gate passes,
- phase13 hard gate passes,
- the runtime plane, Quartz bridge, internal messages, hosted workers, ingress inbox, and telemetry scaffolding remain present.

The hidden runtime-semantic defects that originally opened bundle14 are now closed. The repo now has explicit proof for restart safety, duplicate-side-effect prevention, and correctness under concurrency on the automation and connector execution surfaces.

## What is already in good shape

- Workbench read path remains zero-write.
- Explicit projection repair boundary is still present.
- Unknown-manifest shared editor proof remains closed.
- Automation runtime options bind from production configuration.
- Durable internal message plane, trigger registry, hosted workers, ingress inbox, and execution telemetry are present.
- The legacy background-job queue no longer has production call sites that schedule new work directly.

## Hidden defects that were closed

### 1. Once-like triggers now retire after fire

**Evidence:** `src/CanDoItAll.Modules.Automation/AutomationTriggering.cs`
- `AutomationTriggerQuartzJob.Execute(...)` now disables once-like trigger kinds after a successful fire and clears their next planned fire time.
- `QuartzAutomationSchedulerBridge.SynchronizeTriggerAsync(...)` now treats already-consumed once-like triggers as retired and skips projecting them again after restart.

**Proof:** `One_shot_trigger_is_not_rehydrated_after_it_has_already_fired` and `Once_like_trigger_is_retired_after_first_fire`

### 2. Trigger save now returns the canonical post-projection snapshot

**Evidence:** `src/CanDoItAll.Modules.Automation/AutomationTriggering.cs`
- `AutomationTriggerRegistry.SaveAsync(...)` now reloads the canonical trigger row after `schedulerBridge.SynchronizeAsync(...)` and maps the reloaded record instead of the pre-projection tracked entity.

**Proof:** `Trigger_registry_save_returns_reloaded_next_fire_time_after_quartz_projection`

### 3. Ingress cursor reads and writes are now normalized and atomic enough

**Evidence:** `src/CanDoItAll.Modules.Automation/AutomationIngressService.cs`
- `GetCursorAsync(...)` and `SaveCursorAsync(...)` now normalize required key values consistently before lookup.
- `SaveCursorAsync(...)` now converges on the durable row when concurrent first writes hit uniqueness conflicts.

**Proof:** `Plugin_ingress_cursor_save_trims_keys_before_lookup` and `Concurrent_first_cursor_save_reuses_the_same_cursor_row`

### 4. Ingress materialization now has a persisted single-executor claim boundary

**Evidence:** `src/CanDoItAll.Modules.Automation/AutomationIngressService.cs`
- `MaterializeAsync(...)` now resolves the current snapshot, claims a persisted `Materializing` state before plugin code runs, and makes concurrent callers wait for the durable outcome instead of re-running the plugin materializer.
- `src\CanDoItAll.Modules.Automation\AutomationRuntimeModels.cs` adds the explicit `Materializing` state so the claim boundary is representable in storage and code.

**Proof:** `Concurrent_materialize_calls_only_run_the_materializer_once` and `Already_materialized_envelope_returns_existing_snapshot_without_reinvoking_plugin_code`

### 5. Direct connector processing is now lease-bound

**Evidence:** `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs`
- `ConnectorOutboxService.ProcessAsync(Guid commandId, ...)` now delegates into a claim-first direct execution path that uses the same durable lease boundary as worker-driven processing.

**Proof:** `Direct_process_async_claims_a_lease_before_execution` and `Concurrent_direct_process_calls_do_not_execute_the_same_command_twice`

## Advisory-only follow-ups

These are real concerns, but I am keeping them advisory in bundle14 rather than turning them into hard blockers immediately:
- cancellation should be propagated explicitly instead of being folded into broad `catch (Exception)` branches around handler execution,
- telemetry publishing is still tightly interleaved with runtime state transitions, which may deserve a dedicated outbox/best-effort boundary later.

## Overall conclusion

Codex finished the whole bundle14 job.

The repository is execution-grade for the bundle14 plugin-wave scope because the restart and concurrency semantics above are now implemented, tested, and enforced by the carry-forward and phase14 gates.
