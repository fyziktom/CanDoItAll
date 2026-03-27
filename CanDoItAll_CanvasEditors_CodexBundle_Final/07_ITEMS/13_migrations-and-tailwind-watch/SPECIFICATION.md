
# Specification

## Item identity

- **Item ID:** I13
- **Title:** EF migrations and Tailwind watch nodes
- **Origin:** docx
- **Dependencies:** I10, I12

## Objective

Add execution-oriented nodes for database migrations and Tailwind watch so common developer workflows live on the canvas.

## Normalized scope

Implement migration command nodes and Tailwind watch nodes with project-aware command storage and terminal execution reuse.

### In scope

- EF migration nodes and command selection or input.
- Tailwind watch nodes and command configuration.
- Execution handoff into the shared terminal or runtime surface.

### Out of scope

- Smart command generation for every possible custom repo layout.

## Key implementation decisions

- Reuse the same execution surface created for scripts and runtime nodes.
- Store commands explicitly and transparently so users can inspect and adjust them.
- Support project-aware defaults but allow manual overrides.

## Implementation tasks

- Add migration and Tailwind node subtypes and metadata.
- Define project-aware command defaults and manual override fields.
- Wire the nodes to the shared terminal execution path.

## Risks to control

- Opaque command generation makes debugging impossible for users.

## Covered original notes

- N096 — Apply Migration EF (add, update, etc)
- N097 — Select or input of command I call from ps for some migrations
- N098 — Tailwind watch run (for projects that use tailwind)
- N099 — Command how to run in ps tailwind for that specific project
