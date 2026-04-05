# Implementation sequence

Implement in dependency order. The order below is designed to keep schema, UI, project integration, and validation aligned.

## Ordered execution

### Wave A

B01 — **Foundation: unified party domain, schema, and module skeleton**
- Purpose: Create the new CRM/HR module project, full relational schema, seed strategy, service registration, startup wiring, and core DTOs around a unified Party model that can represent persons, organizations, organization units, and AI agents.
- Depends on: None

B02 — **Directory shell, navigation, routes, and core BaseLib pages**
- Purpose: Add the CRM / HR shell entry, root pages, route structure, summary dashboard, directory workspace, and BaseLib-first page composition without using canvas components.
- Depends on: B01

### Wave B

B03 — **Contact points, addresses, relationships, org structure, import/export, and duplicate merge**
- Purpose: Finish the party directory by implementing contact methods, addresses, role assignments, relationship editors, import/export flows, and a safe duplicate merge experience.
- Depends on: B01, B02

B06 — **HR workforce structure, worker profiles, and delivery units**
- Purpose: Add workforce profiles for employees, contractors, freelancers, and delivery units, including reporting lines, home units, lifecycle dates, rates, seniority, and structure-aware views.
- Depends on: B01, B02, B03

B09 — **AI agent profiles, provider bindings, capabilities, and governance**
- Purpose: Make AI agents a first-class party type with provider bindings, human ownership, capability records, validation status, and directory visibility.
- Depends on: B01, B02, B03

### Wave C

B10 — **Project and Workbench party assignment integration**
- Purpose: Connect projects and project-structure nodes to the new directory so customer, partner, delivery unit, participant, meeting, work item, and AI-agent assignment flows all use the unified Party model.
- Depends on: B01, B02, B03, B06, B09

B04 — **CRM accounts, contacts, stakeholders, interaction journal, and follow-ups**
- Purpose: Implement account and contact views, stakeholder role handling, interaction logging, account summaries, and overdue next-action workflows on top of the unified party model.
- Depends on: B01, B02, B03

### Wave D

B05 — **Opportunities, pipeline, stage history, and project conversion**
- Purpose: Build the opportunity board, structured stage progression, stage history, partner-sourced deals, lost reasons, and conversion of won opportunities into CanDoItAll project context.
- Depends on: B01, B02, B03, B04, B10

B07 — **Skills, capacity, staffing requests, bench management, and allocations**
- Purpose: Implement skill catalog handling, proficiency, certifications, availability blocks, staffing requests, project allocations, bench views, and demand-versus-capacity reporting.
- Depends on: B01, B02, B03, B06, B10

B08 — **Recruitment pipeline, interviews, onboarding, and offboarding**
- Purpose: Implement candidate handling, interview scheduling, structured feedback, hiring conversion, onboarding and offboarding task management, and lifecycle reminders.
- Depends on: B01, B02, B03, B06

### Wave E

B11 — **Cross-module integration with search, activity, resources, validation, test lab, and automation**
- Purpose: Finish enterprise integration by indexing CRM/HR artifacts, writing activity events, linking owners to resources, validation, and tests, and wiring reminder-style automation jobs.
- Depends on: B01, B02, B03, B04, B05, B06, B07, B08, B09, B10

B12 — **Security, privacy, audit, and safe lifecycle controls**
- Purpose: Add sensitive-data markers, audit entries, soft-delete rules, safe search behavior, HR-only note separation, and future-ready permission seams suitable for the current local-user model.
- Depends on: B01, B02, B03, B04, B06, B08, B11

B13 — **Validation hardening, rollout, migration rehearsal, and regression suite**
- Purpose: Create the final quality gate: broad automated tests, Playwright coverage, screenshot semantics, seed data rehearsal, migration verification, and rollout/rollback notes.
- Depends on: B01, B02, B03, B04, B05, B06, B07, B08, B09, B10, B11, B12

## Why this order works

1. **Foundation first** so all later bundles share one stable schema and route surface.
2. **Directory, workforce, and AI-agent identity** come before project integration because projects need real shared actors to point at.
3. **Project/workbench integration** comes before opportunity conversion and staffing so those later flows can reuse real assignment infrastructure.
4. **Cross-module and privacy hardening** come after the main business workflows exist, because otherwise the team would be wiring accountability into moving targets.
5. **Final regression and rollout** comes last and is explicitly treated as an implementation bundle, not an afterthought.

## Execution rule

Do not declare a wave complete until:

- all bundles in the wave satisfy their acceptance criteria,
- referenced tests pass,
- UI bundles have screenshots plus semantic review notes,
- and traceability still maps every user story to at least one implemented bundle.
