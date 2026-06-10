# Target Architecture

## Current allowed architecture

1. **Process Core**
   - Pure deterministic rules and read models.
   - No drivers, EF, modules, UI, AgentFramework, OpenAI, storage, workspace, or process runtime orchestration.

2. **Process Module Runtime**
   - Owns definitions, templates, run lifecycle, outbox, dispatch, finalizer, artifacts, recovery, manager diagnostics, API/UI, scheduler/workflow-origin starts.

3. **Verification Runtime Host**
   - Process-module-owned read-only host.
   - Selects explicit lanes.
   - Applies options, emergency disable, payload limits.
   - Records durable audit.
   - Returns structured success/denial/readback.
   - Never mutates process state.

4. **Domain Verification Drivers**
   - Verification-only packages over supplied evidence.
   - No self-registration, no reflection discovery, no external calls, no writes.

5. **Future Dry-Run Execution Host**
   - May model effectful requests and produce dry-run plans.
   - Must deny all effects by default.
   - Must not execute commands, package restore, Office/Graph calls, CRM mutation, workspace/storage writes, process mutation, transition/finalizer/claim/retry mutation.

6. **Future Execution-Capable Host**
   - Still not approved.
   - Requires separate source-backed approval bundle.
