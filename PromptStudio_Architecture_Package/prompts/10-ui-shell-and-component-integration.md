# Codex Prompt 10 — UI Shell Hardening and Component Integration

## Objective
Refine the shared shell, page templates, reusable components, and cross-module UX consistency so the application feels unified and production-shaped.

## Required reading
1. `docs/03-ui-architecture-and-ascii-layouts.md`
2. `docs/04-solution-architecture.md`
3. `docs/07-implementation-plan.md`
4. `docs/08-checklists.md`

## Constraints
- Use .NET 10 and C#.
- Use Blazor Web App with Interactive Server rendering.
- Use Tailwind CSS and the shared component strategy.
- Keep code comments in English.
- Preserve the modular monolith boundaries from the architecture package.
- Prefer one `DbContext` per operation via `IDbContextFactory`.
- Keep business logic out of page-only code.
- Do not log or expose secrets.
- Add or update tests for the touched behavior.
- Keep naming and file structure aligned with the package documents.

## Scope
This prompt consolidates the UI into a cohesive whole. It may run after or partially alongside feature prompts once core functionality exists.

## Tasks
1. Review all existing pages against the UI architecture document.
2. Build or refine shared page templates, right-side drawer behavior, action toolbar patterns, badges, empty states, and error/loading states.
3. Ensure shell-wide context visibility for workspace, project, and current phase.
4. Standardize navigation, breadcrumbs, status presentation, and action ordering.
5. Build or refine any missing shared components that are repeatedly needed across modules.
6. Improve usability of the Dashboard and project workspace summary views.
7. Add component tests for shared shell and component patterns.

## Required deliverables
- refined shell and page templates
- standardized shared components
- improved dashboard and workspace summaries
- component tests for shell-level behavior

## Acceptance criteria
- the app feels consistent across modules
- repeated UI patterns are centralized into reusable components
- empty/loading/error states exist on the major pages
- current project/phase context is clearly visible
- component tests cover critical shared shell patterns

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the UI has a unified, repeatable interaction model across the implemented modules.