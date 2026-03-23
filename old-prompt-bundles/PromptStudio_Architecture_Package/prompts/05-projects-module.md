# Codex Prompt 05 — Projects Module and Stack Profile

## Objective
Implement projects, phases, statuses, generalized option selections, and the project workspace baseline.

## Required reading
1. `docs/01-ux-discovery.md`
2. `docs/02-technical-requirements.md`
3. `docs/03-ui-architecture-and-ascii-layouts.md`
4. `docs/03a-workbench-tabs-canvas-and-state.md`
5. `docs/04-solution-architecture.md`
6. `docs/07-implementation-plan.md`

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
2. Implement wizard-first project creation and major edit flows instead of relying on one raw editor form as the primary UX.
3. Implement phase timeline and status handling.
4. Implement the generalized option selection model for language, DB, UI, external APIs, storage, and notes.
5. Build Project Overview and Stack Profile pages according to the UI document.
6. Add opened-project tab semantics so a project can become a first-class internal work item.
7. Introduce the unified project-object graph baseline required by the workbench and later canvas editing flows.
8. Prepare project routes and artifact-opening contracts used later by the project structure and project calendar workbench surfaces.
9. Add project summary/query models for dashboard and list screens.
10. Add tests for project creation, phase handling, option selection persistence, and the wizard-first flow.

## Required deliverables
- Projects module domain and persistence
- wizard-first project creation and editing UI
- project overview page
- stack profile page
- opened-project tab baseline
- unified project-object graph baseline
- option selection infrastructure
- automated tests

## Acceptance criteria
- a project can be created end-to-end
- project creation is guided and comfortable instead of raw CRUD
- dates, phases, and statuses persist correctly
- option selections and notes persist correctly
- project list and overview views are usable, not placeholder-only
- a project can be opened as a meaningful internal work item
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
Stop when the project workspace becomes a credible center of the application and no longer depends on raw CRUD as the main experience.
