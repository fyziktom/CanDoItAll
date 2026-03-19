# Codex Prompt 06A - Workbench Tabs, Project Structure Canvas, and Project Calendar

## Objective
Implement the internal tab workspace, tab persistence and sleep lifecycle, the project structure canvas wrapper, and the project events calendar wrapper so the application becomes a real project-control workbench instead of a set of disconnected pages.

## Required reading
1. `README.md`
2. `docs/03-ui-architecture-and-ascii-layouts.md`
3. `docs/03a-workbench-tabs-canvas-and-state.md`
4. `docs/03b-development-manager-watch-capsules-and-tuning.md`
5. `docs/04-solution-architecture.md`
6. `docs/06-architecture-review-gap-analysis.md`
7. `docs/07-implementation-plan.md`
8. `docs/08-checklists.md`
9. `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\README.md`
10. `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\rebuild\blazor-jsinterop-component-plan.md`
11. `C:\repositories\CanDoItAll\docs\canvas-events-calendar\README.md`
12. `C:\repositories\CanDoItAll\docs\canvas-events-calendar\rebuild\blazor-jsinterop-component-plan.md`

## Constraints
- Use .NET 10 and C#.
- Use Blazor Web App with Interactive Server rendering.
- Use Tailwind CSS and the shared component strategy.
- Use the existing `CanDoItAll.Components` library where possible and add missing shell/workbench components in the same style.
- Keep code comments in English.
- Preserve the modular monolith boundaries from the architecture package.
- Prefer one `DbContext` per operation via `IDbContextFactory`.
- Keep business logic out of page-only code.
- Do not log or expose secrets.
- Keep tab restore state behind an explicit browser-storage abstraction.
- Wrap the existing canvas and calendar JavaScript engines first; do not rewrite them in pure C# in version one.
- Treat the JavaScript engines as rendering and interaction adapters only; keep business logic, persistence, validation, and command semantics in C#.
- Implement the grouped hexagonal context menu pattern as the standard canvas action launcher.
- Do not satisfy the canvas/calendar scope with placeholder card-list rendering.
- Add or update tests for the touched behavior.

## Scope
This prompt covers the workbench architecture that sits between projects/resources and later prompt-factory or validation flows.

## Tasks
1. Implement the workbench or tab-session domain: tab identity, tab kind, open/background/sleep lifecycle, restore snapshots, and pinned tabs.
2. Implement tab host services, registry, persistence store, and local-storage-backed restore flow.
3. Build the shell-level tab strip, tab actions, restore UX, and dirty or sleeping indicators.
4. Implement artifact-aware internal tab kinds for opened projects, prompt sessions, validations, and test artifacts.
5. Implement a project structure canvas wrapper using the documented JS engine, typed .NET DTOs, and Blazor-owned inspector or outline surfaces.
6. Implement a project events calendar wrapper using the documented JS engine and typed .NET DTOs.
7. Ensure both workbench surfaces open inside internal tabs instead of forcing browser-tab workflows.
8. Add project artifact linking so calendar items and structure nodes can open related prompts, resources, validations, and tests inside the tab workspace.
9. Implement the grouped hexagonal context menu for node actions and route every command into typed C# workbench handlers.
10. Support direct creation and linking of project-object graph items from the structure workbench.
11. Ensure workbench surfaces expose stable tab, capsule, and selection metadata needed by development tuning mode.
12. Add tests for tab persistence, restore, sleep or wake behavior, artifact-tab semantics, canvas command routing, and the JS interop wrappers' main round-trip contracts.

## Required deliverables
- tab workspace domain and services
- tab strip and shell integration
- browser-storage-backed restore path
- project structure canvas wrapper
- project events calendar wrapper
- artifact-aware workbench tabs
- project-object authoring baseline
- artifact linking into internal tabs
- automated tests

## Acceptance criteria
- the user can open, close, reorder, and reactivate internal tabs
- opened projects and prompt sessions behave as meaningful internal work items
- tab state survives refresh or crash through local storage
- heavy tabs can transition into a sleeping state and recover safely
- the project structure canvas works through a real wrapper-first JS interop approach around the documented engine
- the events calendar works through a real wrapper-first JS interop approach around the documented engine
- prompts, resources, validations, and tests can be opened from the workbench surfaces inside internal tabs
- the grouped hexagonal context menu is available on the structure canvas and its actions are executed through C# workbench services
- the workbench can create and connect project-object graph items directly instead of only visualizing them
- workbench surfaces publish stable metadata for manager-driven tuning and verification flows
- tests cover the critical restore and wrapper contracts

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the application has a credible internal workbench model and the visual orchestration surfaces are real, not placeholder renderings.
