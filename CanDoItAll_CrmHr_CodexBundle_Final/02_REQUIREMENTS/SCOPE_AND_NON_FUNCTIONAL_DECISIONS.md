# Scope and non-functional decisions

## In scope

- A merged **CRM / HR module** implemented as `CanDoItAll.Modules.CrmHr`
- One unified **Party** root for:
  - person
  - organization
  - organization unit / delivery unit
  - AI agent
- CRM:
  - accounts, contacts, stakeholders
  - interactions and next actions
  - opportunities and pipeline
  - conversion of won opportunities into project context
- HR:
  - employee, contractor, freelancer, and delivery-unit profiles
  - workforce structure and reporting lines
  - skills, certifications, availability, staffing requests, allocations
  - recruitment, interviews, onboarding, and offboarding
- AI agents:
  - shared identity
  - provider-profile binding
  - ownership, capability, validation, and project assignment
- CanDoItAll integration:
  - projects
  - workbench participants / meetings / work items
  - resources
  - validation
  - test lab
  - activity
  - search
  - automation reminders
- Privacy, audit, archive controls, and screenshot-backed QA

## Explicitly out of scope for this bundle

- payroll processing
- tax reporting
- employee benefits administration
- attendance clocking and time-payroll reconciliation
- commissions engine
- customer support ticketing
- marketing campaigns and email automation
- full document DMS or e-signature suite
- external calendar or email sync as a mandatory dependency

## Design rules

1. **BaseLib only for UI**  
   Use `CanDoItAll.Components.BaseLib` plus normal Razor/HTML/Tailwind. Do not bring canvas libs into this module.

2. **Shared identity, not duplicated sub-registries**  
   CRM and HR views sit on the same Party root. Do not create separate `Customer`, `Employee`, and `AiAgent` identity tables that cannot be reconciled.

3. **Roles and profiles, not duplicate persons**  
   The same party may be customer contact, employee, candidate, or AI steward across different contexts.

4. **Archive before delete**  
   If a party is referenced by projects, opportunities, interactions, or staffing data, use archive / inactive status instead of hard deletion.

5. **Search is selective**  
   General search indexes operationally useful fields, but confidential HR notes and sensitive content stay out of the broad search index.

6. **Structured extension points are allowed**  
   Use `ExtendedDataJson` or equivalent on selected entities for future custom fields. Do not explode schema for every edge-case field.

7. **Project integration is a first-class requirement**  
   The module is incomplete if it cannot assign parties to projects, workbench nodes, meetings, and work items.

## Non-functional requirements

### Data quality

- Normalize names and contact values for duplicate detection.
- Keep external identifiers and source metadata where import matters.
- Preserve historical relationships when merging duplicates.

### Performance

- Default list screens must be filterable and paged.
- Frequently filtered columns require indexes.
- Party detail loads should fetch only the selected aggregate, not entire registries.

### Migration safety

- Existing Workbench participant nodes must remain readable.
- New fields added to project metadata must be backward compatible.
- Startup seeding must remain deterministic and idempotent.

### Privacy and audit

- Sensitive HR notes must be isolated from broad search and broad lists.
- Important changes must be audit-recorded.
- The module must be future-ready for RBAC even though the current repo uses a local-user model.

### Testability

- Every subbundle requires at least one targeted automated test layer.
- Every UI-changing subbundle requires Playwright flows and screenshots.
- Screenshot files without semantic review notes do not satisfy the quality gate.
