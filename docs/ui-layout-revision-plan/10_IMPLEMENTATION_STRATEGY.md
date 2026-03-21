# Implementation Strategy

## Execution Status Update (2026-03-20)

The recommended phase order was largely followed.

Completed in code:

- Phase 1: shell foundations
- Phase 2: shared composition components
- Phase 3: high-value standard pages
- Phase 4: medium-complexity standard pages
- Phase 5: protected-route shell adoption

Phase 6 and Phase 7 remained necessary after the first pass because live QA exposed rollout gaps that were not obvious from code inspection alone:

- stale Tailwind output made several desktop layouts appear unfinished even though Razor markup had already been migrated
- resources, test lab, prompt gallery, and settings still needed missing filters or create affordances
- protected structure-canvas interactions required interop fixes during real browser validation

## Strategy Summary

Phase 1 should be executed as a controlled sequence, not as broad parallel cleanup.

The order matters because:

- shell behavior affects every route
- shared page-composition components reduce duplication before migration begins
- protected workbench routes should only receive shell-level treatment after the new shell mode exists

## Recommended Execution Order

### Phase 0: Baseline And Guardrails

Goals:

- read the protected-area rules
- identify route groups
- confirm which pages are standard management pages versus focus workbenches

Required outputs:

- route-to-shell-mode map
- protected-area checklist attached to the implementation task
- explicit reminder to rebuild Tailwind output before visual QA

### Phase 1: Global Shell Foundations

Targets:

- `MainLayout.razor`
- shell mode routing
- navigation behavior below `lg`
- right-rail visibility rules
- active tab summary placement

Goals:

- remove duplicate page introduction
- define `StandardPage` and `FocusWorkbench` shell modes
- keep navigation reachable on smaller viewports
- reduce non-essential chrome on high-focus routes

Exit criteria:

- standard pages and protected workbench pages can render under different shell policies
- smaller screens still have access to navigation

### Phase 2: Shared Composition Components

Targets:

- `CanDoItAll.ComponentKit`

Goals:

- add the missing page-composition primitives before migrating pages

Priority components:

- `PageScaffold`
- `ListDetailShell`
- `ListPanelHeader`
- `FormSection`
- `StickyActionFooter`
- `EmptyState`
- `FilterBar`

Exit criteria:

- at least one standard page can be migrated without bespoke page-local layout helpers

### Phase 3: High-Value Standard Pages

Recommended order:

1. Projects
2. Resources
3. Validation Center
4. Test Lab
5. Settings

Why this order:

- these pages are the clearest examples of reusable list/detail and long-form problems
- solving them creates shared patterns that later pages can adopt more cheaply

Exit criteria:

- these pages share the same structural grammar
- selected state, primary actions, form sections, and sticky actions are consistent

### Phase 4: Medium-Complexity Standard Pages

Recommended order:

1. Prompt Gallery
2. Activity
3. Automation
4. Dashboard
5. Project Calendar

Goals:

- align lower-risk pages to the new system
- make the dashboard operational
- improve search/timeline and status-summary pages

### Phase 5: Protected Route Shell Adoption

Targets:

- Project Structure
- Prompt Factory

Goals:

- apply the quieter `FocusWorkbench` shell mode
- widen the working area
- remove redundant surrounding chrome

Important rule:

- do not redesign internal workbench composition during this phase

### Phase 6: Consistency And State Sweep

Goals:

- standardize empty/loading/error states
- standardize status chip treatment
- verify button hierarchy and destructive action placement
- remove obvious remaining one-off page-local layout patterns

### Phase 7: QA Sweep

Goals:

- route-by-route UX review
- responsive review
- protected-area regression review
- accessibility sanity pass
- live Tailwind/CSS verification so screenshots reflect the real shipped styles

## Dependency-Aware Sequencing

### Why shell first

If the shell stays noisy, page cleanup will still feel compromised.

### Why shared components second

If pages migrate before shared composition exists, the codebase will collect another layer of one-off wrappers.

### Why protected routes late

Protected routes should benefit from the new shell mode without becoming test-heavy merge conflict zones during the broader migration.

## Implementation Rules

1. Prefer shared composition to page-local helper markup.
2. Do not build a second UI library inside module pages.
3. Do not expand scope into canvas internals.
4. Do not rewrite services to make the layout work.
5. Do not replace everything at once when a route-by-route migration is safer.
6. Rebuild `Tailwind/output.css` before any visual signoff or browser screenshot review.
7. Treat live Playwright review as mandatory for protected routes and desktop list/detail pages.

## Suggested Page Migration Batch

### Batch A

- Projects
- Resources

Purpose:

- prove the list/detail shell and form-section model

### Batch B

- Validation Center
- Test Lab

Purpose:

- prove long-form review/result composition

### Batch C

- Settings
- Prompt Gallery

Purpose:

- prove secondary tabs and admin/list-detail patterns

### Batch D

- Activity
- Automation
- Dashboard
- Project Calendar

Purpose:

- finish lower-risk alignment and polish

### Batch E

- Project Structure shell mode
- Prompt Factory shell mode

Purpose:

- apply the quiet-shell treatment after the shell model is stable
