# Target Architecture

## Stable process runtime

The target is not "driver runtime first". The target is:

```text
UI / API / Project Structure / Scheduler / Workflow origin
        |
        v
ProcessesService launch APIs
        |
        v
Process run + step run persistence
        |
        v
ProcessRunAutomationDispatchService + outbox/claim/route/finalizer
        |
        v
MAF workflow-backed role OR direct-agent execution
        |
        v
Artifact projection + validation + manager diagnostics
        |
        v
Read-only driver verification diagnostics (optional support, no mutation)
```

## Driver runtime decision

### Needed now
- Normal DI registration for modules/services.
- Scheduler process target launch.
- Workflow-origin process start.
- Process manager/directive surfaces.
- Agent runtime tools used by process execution.

### Not needed now
- Generic driver host/registry/selector.
- Driver manager command.
- Driver scheduler/workflow hook.
- Execution-capable driver lane.

### Future-gated
Runtime driver host can be considered only after the current process runtime is proven stable by UI/service/live-provider E2E tests. Future gate must define lifecycle owner, security, audit persistence, sandbox/allow-lists, explicit approval, emergency stop, and failure semantics.

## Generic Process Core

Process Core must stay deterministic and generic:
- no `.NET`, Office, business-analysis, MAF, EF, workspace/storage, UI, driver package, scheduler, or workflow runtime dependencies;
- only pure read models/rules/descriptors;
- domain-specific evidence verification remains in driver packages and process-module adapters.
