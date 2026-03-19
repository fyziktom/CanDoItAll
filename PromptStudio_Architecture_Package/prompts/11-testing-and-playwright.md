# Codex Prompt 11 — Testing Infrastructure, Test Lab, and Playwright Coverage

## Objective
Implement the test lab domain/UI and strengthen automated testing across unit, integration, component, and Playwright layers.

## Required reading
1. `docs/03-ui-architecture-and-ascii-layouts.md`
2. `docs/03a-workbench-tabs-canvas-and-state.md`
3. `docs/03b-development-manager-watch-capsules-and-tuning.md`
4. `docs/07-implementation-plan.md`
5. `docs/08-checklists.md`
6. `docs/09-validation-and-testing-plan.md`
7. `docs/11-references.md`

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
This prompt covers M8 and the broader test infrastructure expectations for the product.

## Tasks
1. Implement the TestLab module domain and persistence.
2. Build pages for test plans, linked test cases, evidence artifacts, and results.
3. Support screenshot/evidence metadata and linkage to project stories/features/phases.
4. Add or refine unit, integration, and component tests for the already implemented modules.
5. Set up or refine Playwright project configuration and initial end-to-end suites for primary flows, including internal tab restore, workbench surfaces, and manager-ready waits.
6. Add automated coverage for manager watch-state normalization, runtime readiness confirmation, capsule generation, and dev-only tuning request flows using fake or controlled adapters where required.
7. Ensure test artifacts and reports can be associated with the Test Lab concept.
8. Add documentation or scripts for running the full test stack.

## Required deliverables
- TestLab domain and UI
- screenshot/evidence records
- linked test plans/cases/results
- improved automated test coverage
- Playwright baseline suites and config
- test-run documentation/scripts

## Acceptance criteria
- test plans and evidence can be managed in the application
- primary workflows have Playwright coverage
- automated tests exist across all intended layers
- failing tests produce actionable artifacts
- test lab pages are linked coherently to projects and validations
- the manager loop can be validated without relying on arbitrary sleeps

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the application has a credible quality system, not just scattered tests.
