# Codex task — PRM-F22

Implement **Process-native work briefs, baton handoffs, and governed triage routing** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner of work-brief, baton, and routing semantics.
- Do not hide baton contents or triage decisions only inside runtime prompt text.
- Do not add a compile-time dependency on the uploaded AgentFramework repo.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- Each executable step can materialize a normalized work brief from process, step, template, customer, and governance context.
- Baton handoffs are persisted as first-class runtime artifacts with source role, target role, brief snapshot, and completion context.
- Triage or dispatcher behavior is modeled as a process role, step, or governed routing decision record rather than hidden out-of-band agent topology.
- Direct production agent-to-agent wiring outside the process requires an explicit override path with journal evidence.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessWorkBriefModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessContextReferenceModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessWorkBriefService.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessTriageService.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeServices.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTransitionServices.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessRunPage.razor (new)`
- `tests/CanDoItAll.Tests.Integration/ProcessWorkBriefIntegrationTests.cs (new)`
- `tests/CanDoItAll.Tests.Integration/ProcessTriageRoutingIntegrationTests.cs (new)`
- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
