# Codex task — PRM-F03

Implement **Actor roles, responsibilities, and CRM-HR bindings** inside the uploaded CanDoItAll solution.

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

- A process step can reference responsible, consulted, approver, and observer roles.
- Roles can bind to CRM-HR parties and AI-agent profiles without duplicating durable identity.
- Actor rebinding preserves auditability of earlier runs.
- Future AI execution metadata can be attached without introducing a runtime dependency on AgentFramework.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessActorModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorServices.cs (new)`
- `src/CanDoItAll.Modules.CrmHr/CrmHrModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrAgentsPage.razor`
- `tests/CanDoItAll.Tests.Integration/ProcessesCrmHrIntegrationTests.cs (new)`
