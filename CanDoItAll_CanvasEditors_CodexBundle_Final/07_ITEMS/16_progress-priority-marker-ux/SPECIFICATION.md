
# Specification

## Item identity

- **Item ID:** I16
- **Title:** Progress, priority, and marker UX normalization
- **Origin:** docx
- **Dependencies:** I01

## Objective

Resolve ambiguity around the small status controls and make them easier to use accurately.

## Normalized scope

Normalize click behavior for progress, priority, and markers, and enlarge compact-ring hit targets in the right-click menu.

### In scope

- Badge interaction behavior.
- Compact ring sizing and menu ergonomics.
- Associated adapter and component tests.

### Out of scope

- A total redesign of all node action affordances.

## Key implementation decisions

- Normalize the inconsistent note by using left-click progress badge for progress only and left-click priority badge for priority only.
- Keep marker selection separate rather than overloading one icon with multiple semantic meanings.
- Increase compact control diameter and hit targets for accessibility and reliability.

## Implementation tasks

- Separate progress and priority interaction pathways clearly.
- Increase ring or badge sizes and update any hit testing if needed.
- Review accessibility and keyboard affordances for the status controls.

## Risks to control

- Users will keep misfiring updates if control hit targets remain too small.

## Covered original notes

- N126 — Common
- N127 — Left click on Progress icon in node must show only selector of priority
- N128 — Markers and progress main circle in right click menu must have larger diameter
