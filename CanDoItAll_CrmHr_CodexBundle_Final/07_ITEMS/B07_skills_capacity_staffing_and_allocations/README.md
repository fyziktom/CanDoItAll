# B07 — Skills, capacity, staffing requests, bench management, and allocations

## Status

- `Completed on 2026-04-03`

## Purpose

Implement skill catalog handling, proficiency, certifications, availability blocks, staffing requests, project allocations, bench views, and demand-versus-capacity reporting.

## Dependencies

B01, B02, B03, B06, B10

## Main stories covered

- **HR-07** As a capability lead, I can maintain a person’s skills so staffing and delivery planning can search by competence.
- **HR-08** As a capability lead, I can record skill proficiency so staffing decisions are not binary.
- **HR-09** As a capability lead, I can record certifications and important qualifications so regulated or specialized work can find compliant people.
- **HR-10** As a resource manager, I can record capacity and default weekly availability so bench and load views are grounded.
- **HR-11** As a resource manager, I can block leave, partial availability, and unavailability windows so plans reflect real capacity.
- **HR-12** As a resource manager, I can see who is on the bench or nearing availability so I can staff new work.
- **HR-13** As a project manager, I can request staffing from HR with desired role, skills, dates, and allocation so demand is structured.
- **HR-14** As a resource manager, I can allocate a person or delivery unit to a project with a percentage and dates so staffing commitments are explicit.
- **HR-15** As a delivery manager, I can see current and future allocations for a person, contractor, or delivery unit so overloads are visible.
- **HR-18** As a delivery lead, I can search workforce by skill, location, seniority, and availability so I can assemble delivery teams faster.
- **HR-31** As a resource manager, I can assign a company or unit instead of a named person to early staffing placeholders so rough planning can start before individuals are known.
- **HR-32** As a delivery director, I can see demand versus available capacity by team or delivery unit so staffing risks surface early.
- **HR-33** As a people ops analyst, I can search for expiring assignments, onboarding items, and contract end dates so HR follow-up becomes proactive.
- **HR-35** As a project manager, I can view allocated people and units per project from the HR side so staffing ownership is bidirectional.
- **PRJ-14** As a resource manager, I can have project allocations automatically influence HR capacity views so assignments have real staffing impact.

## Main routes

- `/crm-hr/assignments`
- `/crm-hr/workforce`

## Done when

- Skills and proficiency are searchable from workforce/assignment pages.
- Staffing requests can be created with role, skills, dates, and allocation.
- Allocations affect capacity views and conflict callouts appear.
- Project-linked allocations are visible from both HR and project context.

## Execution Notes

- `/crm-hr/workforce` now owns skill-matrix and capacity-block editing for people and delivery units, including proficiency-aware search terms, bench and overload summary cards, and project-linked conflict visibility.
- `/crm-hr/assignments` now owns staffing-request entry, demand summary, candidate search, and allocation editing, with project allocations persisted through the shared B10 `ProjectPartyAssignment` path instead of a parallel CRM-HR-only store.
- Closure exposed provider-specific ordering limits under SQLite, so staffing, capacity, and project-allocation ordering now happens after materialization while preserving the intended visible sort order.

## Proof Captured

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "CrmHrWorkforcePageTests|AssignmentsPageTests" -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "StaffingAllocationIntegrationTests|ProjectPartyAssignmentIntegrationTests" -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter StaffingFlowTests -v minimal`
- Browser artifacts: `C:\repositories\CanDoItAll\evidence\crm-hr\b07\crm-hr-assignments-b07-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b07\crm-hr-assignments-b07-tablet.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b07\crm-hr-workforce-b07-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b07\crm-hr-workforce-b07-tablet.png`
