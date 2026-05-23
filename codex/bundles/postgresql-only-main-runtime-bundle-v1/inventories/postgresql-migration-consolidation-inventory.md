# PostgreSQL migration consolidation inventory

Fill this during SB08.

## Before consolidation

- [ ] Current PostgreSQL migration files listed.
- [ ] Current model snapshot inspected.
- [ ] SQLite project removed.
- [ ] SQLite runtime/profile/snapshot branches removed.
- [ ] Build passes.
- [ ] Tests relevant to persistence pass.

## Consolidation target

- [ ] One baseline migration exists.
- [ ] One model snapshot exists.
- [ ] Fresh PostgreSQL DB can be created from zero.
- [ ] App starts against fresh DB.
- [ ] Representative process/workflow persistence path works.

## Real DB guidance

The user has one real PostgreSQL DB. Provide one of:

- manual schema alignment guide,
- tested migration script,
- dump/recreate/import plan,
- explicit statement that manual intervention is required.

Do not pretend automatic real DB migration is solved unless it is implemented and tested.
