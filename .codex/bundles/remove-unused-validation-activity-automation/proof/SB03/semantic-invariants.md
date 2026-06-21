# SB03 Semantic Invariant Contract

## Invariant ID

- Invariant ID: `RM-003`

## Source raw note

- Remove unused Validation, Activity, and Automation modules and related tests, including hidden product connections such as project-structure menus.

## Expected behavior

- The three module projects are deleted, not compiled, not registered, and not exposed in active UI or tests.

## Disallowed shallow implementation

- Removing navigation labels while leaving project references, DI registration, Workbench actions, or obsolete tests.

## Failing-first test

- failing-first: N/A - process/non-production deletion audit; no new production behavior fixture was introduced.

## Passing test

- `proof/SB03/transcripts/active-reference-audit.txt`
- `proof/SB03/transcripts/deleted-paths.txt`
- `proof/SB04/transcripts/test-components-targeted.txt`
- `proof/SB04/transcripts/test-unit-targeted.txt`
- `proof/SB04/transcripts/test-integration-service-targeted.txt`

## Changed source files

- `CanDoItAll.slnx`
- `src/CanDoItAll.Composition/*`
- `src/CanDoItAll.Web/*`
- `src/CanDoItAll.Modules.Workbench/*`
- `src/CanDoItAll.Migrations.PostgreSql/Migrations/20260621212712_RemoveUnusedValidationActivityAutomationModules.cs`
- `tests/*`
- `tools/CanDoItAll.ScenarioSeeder/*`

## Production assertions

- `src/CanDoItAll.Modules.Activity`, `src/CanDoItAll.Modules.Automation`, and `src/CanDoItAll.Modules.Validation` are absent.
- Active reference audit finds no old module namespaces, services, project references, or `add-validation-run`.
- EF migration drops the old module tables from the current PostgreSQL model.

## Red-team negative case

- The audit fails if an old module namespace, registration, project reference, or Workbench create action returns.

## Downstream dependency check

- SB04 verified the rebuilt app and Browser shell after the deletion.
