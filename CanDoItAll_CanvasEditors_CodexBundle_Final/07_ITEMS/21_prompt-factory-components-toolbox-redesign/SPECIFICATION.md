
# Specification

## Item identity

- **Item ID:** I21
- **Title:** Prompt Factory components toolbox redesign
- **Origin:** docx
- **Dependencies:** I20

## Objective

Replace the current wrong Prompt Factory component toolbox behavior with a real, searchable floating tool window.

## Normalized scope

Redesign the Prompt Factory components surface from the existing toolbox-panel or accordion style into a Visual Studio-inspired floating tree view with search and internal scroll.

### In scope

- Prompt Factory components toolbox container and content layout.
- Search behavior and hierarchical grouping.
- Internal scrolling and stage-fit behavior inside the floating host.
- Creation flow from the redesigned toolbox.

### Out of scope

- Fixing the intermittent 44-node insertion bug by itself; that has its own item.

## Key implementation decisions

- Treat the existing toolbox-panel implementation as a reference point, not the final UX.
- Prefer a dense tree-view or outline experience over stacked accordions for large component catalogs.
- Keep search pinned at the top and content scrollable within the window body.

## Implementation tasks

- Replace the transient or wrong component toolbox presentation with the shared floating tool-window host.
- Render component groups as a tree or equivalent dense hierarchy rather than accordions.
- Keep search bar fixed at the top and the component list independently scrollable.
- Ensure adding a component from the toolbox still routes through the proper catalog action pipeline.

## Risks to control

- Users will keep missing components if the toolbox remains visually dense but structurally weak.

## Covered original notes

- N142 — Prompt factory
- N143 — Components
- N144 — Better search of components
- N145 — It must work as toolbar in visual studio\
- N147 — Toolbar with components must be available as classic floating window toolbar inside of canvas, that I can show/hide, pin, move, etc.
- N148 — Inside accordeons with sections of prompts components
- N149 — Search bar on top
- N150 — Vartical Scrollbar inside if too much component/sections are in it. Toolbar window must fit always into visible canvas
