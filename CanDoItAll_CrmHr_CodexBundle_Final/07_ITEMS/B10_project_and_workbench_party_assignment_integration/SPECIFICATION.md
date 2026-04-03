# Specification

## Objective

Connect projects and project-structure nodes to the new directory so customer, partner, delivery unit, participant, meeting, work item, and AI-agent assignment flows all use the unified Party model.

## Scope

- Create project-party assignment infrastructure and project summary enrichment.
- Add central party picker flows to Workbench participant, meeting, and work-item editors.
- Allow project-local participant fallback when central registry is intentionally not used.
- Ensure allocations and AI-agent reuse can link through the same assignment layer.

## Services and entities involved

**Services**

- `ProjectPartyIntegrationService`
- `ProjectsService`
- `ProjectWorkbenchService`
- `HrService`
- `AiAgentService`

**Entities / concepts**

- `ProjectPartyAssignment`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

- **PRJ-01** As a project manager, I can assign primary customer, partner, and delivery unit to a project so project context is commercially and operationally complete.
- **PRJ-02** As a project manager, I can assign people, contractors, companies, and AI agents to a project so project staffing is unified.
- **PRJ-03** As a project manager, I can assign responsibility for a project structure delivery node so the workbench shows who is expected to deliver it.
- **PRJ-04** As a project manager, I can choose meeting participants from the unified directory so meetings reference real parties.
- **PRJ-05** As a project manager, I can indicate with whom a meeting happens such as customer, partner, team, or AI agent so the structure reflects real collaboration.
- **PRJ-06** As a project manager, I can assign work items from the unified directory so assignee data is reusable across project and HR views.
- **PRJ-07** As a project manager, I can create a project participant node from an existing party or create a new party from the node flow so local workbench and central registry stay connected.
- **PRJ-08** As a project manager, I can decide whether a participant node is centrally synced or project-local so edge cases do not block work.
- **PRJ-09** As a project manager, I can see related customer, partner, delivery team, and AI agents on project overview screens so context is visible without opening the CRM/HR module first.
- **PRJ-10** As a portfolio manager, I can filter projects by customer, delivery unit, account manager, or key stakeholder so portfolio review is relationship-aware.
- **PRJ-15** As a prompt engineer, I can reuse the same AI agent record in prompt, project, and staffing flows so agent identities stay consistent.
- **PRJ-16** As a meeting facilitator, I can pull project-linked parties into meeting defaults so recurring collaboration setup is faster.
- **CRM-22** As a delivery manager, I can see primary customer, partner, and sponsor data on project-related surfaces so operational teams stay commercially aware.
- **HR-14** As a resource manager, I can allocate a person or delivery unit to a project with a percentage and dates so staffing commitments are explicit.
- **HR-15** As a delivery manager, I can see current and future allocations for a person, contractor, or delivery unit so overloads are visible.
- **HR-31** As a resource manager, I can assign a company or unit instead of a named person to early staffing placeholders so rough planning can start before individuals are known.
- **AI-05** As a project manager, I can assign an AI agent to a project, work item, or meeting follow-up so blended teams are supported.

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- Projects show primary related parties on list or detail surfaces.
- Workbench participant creation can pick existing parties or create new ones.
- Meeting and work-item editors can assign central parties.
- Project-local-only participants remain supported.
- No existing structure flow is broken by central-party integration.
