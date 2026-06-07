# Architecture Plan

## Target Shape

This bundle expands the already-created `CanDoItAll.Processes.Core` seed, but only with pure deterministic read models and rules.

Allowed Core contents in this bundle:
- Route rule preservation and hygiene.
- Subprocess lifecycle status/reason facts.
- Subprocess artifact source mapping descriptors.
- Artifact expectation snapshot/read model.
- Pure artifact expectation matching/satisfaction descriptors.

Forbidden Core contents:
- EF / `DbContext` / query services.
- Workspace, storage, filesystem, path IO, storage placement.
- Claim lifecycle, heartbeat, lease, transition execution.
- AgentFramework execution, provider repair, retry orchestration.
- Finalizer application or process state mutation.
- Production driver APIs, registries, runtime selectors, manager tools, DI registration.
- UI/browser code.

## Dependency Direction

```mermaid
flowchart LR
    Contracts[CanDoItAll.Processes.Contracts]
    Core[CanDoItAll.Processes.Core]
    Module[CanDoItAll.Modules.Processes]

    Core --> Contracts
    Module --> Core
    Module --> Contracts
    Module --> Infrastructure[Infrastructure / EF / Storage / Workspace / AgentFramework]

    Contracts -.x.-> Core
    Core -.x.-> Module
    Core -.x.-> Infrastructure
```

## Module Adapters

Adapters stay module-local:
- Convert EF/module entities into Core read models.
- Preserve existing process module behavior.
- Allow Core to remain pure and testable.

## Driver Readiness

Driver work remains a proposal lane:
- Verification-only descriptors can be documented.
- No production interface, registry, DI registration, manager command, runtime dispatch, or execution-capable helper is allowed.
- A future driver bundle must be separate from the Core expansion bundle.
