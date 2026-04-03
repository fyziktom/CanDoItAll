# Implementation prompt

Implement **B10 — Project and Workbench party assignment integration** for CanDoItAll.

## Bundle goal

Connect projects and project-structure nodes to the new directory so customer, partner, delivery unit, participant, meeting, work item, and AI-agent assignment flows all use the unified Party model.

## Hard rules

- follow `03_ARCHITECTURE/*` and `02_REQUIREMENTS/SCOPE_AND_NON_FUNCTIONAL_DECISIONS.md`
- keep UI in BaseLib / Razor / HTML only
- do not introduce canvas components
- preserve backward compatibility for existing project/workbench flows where relevant
- add or update tests listed in `FILE_REFERENCES.md`
- add screenshot evidence requirements from `SCREENSHOT_REQUIREMENTS.md`

## Implementation steps

1. Inspect all files in `FILE_REFERENCES.md`.
2. Implement the data model / service changes required for this bundle.
3. Implement the route or UI changes required for this bundle.
4. Wire search/activity/integration seams if this bundle requires them.
5. Add automated tests at the correct level.
6. Execute browser validation and capture screenshots.
7. Write a concise evidence note summarizing code changes, tests, and screenshots.

## Bundle-specific targets

- Create project-party assignment infrastructure and project summary enrichment.
- Add central party picker flows to Workbench participant, meeting, and work-item editors.
- Allow project-local participant fallback when central registry is intentionally not used.
- Ensure allocations and AI-agent reuse can link through the same assignment layer.

## Stories that must be satisfied in this bundle

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

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
