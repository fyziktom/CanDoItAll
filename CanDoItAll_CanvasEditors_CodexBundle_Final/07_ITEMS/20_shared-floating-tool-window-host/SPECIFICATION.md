
# Specification

## Item identity

- **Item ID:** I20
- **Title:** Shared floating tool window host for canvas editors
- **Origin:** conversation
- **Dependencies:** None

## Objective

Create one reusable floating tool-window shell that both canvas editors can use for pinned, movable, searchable auxiliary panes.

## Normalized scope

Generalize the existing floating inspector patterns into a shared floating tool-window host inspired by Visual Studio tool windows and constrained to the visible canvas.

### In scope

- Reusable floating tool-window shell.
- Canvas-bound movement and fit-to-visible-canvas behavior.
- Shared header actions such as show or hide, pin, move, and close where appropriate.
- Slots for tree views, search bars, and preview content.

### Out of scope

- Solving every individual toolbox content requirement by itself; those land in dedicated downstream items.

## Key implementation decisions

- Build a shared host instead of implementing separate ad-hoc floating windows for Prompt Factory and Project Structure.
- The host must support show or hide, pin, drag, bounds clamping, and internal scrolling.
- Toolbar window behavior should feel closer to Visual Studio Solution Explorer than to temporary accordions or transient context menus.

## Implementation tasks

- Generalize or extend the floating inspector host into a reusable tool-window host.
- Add pin, move, bounds-clamp, and scroll behavior.
- Support consistent header and body slots for search, tree content, and previews.
- Ensure the host is visually safe on smaller canvases and within the visible stage.

## Risks to control

- Two separate floating panel systems will drift apart quickly and multiply bugs.

## Covered original notes

- No direct DOCX note mapping. This item exists because the user explicitly required cross-cutting validation or shared architecture.
