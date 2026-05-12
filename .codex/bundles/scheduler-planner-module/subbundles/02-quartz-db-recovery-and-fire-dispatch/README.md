# 02-quartz-db-recovery-and-fire-dispatch

## Status

- `Completed`

## Objective

- Make the existing Quartz triggering path database-recoverable and connect SchedulerPlanner schedules to Automation trigger projection and durable fire handling.

## Success Criteria

- Quartz is configured with a DB-backed persistent store for supported runtime database profiles.
- Quartz recovery tables are created/migrated through existing database bootstrap conventions.
- SchedulerPlanner schedule changes create/update/delete the corresponding `AutomationTriggerRecord`.
- Scheduler fire requests create durable `SchedulerPlanRun` history with deterministic dedupe.
- Restart/recovery tests prove Quartz persisted triggers survive process restart.

## Covered Inputs

- SPM-R002
- SPM-R003
- SPM-R009, durable fire-history side
- SPM-R010, Automation envelope correlation side
- SPM-R011
- SPM-R012, Automation/fire logging side
- SPM-R013
- SPM-R016, Quartz package/store decision

## Prerequisites

- `01-scheduler-domain-and-persistence` complete.
- SchedulerPlan, SchedulerPlanRun, trigger key generation, and CRON description contracts exist.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Services\AutomationModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Triggers\AutomationTriggering.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Services\AutomationMessagingServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Runtime\AutomationRuntimeContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContext.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\CanDoItAllDatabaseMigrationBootstrap.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AutomationRuntimeIntegrationTests.cs`

## Deliverables

- Quartz persistent-store configuration using provider-specific ADO.NET delegate/table setup for supported profiles.
- Quartz schema migration/bootstrap path for SQLite and PostgreSQL or an explicit fail-fast unsupported-provider path.
- SchedulerPlanner to `IAutomationTriggerRegistry` projection logic.
- Automation message handler for SchedulerPlanner trigger fires that creates or updates `SchedulerPlanRun` records and dispatches to the target-launch contract with test fakes.
- Deterministic dedupe/correlation keys for repeated fire requests.
- Integration tests for schedule projection, restart recovery, fire handling, dedupe, retry/dead-letter visibility, and existing Automation runtime regressions.

## Dependency Impact

- Subbundle 03 depends on fire history and launcher contracts from this phase.
- Subbundle 04 depends on active schedule next/last fire state and history status behavior.
- Final closure cannot pass if Quartz uses RAMJobStore or only application-level trigger rehydration.

## Validation Depth

- `Critical infrastructure foundation`

## Implementation Steps

1. Inspect runtime database profile/provider abstractions and existing migration/bootstrap conventions.
2. Add Quartz persistent-store packages only when required by the selected serializer/provider approach.
3. Configure Quartz with ADO.NET persistent store, table prefix, string properties, serializer, and provider-specific delegate.
4. Add or integrate Quartz table creation/migration for supported profiles.
5. Update Automation/SchedulerPlanner service wiring so schedule changes project into `Automation_Triggers`.
6. Add SchedulerPlanner Automation fire handler with explicit owner/key filtering.
7. Record `SchedulerPlanRun` status transitions for received, duplicate, dispatching, dispatched, failed, and dead-letter-linked cases.
8. Add restart/recovery tests that fail if Quartz falls back to in-memory store.
9. Re-run existing Automation integration tests.
10. Update `reviews/01-execution-report.md` with store configuration, provider support, and proof.

## Scope Exceptions

- Real process/workflow launch implementations belong to subbundle 03. This subbundle may use fake launchers in integration tests.
- UI is out of scope.

## Do Not Do

- Do not replace Quartz.
- Do not rely only on `Automation_Triggers` rehydration as proof of Quartz DB recovery.
- Do not hide unsupported database providers behind no-op fallback behavior.
- Do not serialize complex object graphs into Quartz job data if string properties can represent the needed keys.

## Acceptance Checklist

- Quartz persistent store is configured for supported runtime database profiles.
- Quartz tables are present after bootstrap/migration.
- SchedulerPlanner schedule create/update/disable/delete projects correctly to Automation trigger records.
- A fired Automation trigger produces one schedule run history row per dedupe key.
- Duplicate fire requests are idempotent and visible.
- Existing Automation integration tests still pass.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Targeted integration tests for Quartz DB persistence/recovery.
- Targeted integration tests for schedule-to-Automation projection.
- Targeted integration tests for fire handler dedupe and history.
- Existing `AutomationRuntimeIntegrationTests` pass.

## Browser Validation Logging

- N/A. This subbundle does not add browser-visible UI.

## Progression Gate

- Downstream launch adapters and UI may continue only after Quartz DB persistence/recovery is proven and schedule fires are durable, deduped, and queryable.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Configure Quartz DB-backed recovery and connect SchedulerPlanner schedules to Automation trigger projection and durable fire handling. Use fake launchers in tests where needed; do not implement real process/workflow adapters or UI yet. Capture restart/recovery proof and update the execution report.
```
