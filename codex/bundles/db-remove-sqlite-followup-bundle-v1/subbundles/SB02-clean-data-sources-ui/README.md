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

## Status

- Completed

## Objective

Make the Data Sources panel render only the supported PostgreSQL runtime path and explicit development/test override state.

## Covered Inputs

- `bundle://requirements/01-followup-requirements.md`

## Prerequisites

- SB01 provider model cleanup completed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor`
- `repo://tests/CanDoItAll.Tests.Components/SettingsPageDataSourcesTests.cs`

## Deliverables

- Retired-provider UI branch removal.
- Snapshot action removal.
- Data Sources component/browser regression tests.

## Dependency Impact

- The UI depends on control-plane quarantine to prevent unsupported legacy profiles from reaching the typed listing path.

## Validation Depth

- Component tests and Playwright browser proof.

## Implementation Steps

- Remove retired provider forms and labels.
- Remove snapshot controls.
- Assert absence through component and browser tests.

## Do Not Do

- Do not expose persisted InMemory profile creation in the product UI.

## Acceptance Checklist

- Data Sources shows PostgreSQL-focused controls.
- Retired provider text/test IDs are absent.
- Snapshot controls are absent.

## Proof Required

- `bundle://proof/SB02/manifest.md`

## Browser Validation Logging

- Playwright evidence is recorded under `bundle://evidence/SB02`.

## Progression Gate

- Data Sources component and browser proof must pass before SB08.

## Suggested Agent Prompt

Implement SB02, then run the Data Sources component and Playwright proof commands in `proof/SB02/manifest.md`.
