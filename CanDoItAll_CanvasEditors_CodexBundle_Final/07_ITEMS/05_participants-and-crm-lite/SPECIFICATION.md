
# Specification

## Item identity

- **Item ID:** I05
- **Title:** Participants and CRM-lite registry
- **Origin:** docx
- **Dependencies:** I01

## Objective

Introduce people and organization nodes without turning the app into an oversized CRM rewrite.

## Normalized scope

Add participant-related node types and a lightweight registry for HR, team blocks, team sections, freelancers, partners, and AI agents, reusable by tasks and meetings.

### In scope

- Participant node family and registry.
- HR, Team block, Team Section, Freelancer, Partner, and AI Agent variants.
- Selector reuse in downstream task assignment and meeting participation.

### Out of scope

- Sales pipelines, deal stages, or CRM-style account management.

## Key implementation decisions

- Do not implement a full CRM module; implement a lightweight participant registry and references.
- Use the same participant objects across meetings, tasks, and org-chart-like canvas structures.
- Model AI Agent as a participant-like entity with a distinct subtype and iconography.

## Implementation tasks

- Define participant metadata and visual variants.
- Create a lightweight registry or selector source for participant reuse.
- Support organization-chart-like grouping with team blocks and team sections.
- Ensure AI Agent can participate anywhere a person-like assignee or participant is allowed when semantically appropriate.

## Risks to control

- Scope explosion into full CRM functionality.

## Covered original notes

- N040 — Prarticipants
- N041 — HR
- N042 — From CRM (need to add at least basic crm module)
- N043 — Team block (for example as start of organization chart)
- N044 — Team Section (for example HW department, etc.)
- N045 — Freelancer
- N046 — Partner
- N047 — AI Agent
