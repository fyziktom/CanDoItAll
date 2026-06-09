# Current State Review

## What was verified from real source before preparing this bundle

The latest branch shows real progress: application startup smoke tests exist, process template catalog smoke exists, UI large-screen process-start proof is reported, process run creation/persistence and deterministic process scenarios are reported, and full unit proof is reported as clean in the latest bundle.

The current source also shows these stable facts:

- `ApplicationStartupIntegrationTests` starts the current web composition, hits `/health`, verifies process templates over `/api/processes/templates`, and asserts `ProcessesService`, `ProcessTemplateCatalogService`, and `IProcessRunAutomationDispatchService` are registered.
- `src/CanDoItAll.Modules.Processes/README.md` now documents supported process launch surfaces: `/processes`, `/projects/{projectId}/processes`, `/processes/live`, `/projects/{projectId}/processes/live`, `/api/processes` launch APIs, project-structure launch API, and `ProcessesService.StartRunFromTriggerAsync`.
- `ProcessesService.StartRunFromTriggerAsync` delegates scheduler/workflow/manual trigger starts into normal `StartRunAsync` after validating trigger source and requester metadata.
- `SchedulerPlannerService` already depends on `ProcessesService` and has a scheduler target launch surface; scheduler process launch should remain service-centered, not driver-runtime-centered.
- `ProcessDriverVerificationGateway` is now explicit and typed for the current read-only lanes.
- `ProcessReadOnlyVerificationBatchOrchestrator` composes read-only verification lanes and aggregates diagnostic responses without process mutation.

## Important correction to previous framing

The terms previously listed as "forbidden" are not all permanently forbidden. They fall into three categories:

### Needed now, but not through a driver runtime
- DI registration for ordinary application/process services.
- Scheduler-triggered process starts.
- Workflow-origin process starts.
- Manager-facing process controls and manager chat/directives.
- Agent runtime tool providers for process execution.

These are part of the normal process runtime and should be tested/restored as first-class process features.

### Not needed for current "processes work like before"
- A generic process-driver runtime host.
- A driver registry.
- A driver selector/fallback runtime.
- Driver DI auto-registration.
- Driver manager commands.

The current process runtime can run via `ProcessesService`, dispatch services, MAF/workflow/direct-agent execution, outbox workers, and existing scheduler/workflow start paths without a generic driver runtime.

### Future-gated
- Execution-capable process drivers.
- Shell/package/Graph/CRM/workspace/storage mutation through drivers.
- A generic driver runtime host with lifecycle, authorization, auditing, sandboxing, allow-lists, and observability.

Those may become useful later, but enabling them now would blur ownership and likely break the generic Process Core boundary.

## What is still not fully proven

- A real OpenAI-backed end-to-end process run, with credits/API key provided through environment or secure config, has not been proven by the existing source-backed tests.
- UI proof appears to focus on the Processes route; project-structure node start path and run output navigation need stronger large-screen proof.
- Deterministic `.NET` create/modify and business-analysis scenarios are useful, but they do not replace a live provider smoke with strict budget and timeout controls.
- Background worker mode and outbox drain behavior need an explicit local-runtime lane smoke: no orphaned claim, no stuck step, no hidden worker disabled state.
- Long-lived tests must be audited again to remove all transient bundle-path coupling, not only the known architecture class.
