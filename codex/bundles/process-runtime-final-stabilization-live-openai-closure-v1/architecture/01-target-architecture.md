# Target Architecture for This Stabilization Bundle

## Process Core
Pure deterministic rules/read models only. No dispatcher extraction in this bundle. No UI, EF, AgentFramework, OpenAI, templates, scheduler, workflow, drivers, runtime-host or domain-specific process families.

## Process Module Runtime
Still owns process definitions, templates, launch plans, run lifecycle, outbox dispatch, finalizer, artifacts, manager/operator readback, scheduler/workflow-origin starts, UI/API integration, and runtime-host diagnostic surfaces.

## Verification/Dry-run Runtime Host
Allowed only as read-only/dry-run diagnostics. It may produce capability/readback/audit/denial information. It may not mutate process state, transitions, claims, finalizers, retries, workspace, storage, Office/Graph, CRM, or execute commands.

## Execution-capable Drivers
Still explicitly deferred. Do not implement. Do not prepare hidden registration paths.
