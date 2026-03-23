# Codex Prompt 07 — Prompt Library, Gallery, Versioning, and Usage

## Objective
Implement the prompt management domain: drafts, versions, collections, tags, search filters, clone flow, and usage history.

## Required reading
1. `docs/01-ux-discovery.md`
2. `docs/03-ui-architecture-and-ascii-layouts.md`
3. `docs/04-solution-architecture.md`
4. `docs/07-implementation-plan.md`
5. `docs/09-validation-and-testing-plan.md`

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
This prompt covers M5: prompt library management before the advanced factory workflow is layered on top.

## Tasks
1. Implement the Prompts module aggregates and persistence.
2. Implement prompt draft editing and saving.
3. Implement final/version creation rules.
4. Implement prompt collections/galleries.
5. Implement tags and filter/search support at the UI level.
6. Implement usage history with project/phase/provider/repository/commit metadata.
7. Implement clone and reuse flows.
8. Build Prompt Gallery pages and detail views.
9. Add tests for prompt versioning, clone behavior, and usage records.

## Required deliverables
- prompt domain and persistence
- prompt gallery UI
- version history support
- collections and tags
- usage history
- clone flow
- tests

## Acceptance criteria
- draft and final prompts behave differently and correctly
- version history is immutable
- collections and tags are usable from the UI
- usage can be recorded with repository context
- prompt clone flow works
- tests prove versioning and usage behavior

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the prompt library can be used productively even without the factory.