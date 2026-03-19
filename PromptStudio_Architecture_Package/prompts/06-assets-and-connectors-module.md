# Codex Prompt 06 — Resources and Connectors Module

## Objective
Implement the generalized resource model, descriptor registry, typed editors, connector profile reuse, sensitivity handling, and resource detail UX.

## Required reading
1. `docs/01-ux-discovery.md`
2. `docs/03-ui-architecture-and-ascii-layouts.md`
3. `docs/04-solution-architecture.md`
4. `docs/06-architecture-review-gap-analysis.md`
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
This prompt covers M4: project-linked resources for all required kinds, status tracking, secret references, and typed resource editors.

## Tasks
1. Implement the `ProjectResource` model and supporting persistence.
2. Implement the resource descriptor registry and registration pattern.
3. Implement add/edit/detail flows for required resource kinds:
   - folder
   - file
   - web link
   - FTP
   - PowerShell script
   - repository
   - Docker / Docker Compose
   - SSH
   - secret link
   - prompt link
4. Add validation status, preview/indexing flags, and sensitivity classification.
5. Add connector/profile reuse where applicable.
6. Build the Resources page with filters, badges, and a detail drawer.
7. Keep preview/indexing behavior capability-based; do not overbuild parsers.
8. Add component and integration tests for resource creation and editing.

## Required deliverables
- generalized resource persistence
- descriptor registry
- typed resource editors
- resource list/detail UI
- secret reference support
- validation/sensitivity indicators
- automated tests

## Acceptance criteria
- every required resource kind can be registered through the UI
- the descriptor model is used consistently
- secret references work for relevant connectors
- unsupported preview/indexing scenarios degrade gracefully
- resources show status and sensitivity clearly
- tests cover the main resource flows

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the application can reliably manage every requested resource type at a metadata/registration level.