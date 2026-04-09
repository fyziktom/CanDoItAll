# Codex task — PRM-F11

Implement **Activity, Automation, Validation, and TestLab hooks** inside the uploaded CanDoItAll solution.

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

- Runs can emit activity entries and automation signals without tight module coupling.
- Validation and TestLab references can be attached to steps and gates.
- Overdue steps and blocked approvals become visible in automation/operations surfaces.
- The hook design does not require the intelligence lake to exist first.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Activity/*`
- `src/CanDoItAll.SharedKernel/AutomationSignals.cs`
- `src/CanDoItAll.Modules.Automation/*`
- `src/CanDoItAll.Modules.Validation/*`
- `src/CanDoItAll.Modules.TestLab/*`
- `tests/CanDoItAll.Tests.Integration/ProcessCrossModuleHooksIntegrationTests.cs (new)`
