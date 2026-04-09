# Codex task — PRM-F13

Implement **Future AgentFramework adapter and AI executor seam** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Do not create a new durable agent registry; use CRM-HR bindings when actors are involved.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo in the first process-management implementation.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- The process runtime can distinguish manual, AI, and hybrid executor modes.
- The process module compiles and works without referencing AgentFramework projects.
- A bridge contract exists for future AI execution and handoff orchestration adapters.
- CRM-HR remains the durable owner of AI agent identity and staffing.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/IProcessActorExecutionBridge.cs (new)`
- `src/CanDoItAll.Modules.Processes/NullProcessActorExecutionBridge.cs (new)`
- `src/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Models/AgentModels.cs (reference seam)`
- `src/CanDoItAll.AgentFramework-main/integration-map/*.md (reference seam)`
- `tests/CanDoItAll.Tests.Unit/ProcessActorExecutionBridgeTests.cs (new)`
