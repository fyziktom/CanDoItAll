# CanDoItAll plugin-wave architecture review bundle v11

## Purpose
Re-check the new repo after the claimed phase10 closure, confirm whether the bundle10 blocker is now genuinely closed, and define the next execution-grade pre-plugin runtime package so the platform can safely absorb a larger wave of plugins.

## Verdict
**GO for phase10 closure.**  
**NO-GO for the larger plugin wave until phase11 closes.**

### Bundle10 is now genuinely closed
The previous critical blocker is fixed:

1. `ProjectStructureAssemblyService.LoadAsync(...)` no longer mutates persisted projection rows during reads (`src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:130-176`).
2. stale projection cleanup moved to an explicit maintenance boundary in `ProjectStructureProjectionMaintenanceService.RepairAsync(...)` (`src/CanDoItAll.Modules.Workbench/ProjectStructureProjectionMaintenanceService.cs:15-68`).
3. zero-write read tests now exist and explicitly prove the hot read path stays read-only even when stale system-managed rows, stale layout rows, and legacy compatibility payloads are present (`tests/CanDoItAll.Tests.Integration/ProjectWorkbenchProjectionMaintenanceIntegrationTests.cs:15-198`).
4. unknown-manifest shared-editor proof now exists across provider and resource connectors without page-specific UI code (`tests/CanDoItAll.Tests.Integration/UnknownConnectorManifestIntegrationTests.cs:18-99`).

### What phase11 must close before the next real plugin wave
- **HG-11-01**: operational messages, wakeups, retries, and pub-sub events must be modeled as an internal execution plane, not as default Workbench nodes.
- **HG-11-02**: a canonical trigger registry must exist, with cron and timezone preserved canonically and projected into Quartz-backed runtime scheduling.
- **HG-11-03**: durable internal messaging must exist for commands, events, and trigger wakeups; plugin-to-plugin orchestration must not depend on in-memory channels.
- **HG-11-04**: hosted workers must automatically drain due triggers, connector outbox commands, and queued background work.
- **HG-11-05**: inbound plugin data sources (email, WhatsApp, webhooks, polling connectors) must first land in a durable ingress inbox with deduplication and explicit node materialization.
- **HG-11-06**: execution policy, observability, retries, dead-lettering, and optional MQTT-based telemetry must be added without making MQTT the canonical internal transport.

## Current repo gaps that justify phase11
1. `IBackgroundJobQueue` is still in-memory-only and its dequeue side has no active consumer (`src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs:15-20,93-101`; DI registration at `src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:98-100`).
2. `ConnectorOutboxService.ProcessPendingAsync(...)` exists but has no active runtime caller, so queued connector commands still depend on manual invocation (`src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs:326-354`).
3. there are no hosted workers, no Quartz integration, and no broker seam in the current repo baseline (`inventories/05-runtime-gap-search-baseline.txt`).
4. `AutomationWorkspaceService` depends on a singular `IAutomationSignalProvider`, which is a last-registration-wins shape and is not open-world enough for a large plugin ecosystem (`src/CanDoItAll.Modules.Automation/AutomationModels.cs:10-24`, `src/CanDoItAll.Modules.Automation/AutomationModuleServiceCollectionExtensions.cs:9-13`, `src/CanDoItAll.Modules.CrmHr/CrmHrModuleServiceCollectionExtensions.cs:9-21`).
5. the current “background job” abstraction is mostly UI telemetry around synchronous inline work; for example `PromptFactoryService` enqueues a tracked job and then immediately runs the actual work inline in the same call path (`src/CanDoItAll.Modules.Factory/PromptFactoryService.cs:688-721,744-771`).

## Important architecture decision for phase11
A message is **not** a node by default.

- A **node** is a durable domain artifact that belongs in the user-visible project graph.
- An **internal message / event / command / wakeup** is an execution-plane envelope used for orchestration, retries, subscriptions, and cross-plugin coordination.
- A handler may later materialize a node from a message when the message produces a real business artifact (for example an imported email summary, a created task, or a delivery QA result), but the operational envelope itself must stay outside the canonical Workbench graph.

## Important scope note
The current repo still keeps read-only compatibility fallbacks from legacy metadata:

- marker fallback in `ProjectStructureAssemblyService.cs:77-82`
- reference fallback in `ProjectNodeBindings.cs:391-395`

Those are no longer a phase10 blocker, but they remain advisory cleanup work and should not be expanded further.
