# SB02 - Clean Data Sources UI

## Goal

Make the Data Sources UI PostgreSQL-focused and remove UI references to SQLite legacy profiles.

## Context

The current branch removed new SQLite actions but kept a legacy SQLite form branch and a snapshot-deferred section.

## Required changes

1. Remove `DatabaseProviderKind.Sqlite` UI branch from `DatabaseSourcesSettingsPanel.razor`.
2. Remove "Unsupported legacy profile" rendering.
3. Remove any use of `SqliteDatabasePath`.
4. Remove snapshot-deferred UI section unless product explicitly wants a documentation notice.
5. Remove persisted InMemory editor UI unless retained as development-only.
6. Update labels, test IDs, component tests, and documentation.
7. Ensure profile list cannot display unsupported legacy profiles because SB01 should quarantine them before typed listing.

## Validation

- Component tests verify only PostgreSQL profile creation/editing is visible.
- Component tests verify no SQLite text/test-id is rendered.
- Browser/Playwright proof opens Data Sources page from a clean PostgreSQL profile and from a quarantined legacy catalog scenario.

## Proof artifacts

Write:

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- relevant logs under `evidence/SB02/`

## Acceptance criteria

- Data Sources UI has no SQLite branch.
- No SQLite-specific text appears in runtime UI.
- UI remains usable when legacy catalog was quarantined.
