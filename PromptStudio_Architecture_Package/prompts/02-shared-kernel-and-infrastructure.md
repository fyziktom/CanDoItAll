# Codex Prompt 02 — Shared Kernel and Cross-Cutting Infrastructure

## Objective
Implement the reusable core primitives and cross-cutting infrastructure foundations that all modules depend on.

## Required reading
1. `docs/02-technical-requirements.md`
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
This prompt covers the SharedKernel and Infrastructure baseline that should exist before feature modules deepen.

## Tasks
1. Implement shared primitives such as result types, error models, typed identifiers if appropriate, time/provider abstractions, and guard helpers.
2. Implement common options classes and validation where the architecture expects them.
3. Create infrastructure abstractions for file storage, workspace path resolution, event dispatch plumbing, and background job queue contracts.
4. Add health check and logging/redaction foundations.
5. Add a safe serialization utility layer if needed.
6. Ensure all these primitives are minimal and not bloated.
7. Add tests for shared primitives and infrastructure helpers.

## Required deliverables
- reusable shared primitives
- redaction-aware logging helpers
- background queue contracts
- file/workspace abstraction interfaces
- validated options baseline
- tests for the shared/core layer

## Acceptance criteria
- SharedKernel remains small and cohesive
- Infrastructure contains cross-cutting concerns only
- secret-safe logging helpers exist
- options validation is working where introduced
- the background queue abstraction is ready for later workers
- tests prove the main primitives behave correctly

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the shared/core layers are stable and feature modules can build on top of them without rework.