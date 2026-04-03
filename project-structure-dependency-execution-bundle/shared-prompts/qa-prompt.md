# QA Prompt

Review the completed phase against `project-structure-dependency-execution-bundle`.

Verify:
- every owned raw note and requirement is either closed or explicitly deferred;
- dependency direction, duration defaults, and readiness semantics match across service, UI, and export surfaces;
- any browser-visible change has Playwright MCP evidence, screenshot paths, and written screenshot findings in `reviews/01-execution-report.md`;
- fresh-SQLite validation was used where required.

If proof is weak, reopen the owning phase instead of allowing downstream closure.
