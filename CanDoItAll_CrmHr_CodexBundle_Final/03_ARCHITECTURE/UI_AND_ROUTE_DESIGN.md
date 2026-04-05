# UI and route design

## 1. UI strategy

The CRM/HR module should look like a natural extension of existing **Projects** and **Resources** pages:

- BaseLib page scaffold
- clear page headers
- summary tiles
- secondary tabs
- list/detail shells
- inline editors grouped into sections
- simple cards or columns for pipeline and staffing summaries

## 2. Route map

| Route | Purpose | Primary UI pattern | Key actions |
| --- | --- | --- | --- |
| `/crm-hr` | Module home | `PageScaffold` + `SummaryTiles` + `SecondaryTabs` | Jump to Directory / CRM / Workforce / Recruiting / Agents / Assignments |
| `/crm-hr/directory` | Shared party registry | `ListDetailShell` | create, archive, search, merge, import/export, edit roles/contact points/relationships |
| `/crm-hr/crm` | Accounts, interactions, opportunities | mixed card + detail layout | account review, log interaction, next actions, opportunity pipeline |
| `/crm-hr/workforce` | Employees, contractors, units | `ListDetailShell` + summary cards | maintain profiles, org structure, rates, home units |
| `/crm-hr/recruiting` | Candidate lifecycle | list/detail or simple board | stage, interviews, feedback, convert candidate, onboarding/offboarding |
| `/crm-hr/agents` | AI-agent governance | `ListDetailShell` | bind provider profile, edit capabilities, assign owner, set validation state |
| `/crm-hr/assignments` | Staffing and project allocations | card + filter + detail | staffing requests, allocations, capacity conflicts, bench visibility |

## 3. Shared page composition rules

### Header zone

Use:

- `PageScaffold`
- `PageHeader`
- summary or directional description
- header actions like create/import/export only where appropriate

### Tab zone

Use `SecondaryTabs` or equivalent BaseLib navigation for:

- Directory
- CRM
- Workforce
- Recruiting
- AI Agents
- Assignments

### Main body zone

Preferred patterns:

- `ListDetailShell` for registries and detail editing
- cards / board columns only where the workflow really benefits from stage grouping
- `FilterBar` for search and status filters
- `FormSection` for dense editors
- `StickyActionFooter` for save/archive/convert actions

## 4. No-canvas rule

Do **not** import or reference:

- canvas workbench surfaces
- floating toolbox hosts
- radial menus
- node cards
- scene overlays
- prompt-factory canvas libraries

This module is intentionally a **business data workspace**, not a visual canvas editor.

## 5. Page-level design notes

### Directory

- left pane: search, filters, quick metrics, list of parties
- right pane: party editor
- sections: identity, roles, contact methods, addresses, org relationships, tags, notes, recent activity, confidential notes (only where appropriate)
- merge and import/export live here, not in Projects

### CRM

- summary strip: accounts, open opportunities, overdue next actions, recently active accounts
- account list or account cards
- interaction timeline panel
- opportunity board or grouped list by stage
- conversion button on opportunities when stage becomes won

### Workforce

- filters: workforce kind, home unit, manager, discipline, availability, seniority
- detail panel: job profile, rates, capacity, assignments, skills, certifications
- org structure summary card at top

### Recruiting

- candidate list or grouped stage columns
- detail panel: application, interviews, feedback, onboarding/offboarding tasks
- convert to employee/contractor action

### AI agents

- list of AI agents with execution mode, owner, provider binding, review status
- detail panel: capabilities, limitations, linked provider profile, default model, notes
- assignment visibility to projects and tasks

### Assignments

- filters: project, unit, assignee, allocation window, status
- central table/card list of staffing requests and project allocations
- conflict callouts where allocation exceeds availability

## 6. Project-side UX requirements

Project surfaces remain in their own modules, but must gain CRM/HR awareness:

- Projects page cards should display at least:
  - primary customer
  - delivery unit
  - project manager or owner
  - optionally linked opportunity indicator
- Workbench participant creation/editing should let the user:
  - pick from central directory
  - create new central party
  - mark a participant project-local only
- Meeting and work-item editors should use the same central party picker

## 7. Empty-state and privacy behavior

Use explicit callouts when:

- a section is intentionally empty,
- sensitive HR notes are hidden from broad views,
- no provider profile is linked to an AI agent,
- a project node is still project-local and not synced to a central party,
- a duplicate-merge suggestion needs manual confirmation.

## 8. UI success conditions

The UI design is correct only if it is possible to:

- create a party without touching Workbench,
- assign that party to a project later,
- open CRM and HR views without losing identity continuity,
- work with AI agents in the same directory experience,
- and navigate everything through shell routes using BaseLib-first screens.
