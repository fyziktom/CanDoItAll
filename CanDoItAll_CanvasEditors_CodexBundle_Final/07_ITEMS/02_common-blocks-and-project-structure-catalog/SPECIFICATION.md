
# Specification

## Item identity

- **Item ID:** I02
- **Title:** Common starter blocks and project structure catalog refresh
- **Origin:** docx
- **Dependencies:** I01

## Objective

Add the missing starter blocks for the project structure canvas and make them discoverable in a cleaner catalog.

## Normalized scope

Create first-class starter blocks for Deployment, Repos, Dockers, Task Flow, Backlog, and Server, with consistent visual profiles and creation paths.

### In scope

- Project structure create catalog.
- Default visual profiles for the new starter blocks.
- Any supporting factory methods or subtype mapping required to create them.

### Out of scope

- Deep feature implementation for each child node family such as repositories, servers, or workflows.

## Key implementation decisions

- Treat these as starter or grouping nodes rather than heavyweight domain entities.
- Keep the catalog grouped and searchable so future block families stay manageable.
- Prefer reuse of existing visual profile mechanisms over one-off CSS classes.

## Implementation tasks

- Add starter block catalog entries and subtype mappings.
- Define node visuals and seed labels for each new block.
- Ensure creation from the canvas produces the right type, subtype, and placement defaults.
- Update any outline or navigation views that list block families.

## Risks to control

- Catalog clutter if grouping and search are not addressed together.

## Covered original notes

- N002 — Common Bloks
- N003 — Deployment
- N004 — Repos
- N005 — Dockers
- N006 — Task Flow
- N007 — Backlog
- N008 — Server
