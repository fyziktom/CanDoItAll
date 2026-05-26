# Target Solution

## UI And Domain Contract

- Introduce a small strongly typed executor-kind option catalog for role editor values so UI choices preserve persisted strings such as `person`, `agent`, `person-or-agent`, and workflow.
- Keep role executor persistence as string because the existing model and templates intentionally support external vocabulary beyond the simple `ProcessExecutorKind` enum.
- Extend domain enums where the template vocabulary is a first-class process concept:
  - `ProcessResponsibilityKind.Accountable`
  - `ProcessArtifactKind.DecisionRecord`
  - `ProcessArtifactTrustRequirement.ApprovalRequired`
- Update priority, canvas port/label, and trust-satisfaction logic so new enum values are not stranded UI-only options.
- Add tests that prove both positive rendering and negative drift detection.

## Development Database Reset

- Use direct PostgreSQL SQL against the configured development database.
- Generate the truncate target list from `information_schema.tables` where `table_schema = 'public'` and `table_name LIKE 'Processes\_%'`.
- Execute `TRUNCATE TABLE ... RESTART IDENTITY CASCADE` only for the generated process table list.
- Capture representative preservation counts for non-process tables before and after.
- Reload default process templates through application services or API after code changes build.

## Boundaries

- Do not change agent/plugin/memory/project schemas.
- Do not drop or recreate the database.
- Do not delete managed files or workspace directories.
- Do not add new UI layout patterns when existing shared components already cover the form structure.
