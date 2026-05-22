# Implementation Prompt

Implement the Cognitive Memory runtime usage toggle with the smallest correct change set.

Hard constraints:

- Add a strongly typed persisted setting with default enabled.
- Keep enabled-mode Cognitive Memory failures explicit.
- Disabled mode must skip optional integrations before project-scope or downstream memory calls.
- Preserve direct settings/status/database management access.
- Add/update targeted tests and migrations.
- Record proof in `reviews/01-execution-report.md` and `proof/SBxx/`.

Stop if migrations cannot be generated or if a disabled integration still calls recall, ingestion, consolidation, or proposal scanning.
