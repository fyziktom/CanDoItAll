# PRM-F16 — Role and agent templates, staffing briefs, and sourcing handoffs

## Objective

Let managers define reusable human/AI/hybrid role templates in CRM-HR, let process designers reference them instead of ad-hoc free-text roles, and let HR fulfill gaps through staffing, recruiting, or agent-sourcing workflows with durable snapshots.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Wave 1**
- Depends on: **PRM-F03**

## Why this feature exists

The senior review found that actor binding alone was not enough.  
A serious process-management system also needs a **template layer** between process roles and concrete assignees:

- manager defines governed archetype,
- process designer references it,
- HR fulfills it,
- runtime resolves it.

## In scope

- Manager-owned reusable role / agent templates in CRM-HR
- Process role selection from template catalog
- Linked staffing briefs and sourcing handoffs
- Eligible pools and fallback metadata
- Template version snapshots on publish/run
- Human / AI parity in template governance

## Non-goals

- Do not create a second staffing or recruiting subsystem inside Processes.
- Do not add direct AgentFramework runtime dependency.
- Do not let template edits rewrite old process history.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrRecruitingServices.cs`
- `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrAssignmentsPage.razor`
- `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrWorkforcePage.razor`
- `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrAgentsPage.razor`
- `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrRoleTemplatesPage.razor (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorTemplateBridge.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorServices.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessDesignerPage.razor`
- `tests/CanDoItAll.Tests.Integration/ProcessesStaffingTemplateIntegrationTests.cs (new)`
