
# Specification

## Item identity

- **Item ID:** I06
- **Title:** Task, issue, and assignment model
- **Origin:** docx
- **Dependencies:** I01, I05

## Objective

Create robust work-item nodes that connect the canvas to delivery ownership and basic execution planning.

## Normalized scope

Add Task and Issue nodes with what/when/who metadata, repo links or free-form description, and compatibility with participant selectors and attachments.

### In scope

- Task and issue node creation and editing.
- Assignment, due date, description, and repository linkage.
- Attachment compatibility points.

### Out of scope

- A full kanban or sprint board implementation.

## Key implementation decisions

- Treat Task and Issue as work-item variants with shared shape and fields.
- Use participant registry selectors for who-assignment wherever possible.
- Allow either a linked repository reference or a pure textual description for issues.

## Implementation tasks

- Add shared work-item metadata and dedicated task/issue subtypes.
- Wire assignment selector to participants.
- Allow optional repository reference or plain description path for issues.
- Expose concise status information on the node card.

## Risks to control

- Assignment UX becomes inconsistent if participant selector integration is skipped.

## Covered original notes

- N048 — Tasks
- N049 — Task
- N050 — What, when, who (for already added HRs offered selector)
- N051 — Issue
- N052 — Possible link to repo
- N053 — Or pure description
- N054 — Attachments
