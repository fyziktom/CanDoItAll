# Structured Input

## Raw Notes

| Id | Raw wording | Normalized ask | Scope |
| --- | --- | --- | --- |
| N001 | "analyze our work with db" | Inspect current EF Core usage in source-backed services. | `src/` and targeted tests |
| N002 | "repair if we have some troubles there" | Patch concrete, low-risk EF query problems found during the audit. | Existing code only |
| N003 | "Use candoitall-bundle-workflow" | Keep the work bundle-backed with validation and closure evidence. | `.codex/bundles/db-ef-query-repair` |
| N004 | "Use optimizing-ef-core-queries" | Apply EF Core query-shape guidance: avoid N+1, no-tracking for reads, push filtering/order/take to SQL. | EF-backed services |

## Hard Constraints

- Do not redesign `AppDbContext`, database profiles, migrations, or storage abstractions.
- Do not introduce silent fallback behavior.
- Do not touch unrelated dirty worktree files.
- Keep edits small and strongly typed.

