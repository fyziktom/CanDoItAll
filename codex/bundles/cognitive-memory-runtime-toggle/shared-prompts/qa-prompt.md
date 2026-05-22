# QA Prompt

Review the runtime toggle change as a bug fix for demo-blocking cross-feature failures.

Check:

- Raw notes `N001`-`N007` map to code and proof.
- `IsEnabled = false` skips agent context before project scope resolution.
- `IsEnabled = false` skips workflow memory executors before executor settings validation.
- `IsEnabled = false` skips scheduled automation before ingestion/consolidation/professor-anchor calls.
- `IsEnabled = true` keeps existing strict behavior.
- PostgreSQL and SQLite migrations match the entity model.
- UI/API can persist the setting at runtime.
- Development PostgreSQL reset proof exists.
