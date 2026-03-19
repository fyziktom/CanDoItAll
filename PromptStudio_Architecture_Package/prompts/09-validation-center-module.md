# Codex Prompt 09 — Validation Center Module

## Objective
Implement validation runs, checklists, findings, review decisions, and the validation center UI for stories, layouts, architecture, plans, and prototype checks.

## Required reading
1. `docs/01-ux-discovery.md`
2. `docs/03-ui-architecture-and-ascii-layouts.md`
3. `docs/04-solution-architecture.md`
4. `docs/07-implementation-plan.md`
5. `docs/08-checklists.md`
6. `docs/09-validation-and-testing-plan.md`

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
This prompt covers M7: rule-first validation infrastructure plus a usable Validation Center UI.

## Tasks
1. Implement the Validation module domain and persistence.
2. Implement checklist storage/versioning.
3. Implement a generic validation run/result model with findings.
4. Implement initial validation types for:
   - stories/use cases
   - ASCII layouts
   - architecture
   - implementation plans
   - prototype checks
   - test coverage plans
5. Build Validation Center pages and result detail views.
6. Add decision actions such as approve, reject, needs changes, or follow-up required.
7. Keep AI-assisted review optional and clearly separate from deterministic rules.
8. Add tests for validation runs, findings persistence, and key UI flows.

## Required deliverables
- Validation module domain
- checklist model
- validation run/finding model
- Validation Center UI
- initial review strategies
- tests

## Acceptance criteria
- validation runs can be created, stored, reopened, and reviewed
- findings have severity and action data
- validation types share one coherent storage/result model
- validation UI links back to project artifacts clearly
- deterministic validation remains primary
- tests pass for core validation behaviors

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the Validation Center is a real working review surface, not a placeholder.