# Executive Summary

## High-Level Findings

The application already has a recognizable shell and several reusable UI pieces, but the user experience is still dominated by page-local composition decisions.

The most important issues are structural:

1. The shell repeats page identity instead of framing it.
2. The global right rail is always present even when it does not help the current task.
3. Standard pages share wrappers but not shared page patterns.
4. Most CRUD screens rely on raw markup instead of reusable action, list, form, and state components.
5. The two canvas workbenches are materially more mature than the rest of the application and should not be destabilized in phase 1.

## Biggest UI / Layout Problems

### 1. Too much chrome before the real task starts

On most routes the user encounters, in order:

- shell top bar
- tab strip
- active tab summary row
- page header
- page content

That stack creates unnecessary vertical cost before the user can act.

### 2. Duplicate page introduction

`MainLayout.razor` already displays the active route title and route description in the top bar. Most pages then render `PageHeader` with another title and description immediately inside the body.

This duplication:

- weakens hierarchy
- makes pages feel taller than they are
- hides primary actions below multiple introductory bands

### 3. The right rail is system-centric, not task-centric

The shell right rail always shows workbench and context diagnostics. That is useful as secondary context, but it competes with task content on standard pages and is especially costly around focus-heavy pages.

### 4. Shared components are not yet a full page-composition system

The repository has:

- low-level primitives in `CanDoItAll.Components`
- higher-order shell pieces in `CanDoItAll.ComponentKit`

But the app still lacks reusable page-level patterns such as list/detail shells, filter bars, empty states, sticky form actions, and standardized list headers.

### 5. Standard pages hide primary actions in body cards

`PageHeader` supports actions, but the page set does not use that capability. Primary actions like `New project`, `New resource`, `New validation`, and `New test plan` sit inside the first card instead of at the page level.

### 6. Similar pages solve the same problem differently

Projects, Resources, Prompt Gallery, Validation, Test Lab, and Settings are all data-heavy management surfaces, but they do not share a standard pattern for:

- list selection state
- filters
- editor sectioning
- action placement
- empty states
- save/reset/delete regions

## Highest-Value Opportunities

1. Create route-aware shell modes:
   - `StandardPage`
   - `FocusWorkbench`

2. Remove duplicate route introduction:
   - shell should provide context
   - page header should own the task

3. Add a real page-composition layer in `CanDoItAll.ComponentKit`:
   - page scaffold
   - list/detail shell
   - filter/search row
   - form section
   - sticky action footer
   - empty/loading states

4. Migrate the standard management pages before touching protected canvas internals.

5. Keep the project structure and prompt factory workbenches intact, but give them a better surrounding shell mode with less noise and more room.

## Strategic Recommendation For Phase 1

Phase 1 should not be a visual rewrite.

It should be a controlled layout standardization pass with this order:

1. stabilize shell behavior and route-aware page modes
2. add missing shared composition components
3. migrate the standard CRUD/management pages onto those patterns
4. give the two protected workbenches a safer focus-shell wrapper
5. finish with consistency and QA

If phase 1 does that well, phase 2 can decide whether the protected workbenches need deeper information-architecture work. Phase 1 should not take that risk.

