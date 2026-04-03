# Phase gates

## Wave A

Bundles in scope:
- B01 — Foundation: unified party domain, schema, and module skeleton
- B02 — Directory shell, navigation, routes, and core BaseLib pages

Wave gate criteria:
- `CanDoItAll.Modules.CrmHr` exists and is registered.
- `/crm-hr` routes load without shell errors.
- a party can be created, edited, archived, and listed through BaseLib pages.
- startup seeding works on a fresh SQLite database.

## Wave B

Bundles in scope:
- B03 — Contact points, addresses, relationships, org structure, import/export, and duplicate merge
- B06 — HR workforce structure, worker profiles, and delivery units
- B09 — AI agent profiles, provider bindings, capabilities, and governance

Wave gate criteria:
- relationships, contact points, and dedupe flows work end to end.
- workforce profiles and delivery units are editable.
- AI agents can be created and bound to provider profiles.
- no canvas components have been introduced into CRM/HR pages.

## Wave C

Bundles in scope:
- B10 — Project and Workbench party assignment integration
- B04 — CRM accounts, contacts, stakeholders, interaction journal, and follow-ups

Wave gate criteria:
- project summaries display CRM/HR context.
- workbench participant, meeting, and work-item flows can use central parties.
- CRM interactions and follow-ups work against real parties.

## Wave D

Bundles in scope:
- B05 — Opportunities, pipeline, stage history, and project conversion
- B07 — Skills, capacity, staffing requests, bench management, and allocations
- B08 — Recruitment pipeline, interviews, onboarding, and offboarding

Wave gate criteria:
- opportunity pipeline works and won opportunities can convert into project context.
- staffing requests and allocations affect capacity views.
- recruiting, interviews, onboarding, and offboarding flows are operational.

## Wave E

Bundles in scope:
- B11 — Cross-module integration with search, activity, resources, validation, test lab, and automation
- B12 — Security, privacy, audit, and safe lifecycle controls
- B13 — Validation hardening, rollout, migration rehearsal, and regression suite

Wave gate criteria:
- cross-module ownership/search/activity integration is visible.
- privacy/audit rules are enforced.
- automated test layers pass.
- Playwright screenshots exist and have semantic review notes.
