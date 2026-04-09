# PRM-F03 — Actor roles, responsibilities, and CRM-HR bindings

## Objective

Bind process actors to CRM-HR parties and AI-agent profiles, model role responsibilities, and prevent creation of a second durable actor registry inside the process module.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Wave 1**
- Depends on: **PRM-F01, PRM-F02**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- A process step can reference responsible, consulted, approver, and observer roles.
- Roles can bind to CRM-HR parties and AI-agent profiles without duplicating durable identity.
- Actor rebinding preserves auditability of earlier runs.
- Future AI execution metadata can be attached without introducing a runtime dependency on AgentFramework.

## Important boundary

This feature establishes **binding and identity boundaries**.

It intentionally does **not** own the reusable role / agent template catalog.  
That extension is handled by **PRM-F16**, where CRM-HR becomes the owner of reusable staffing archetypes and Processes stores references/snapshots only.

## Non-goals

- Do not create a second durable agent directory in the process module.
- Do not make process actors depend on the AgentFramework repo.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessActorModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorServices.cs (new or initial)`
- `src/CanDoItAll.Modules.CrmHr/CrmHrModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrAgentsPage.razor`
- `tests/CanDoItAll.Tests.Integration/ProcessesCrmHrIntegrationTests.cs (new)`
