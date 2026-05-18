# Implementation Prompt

Implement the current subbundle only. Use the processes page as the visual reference, but move policy into BaseLib shared primitives:

- Compact stat badges must use shared tooltip behavior with a 2-second delay.
- Header actions migrated by this bundle must be icon-only, accessible, and tooltip-backed.
- Prefer `PageHeader` stats/actions and `CompactStatStrip` over page-local stat-card markup.
- Preserve data loading and workflow behavior; this is a layout/density migration.
- After each phase, update `reviews/01-execution-report.md` with build, browser, screenshot, and gate status.
