# Detailed current-state review

## Overall conclusion

Codex is **partly right**: the repo now genuinely closes the earlier bundle10/bundle11/bundle12 hard failures, and the shipped gate scripts for phase10/phase11/phase12 all pass.

However, after a manual architecture review of the live code, the baseline is **still not ready to be called execution-grade for the plugin wave**. The remaining problems are not the old Workbench regressions; they are hidden runtime-hardening gaps around configuration, idempotency under concurrency, worker acquisition, and legacy execution seams.

## What is now genuinely fixed

- `ProjectStructureAssemblyService.LoadAsync(...)` is zero-write again and no longer deletes stale projection rows on read.
- `ProjectStructureProjectionMaintenanceService.RepairAsync(...)` exists as an explicit repair boundary.
- Unknown connector manifest round-trip coverage and shared editor proof are back.
- The automation module now contains a durable runtime plane: envelopes, deliveries, dead letters, triggers, ingress envelopes, execution logs, hosted workers, and an optional MQTT bridge.

## What is still not okay

### 1) Automation runtime options are not bound from production configuration

Evidence:
- `src/CanDoItAll.Modules.Automation/AutomationModuleServiceCollectionExtensions.cs:10-39`
- `src/CanDoItAll.Web/Program.cs:50-61`
- repo-wide search: only tests configure `AutomationRuntimeOptions`

The module does `services.AddOptions<AutomationRuntimeOptions>();` but does not bind a config section in production code. That means poll intervals, MQTT host/port/client id, and similar runtime tuning are effectively frozen to defaults unless somebody edits DI code.

This is a plugin-wave blocker because the execution plane has already introduced behavior that operators will need to tune by environment.

### 2) Durable idempotency is still read-then-insert, not atomic

Evidence:
- `src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs:49-102`
- `src/CanDoItAll.Modules.Automation/AutomationIngressService.cs:19-49`
- `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs:230-311`

All three paths do the same pattern:

1. query for an existing row,
2. if missing, insert a new row,
3. save changes,
4. do not recover from a uniqueness conflict.

That works in sequential tests, but it is not concurrency-safe for parallel workers or future multi-instance deployments. The repo already signals a future direction toward splitting runtime responsibilities; this pattern will eventually produce race-driven `DbUpdateException` failures or duplicate attempt behavior.

### 3) Runtime acquisition is still single-instance and not claim/lease based

Evidence:
- `src/CanDoItAll.Modules.Automation/AutomationRuntimeModels.cs:115-158` defines `LockToken` / `LockedAtUtc`
- `src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs:143-167` still loads deliveries into memory and filters after `ToListAsync`
- `src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs:190-198` writes lock fields, but there is no atomic claim/update step before selection
- `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs:335-357` still loads pending commands into memory and filters after `ToListAsync`

The system has lock fields, but the dispatcher does not actually use them as a lease/claim protocol. This is still a single-instance assumption hidden behind durable tables. It also means the hot paths do full-table or broad-table materialization instead of using indexed due-work acquisition at the database boundary.

### 4) Hosted workers have no iteration-level exception isolation

Evidence:
- `src/CanDoItAll.Modules.Automation/AutomationHostedServices.cs:25-97`

Each worker loop calls its runtime service directly without a `try/catch` around one iteration. If an unexpected exception escapes one dispatch/process iteration, the loop exits and that worker instance stops draining work. This is not execution-grade behavior for the plugin wave.

### 5) The legacy background-job queue seam is still live in production code

Evidence:
- `src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:98-100`
- `src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs:15-20, 95-104, 117-143`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs:688-745`
- `src/CanDoItAll.Modules.Automation/AutomationHostedServices.cs:69-97`

The bridge worker currently only **observes/logs** legacy queue items. It does not convert them into durable automation work or close the seam. At the same time, `PromptFactoryService` still uses `IBackgroundJobTracker.EnqueueTrackedAsync(...)`, which means production code still advertises and consumes the old queue-based path.

That is dangerous before plugins because plugin authors or future contributors can still attach new work to a non-canonical seam.

## Non-blocking warnings still present

- marker compatibility fallback from metadata is still active in `ProjectStructureAssemblyService`
- reference compatibility fallback from metadata is still active in `ProjectNodeBindings`
- `CrmHrServices.cs` and `ProjectWorkbenchModels.cs` remain oversized hotspots

These are not phase13 blockers, but they should remain on the backlog.
