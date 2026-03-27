
# Specification

## Item identity

- **Item ID:** I23
- **Title:** Project Structure standard blocks toolbox
- **Origin:** docx
- **Dependencies:** I02, I20

## Objective

Give the project structure canvas the same improved floating toolbox pattern requested for Prompt Factory.

## Normalized scope

Add a floating standard-blocks toolbox to the project structure canvas, using a tree-oriented layout inspired by the Visual Studio Solution Explorer screenshot.

### In scope

- Floating standard-blocks toolbox for project structure.
- Search and grouping behavior for starter blocks and future node families.
- Create-node flow from the toolbox.

### Out of scope

- Re-implementing every project structure editor feature unrelated to block creation.

## Key implementation decisions

- Use the same shared tool-window host as Prompt Factory to avoid duplicate UX systems.
- Prefer tree-style grouping over the current in-inspector accordion stack for long block lists.
- The toolbox should complement or replace the old accordion area without creating two competing primary flows.

## Implementation tasks

- Move or duplicate standard block creation into the shared floating toolbox host.
- Render blocks in a searchable tree-style grouping.
- Keep creation behavior intact and clearly indicate the currently selected source node if relevant.

## Risks to control

- If both the old inspector accordions and the new toolbox stay primary, users may get confused about which one is canonical.

## Covered original notes

- N151 — NOTE: Similar toolbar also for standard blocks in project structure
