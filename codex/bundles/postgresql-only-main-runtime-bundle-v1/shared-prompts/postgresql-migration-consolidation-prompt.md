# PostgreSQL migration consolidation prompt

Use only after SB01-SB07 are complete and validated.

Goal:

Consolidate PostgreSQL migrations into one baseline migration.

Steps:

1. Confirm no SQLite runtime/profile/snapshot branches remain.
2. Confirm build passes.
3. Capture current PostgreSQL migration list.
4. Remove old PostgreSQL migration files only after confirming the model is stable.
5. Generate one new baseline migration for PostgreSQL.
6. Validate fresh DB creation.
7. Validate app startup against fresh DB.
8. Validate representative persistence flows.
9. Write `manual-real-db-alignment.md`.

Do not claim the user's real DB is automatically migrated unless a tested transition script exists.
