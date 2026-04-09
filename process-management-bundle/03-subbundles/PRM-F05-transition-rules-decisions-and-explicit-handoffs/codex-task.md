# Codex task — PRM-F05

Implement **Transition rules, decisions, and explicit handoffs** inside the uploaded CanDoItAll solution.

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

- Decision paths can carry condition text, default-path markers, and branch priority.
- Handoffs record source actor, target actor, payload summary, and completion reason.
- The engine rejects invalid graphs such as unreachable end states or orphaned transitions.
- Sequential specialized handoffs are first-class even before AgentFramework runtime integration.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessTransitionServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessValidationServices.cs (new)`
- `tests/CanDoItAll.Tests.Unit/ProcessTransitionRulesTests.cs (new)`
- `tests/CanDoItAll.Tests.Integration/ProcessHandoffIntegrationTests.cs (new)`
