# Target Solution

This compatibility file mirrors `architecture/01-target-architecture.md` for validator tooling.

## Target Shape
- Preserve route rule behavior already seeded in `CanDoItAll.Processes.Core`.
- Add only pure deterministic read models and rules under approved Core namespaces.
- Keep orchestration, persistence, validation writes, projection writes, workspace/storage/filesystem access, AgentFramework execution, claims, finalizers, and process mutation in `CanDoItAll.Modules.Processes`.
- Keep process-helper-driver readiness as documentation/test-only proof with no production API.

## Dependency Direction
- Core may depend on `CanDoItAll.Processes.Contracts`.
- The process module may depend on Core and Contracts.
- Core must not depend on process modules, infrastructure, EF, workspace/storage/filesystem, AgentFramework, UI, or production driver runtime surfaces.
