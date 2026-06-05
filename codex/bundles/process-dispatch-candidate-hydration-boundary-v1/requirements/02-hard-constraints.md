# Hard Constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not create production process-driver APIs, driver packs, driver registries, or driver modules.
- Do not move EF writes, workflow calls, subprocess calls, execution-client calls, finalizer calls, or transition execution into helpers labeled pure.
- Do not remove existing dispatcher wrapper methods until parity tests prove all callers and edge cases are preserved.
- Do not change public process tool names or access/approval policy.
- Do not change route order: database requirement, upstream materialization, stranded recovery, subprocess, start transition, workflow, direct agent execution, finalizer.
- Do not change in-memory guard or durable claim semantics.
- Do not create browser/mobile/small/medium proof artifacts for this runtime/service refactor.
