# Target Architecture

## Layers

1. **Process Core**: pure deterministic rules/read models only. No drivers, EF, modules, UI, AgentFramework, OpenAI, storage, workspace, or process runtime orchestration.
2. **Process Module Runtime**: owns definitions, templates, run lifecycle, outbox, dispatch, finalizer, artifacts, recovery, manager diagnostics, API/UI, scheduler/workflow-origin starts.
3. **Verification Runtime Host**: process-module-owned read-only host. It selects explicit lanes, applies options and emergency disable, records durable audit, returns structured success/denial, and never mutates process state.
4. **Domain Verification Drivers**: packages over supplied evidence only. They do not self-register, discover, execute commands, call external systems, or write state.
5. **Future Execution-Capable Driver Runtime**: not approved in this bundle. It must pass a separate source-backed approval bundle.

## Current Allowed Host
The current allowed host is verification-only and process-module-owned. It may inspect supplied facts and return diagnostics, audit records, redaction descriptors, evidence references, and operator readbacks.

## Future Host Readiness Work In This Bundle
This bundle may add governance contracts, health/status, audit durability, scheduler/workflow read-only job execution, manager UI/API readback, dry-run sandbox contracts, and future-gate documentation/tests. It must not execute domain actions.