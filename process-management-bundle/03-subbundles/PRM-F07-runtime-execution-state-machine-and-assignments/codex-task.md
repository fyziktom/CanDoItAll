# Codex task — PRM-F07

Implement **Runtime execution state machine and assignments** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Do not create a new durable actor registry; use CRM-HR bindings when actors are involved.
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

- A process run can start from a published definition version and keep that version immutable for the run lifetime.
- Only valid state transitions are allowed for runs and steps.
- Conflicting claims and double completions are rejected deterministically.
- Assignment resolution can consider eligible pools, capacity/validation state, and fallback routes before work is claimed or rebound.
- Manual, human-approved, and AI-backed executors all fit the same state machine.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessRunModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessLeaseService.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorServices.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureLeaseService.cs (reference pattern)`
- `tests/CanDoItAll.Tests.Integration/ProcessRuntimeIntegrationTests.cs (new)`
