# Assumptions And Risks

## Assumptions

- The development database may be dropped and recreated because the user explicitly requested a clean development PostgreSQL database.
- Existing demo flows should continue to run when Cognitive Memory is disabled even if they do not provide a project scope.
- A runtime database setting is preferred over appsettings because no restart should be required.

## Critical Path Risks

- If the setting is modeled only as `ModelAccessMode.Disabled`, governed agent runs can still fail because the contributor treats some memory failures as required context failures.
- If the new EF column is added without both PostgreSQL and SQLite migrations, development and test databases will drift.
- If workflow executor skips happen after parsing executor settings, disabled workflows with incomplete Cognitive Memory node settings can still fail.

## Validation Risks

- Full browser proof may be expensive if the local app cannot be started after the database reset; component tests and build proof are the fallback for UI contract validation.
- The codebase has many direct Cognitive Memory management APIs. The toggle scope must stay clear: optional cross-feature integrations are bypassed, settings/status remain available.
- If tests use in-memory settings helpers, constructor changes can create broad compile failures until test helpers are updated.

## Reopen Triggers

- Reopen SB01 if migrations fail, settings do not persist, or API/UI cannot round-trip the new flag.
- Reopen SB02 if any optional integration can still call recall/ingestion/consolidation while disabled.
- Reopen SB03 if clean PostgreSQL reset cannot apply migrations or leaves the app pointing at stale data.
