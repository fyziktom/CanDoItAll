# Target architecture

## Layers
1. **Process Core**: deterministic pure rules/read models only. No EF, UI, scheduler, workflow, AgentFramework, OpenAI, templates, driver, or runtime-host concepts.
2. **Process Module Runtime**: owns templates, projection, launch plans, process run lifecycle, outbox, automation dispatch, finalizers, artifacts, recovery, manager/operator readback, project/project-structure bridge, scheduler/workflow-origin starts.
3. **AgentFramework / Process-mock runtime**: execution boundary for deterministic process automation tests and live provider execution.
4. **Verification runtime host**: read-only and dry-run-only diagnostics. It may inspect supplied evidence and produce diagnostics/audit/readback. It must not mutate process state.
5. **Future execution-capable host**: still not approved. Requires separate approval gate.

## Current target for this bundle
Restore merge-grade confidence for representative process template execution while keeping the runtime-host path read-only/dry-run-only.
