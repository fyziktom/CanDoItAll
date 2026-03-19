# Codex Prompt Template

Use this template for any additional implementation session.

## Objective
State the exact feature or milestone to implement.

## Required reading
1. `README.md`
2. relevant documents under `docs/`
3. related earlier prompt outputs

## Operating constraints
- Use .NET 10 and C#.
- Keep code comments in English.
- Keep the modular monolith boundaries intact.
- Prefer one `DbContext` per operation through `IDbContextFactory`.
- Do not put business logic directly into UI pages.
- Use existing shared components first; build missing reusable components in `CanDoItAll.ComponentKit`.
- Do not log secrets or raw sensitive values.
- Keep dangerous actions behind explicit approval gates.
- Add or update automated tests.
- Do not leave placeholder TODO comments unless they are explicitly justified in the final summary.

## Workflow
1. Read the relevant architecture documents.
2. Restate the implementation scope briefly.
3. List the files you expect to create or modify.
4. Implement the feature in small coherent slices.
5. Add or update tests.
6. Run the appropriate test commands.
7. Summarize what was completed, what remains, and any risks.

## Required output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test commands executed
5. Completion summary
6. Remaining risks or follow-up items

## Quality bar
The result is acceptable only if:
- it compiles,
- tests pass for the touched area,
- the UI is not placeholder-only,
- naming and structure follow the architecture package,
- no new secret leakage risks are introduced.
