# Assumptions And Risks

## Assumptions

- The existing database profile infrastructure is the correct source of truth for PostgreSQL runtime selection.
- Cognitive Memory automation settings can be persisted independently first; deeper unattended scheduling can build on the stored contract without blocking manual testing.
- File and URL ingestion should create normal Cognitive Memory source/evidence records so downstream consolidation remains unchanged.
- Bundle sample data may live under this follow-up bundle as documents and mermaid mindmaps because it is validation fixture content, not automated test code.

## Critical Path Risks

- If the database setup API does not reuse the profile runtime correctly, the live app and Visual Studio may diverge.
- If external ingestion bypasses source/evidence records, later recall/consolidation tests will not exercise the real memory architecture.
- If UI logic grows directly in Razor, the page will become harder to maintain.

## Validation Risks

- PostgreSQL availability is environment-dependent.
- Browser proof depends on successful app startup and a reachable Cognitive Memory route.
- Migration changes must be validated without leaning on SQLite.

## Reopen Triggers

- Reopen subbundle 01 if API database setup cannot create or switch the requested PostgreSQL profile.
- Reopen subbundle 02 if the UI cannot save settings or initiate ingestion.
- Reopen subbundle 03 if sample data is loaded directly into the database instead of through APIs.
