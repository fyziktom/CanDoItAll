# Scope And Constraints

## In Scope

- shell-level layout cleanup
- removal of redundant layout chrome
- route-aware shell modes
- page header and action placement standards
- list/detail page standardization
- form sectioning and action-region standardization
- empty/loading/error state standardization
- responsive behavior for navigation and page composition
- shared component additions needed to support those changes
- migration guidance for the main non-canvas routes

## Out Of Scope

- internal redesign of the project structure canvas
- internal redesign of the prompt factory canvas
- changes to canvas interaction vocabulary, context menus, or JS interop
- business-rule rewrites in page services
- database/schema redesign
- major route/feature re-architecture
- a full design-language rebrand
- replacing every low-level component in `CanDoItAll.Components`

## Technical Constraints

- stack: C#, .NET 10, Blazor interactive server rendering, Tailwind CSS
- shell host: `src/CanDoItAll.Web`
- low-level shared components: `src/CanDoItAll.Components`
- higher-order shell/workbench components: `src/CanDoItAll.ComponentKit`
- feature pages are mostly module-local Razor pages with inline markup and logic
- several management pages use query-string selected editors instead of separate detail routes
- the workbench tab system and browser persistence are already part of the product model and must remain intact

## UX Constraints

- the app is a power-user local workbench, not a marketing site
- density is acceptable, but it must be intentional and structured
- primary actions must become easier to find without making the app simplistic
- the user should always know:
  - what area they are in
  - what the main task is
  - what the next meaningful action is

## Architectural Constraints

- respect module boundaries
- prefer reusable page-composition components over page-specific one-offs
- do not force every change through the older `CanDoItAll.Components` library
- use `CanDoItAll.ComponentKit` for page-shell and workbench-adjacent composition primitives
- do not expand placeholder primitives such as `Dialog` or `ContextMenu` unless phase 1 truly depends on it

## Stable Areas Not To Disturb

The following areas are stable in phase 1 and are expanded in `03_PHASE1_PROTECTED_AREAS.md`:

- the project structure workbench
- the prompt factory workbench

This includes their canvas interactions, selection behavior, context actions, and core supporting inspector logic.

## Practical Phase-1 Boundary

Phase 1 should mostly change:

- `MainLayout.razor`
- shell mode behavior
- page composition wrappers
- standard CRUD/management pages
- shared composition helpers

Phase 1 should mostly avoid:

- `CanvasWorkbench` internals
- workbench JS/CSS contracts
- complex prompt/session logic
- project graph authoring logic

