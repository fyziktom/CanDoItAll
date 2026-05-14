# Normalized Requirements

| Id | Requirement | Acceptance |
| --- | --- | --- |
| R001 | Inspect EF Core usage in current DB-backed services. | Current-state analysis identifies concrete query-smell categories and source files. |
| R002 | Repair high-confidence query-shape trouble. | `.ToListAsync()` before order/filter/take is replaced with server-side order/filter/take where safe. |
| R003 | Use no-tracking for read-only EF queries where safe. | Read-only DTO/list methods add `AsNoTracking()` without affecting write paths. |
| R004 | Preserve behavior and architecture. | Public contracts, persistence profiles, migrations, and user-visible ordering semantics remain intact. |
| R005 | Validate and close the bundle. | Targeted tests/build pass or any blocker is recorded with exact command output. |

