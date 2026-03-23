# Codex Prompt 01 — Bootstrap Solution

## Objective
Create the initial solution structure and development foundation for CanDoItAll.

## Required reading
1. `README.md`
2. `docs/03a-workbench-tabs-canvas-and-state.md`
3. `docs/03b-development-manager-watch-capsules-and-tuning.md`
4. `docs/04-solution-architecture.md`
5. `docs/07-implementation-plan.md`
6. `docs/08-checklists.md`

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
This prompt covers the M0 foundation work: solution structure, project creation, shell, module registration pattern, ComponentKit baseline, Tailwind integration, and test-project baseline.

## Tasks
1. Create the solution and all planned projects under `src/` and `tests/`.
2. Create a clean module registration pattern so each module can expose an `Add...Module` extension.
3. Create `CanDoItAll.Web` with shell layout, internal tab host baseline, navigation, and placeholder routes for Dashboard, Projects, Prompt Gallery, Prompt Factory, Validation Center, Test Lab, and Settings.
4. Create `CanDoItAll.SharedKernel`, `CanDoItAll.Infrastructure`, `CanDoItAll.ComponentKit`, and the Workbench module baseline.
5. Add Tailwind configuration/build integration appropriate for the web project.
6. Add test projects for unit, integration, component, and Playwright layers.
7. Add the development-only hooks required for the later manager milestone, especially a clean place for a runtime readiness endpoint and dev-only tuning boundaries.
8. Add repo-level documentation for build/test/start commands.
9. Ensure the solution builds cleanly.

## Required deliverables
- working solution and project structure
- Blazor shell and route placeholders
- module registration extensions
- Tailwind baseline
- test project baseline
- developer setup notes

## Acceptance criteria
- solution builds from a clean restore
- shell navigation works
- the shell has an internal tab workspace baseline instead of assuming browser tabs
- all planned top-level routes exist
- module registration is discoverable and consistent
- Tailwind assets are included in the web host
- test projects are wired into the solution
- no module business logic is misplaced in the host
- the foundation is ready for the manager, capsule, and tuning loop defined in the architecture

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop once the foundation is implemented, builds cleanly, and is ready for the next prompt.
