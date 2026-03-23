# Codex Prompt 12 — Hardening, Activity, Search, Observability, and Packaging

## Objective
Implement the remaining hardening work: activity timeline, search document indexing, background job visibility, observability, and release-ready cleanup.

## Required reading
1. `docs/04-solution-architecture.md`
2. `docs/05-requirement-coverage-matrix.md`
3. `docs/07-implementation-plan.md`
4. `docs/08-checklists.md`
5. `docs/09-validation-and-testing-plan.md`
6. `docs/10-executive-qa-review.md`
7. `docs/03a-workbench-tabs-canvas-and-state.md`
8. `docs/03b-development-manager-watch-capsules-and-tuning.md`

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
This prompt covers M9 and the release-hardening work needed before calling the implementation internally ready.

## Tasks
1. Implement the Activity module and a usable activity timeline.
2. Implement search document indexing and basic cross-entity search.
3. Implement background job records and a UI for background task visibility.
4. Review observability: logs, health indicators, diagnostics, restore diagnostics, and safe error messaging.
5. Harden workbench restore, sleeping-tab behavior, and snapshot versioning.
6. Harden the manager loop: watch-state transitions, readiness semantics, capsule drift reporting, and tuning-request history.
7. Re-run the checklists and close the most important UX and safety gaps.
8. Improve packaging/startup/readme guidance for local usage, including manager startup.
9. Add or refine tests for activity/search/job visibility, workbench-hardening flows, and manager-hardening flows.

## Required deliverables
- Activity module
- search abstraction and relational implementation
- background job visibility
- observability refinements
- packaging/startup cleanup
- final tests and docs updates

## Acceptance criteria
- major business actions appear in the activity timeline
- users can search key projects/prompts/resources
- background jobs are visible and diagnosable
- observability is useful without exposing secrets
- release checklists can be passed against the current state
- touched tests pass
- the manager loop is trustworthy under build failure, crash, and recovery conditions

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the solution is internally beta-ready and the remaining work is mostly polish or future expansion.
