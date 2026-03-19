# Codex Prompt 05 — Projects Module and Stack Profile

## Objective
Implement projects, phases, statuses, generalized option selections, and the project workspace baseline.

## Required reading
1. `docs/01-ux-discovery.md`
2. `docs/02-technical-requirements.md`
3. `docs/03-ui-architecture-and-ascii-layouts.md`
4. `docs/04-solution-architecture.md`
5. `docs/07-implementation-plan.md`

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
This prompt covers M3: project creation, editing, overview, stack profile, dates, phases, and generalized option selections with notes.

## Tasks
1. Implement the Projects module aggregates and persistence.
2. Implement project creation and edit flows.
3. Implement phase timeline and status handling.
4. Implement the generalized option selection model for language, DB, UI, external APIs, storage, and notes.
5. Build Project Overview and Stack Profile pages according to the UI document.
6. Add project summary/query models for dashboard and list screens.
7. Add tests for project creation, phase handling, and option selection persistence.

## Required deliverables
- Projects module domain and persistence
- project creation and editing UI
- project overview page
- stack profile page
- option selection infrastructure
- automated tests

## Acceptance criteria
- a project can be created end-to-end
- dates, phases, and statuses persist correctly
- option selections and notes persist correctly
- project list and overview views are usable, not placeholder-only
- the generalized option model is implemented instead of hardcoded one-off fields
- tests pass for project flows

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the project workspace becomes a credible center of the application.