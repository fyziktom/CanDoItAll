# Target Solution

## Target Boundary

- Keep all production code in existing process module boundaries.
- Isolate pure rule and read-model candidates without moving them to a new Core project.
- Confine source-payload recovery, finalizer application, EF readback, filesystem/storage access, claims, transitions, AgentFramework execution, and provider behavior to application-local services.

## Explicit Non-Targets

- No `src/CanDoItAll.Processes.Core`.
- No `src/CanDoItAll.Modules.Processes.Core`.
- No `IProcessDriverPack`, `IProcessDriverRegistry`, runtime driver registry, driver DI registration, manager tool, or production helper-driver API.
- No UI or browser-visible changes.

## Completion Target

- The final architecture decision must state either `Ready for narrow Process Core proposal next` or `Defer Core and list exact blockers`.
- Driver readiness must remain documentation/test-scan only unless a later bundle explicitly introduces production APIs.
