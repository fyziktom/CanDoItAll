# Implementation Prompt

Implement the selected subbundle only. Preserve the user's literal `all projects` scope unless the bundle has been repaired with an explicit exception.

Use existing CanDoItAll patterns:

- `IDatabaseTransferHandler` for database-to-database transfer
- scoped services in `WorkbenchModuleServiceCollectionExtensions`
- BaseLib operational UI controls and existing Projects board styling
- targeted integration/component tests over broad unrelated rewrites

Keep IDs stable across transfer/import. Clear target project-scope records in dependency order. Do not copy ProjectStructure leases or analytics unless implementation proves they are user-facing project content.

After changes, update the subbundle README status, `reviews/01-execution-report.md`, raw note closure rows, and browser validation analytics before moving on.
