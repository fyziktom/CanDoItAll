# Database source setup API and PostgreSQL runtime alignment

## Status

- `Completed`

## Objective

- Add Cognitive Memory-scoped database setup APIs and align the runtime path so PostgreSQL is the primary development and validation database.

## Success Criteria

- Cognitive Memory API exposes current database selection, PostgreSQL profile setup, and profile switching.
- Cognitive Memory status/route inventory lists the new database setup routes.
- Visual Studio launch settings are prepared for the same PostgreSQL database used by validation.
- Focused tests or API smoke proof confirm the routes are reachable.

## Covered Inputs

- R1 PostgreSQL-first runtime.
- R2 Cognitive Memory database setup API.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Properties\launchSettings.json`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ApiIntegrationTests.cs`

## Deliverables

- Database setup request/response contracts in the Cognitive Memory API.
- Route handlers for database selection, PostgreSQL profile creation, and profile switching.
- Updated route inventory.
- Launch settings configured for the validation PostgreSQL database.

## Dependency Impact

- Subbundles 02 and 03 depend on a reliable PostgreSQL runtime. Weak proof here would make UI and sample-data validation ambiguous because the app might run against a different database.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Inspect existing database profile services and dev endpoints.
2. Add Cognitive Memory database setup route handlers that reuse the existing services.
3. Update status route inventory.
4. Add or update focused tests/API proof.
5. Update launch settings for the follow-up PostgreSQL database.

## Scope Exceptions

- This phase does not load sample data or implement UI tabs.

## Do Not Do

- Do not add a separate database switching abstraction when existing profile services are sufficient.
- Do not make SQLite the validation path.

## Acceptance Checklist

- Completed: Database selection endpoint returns the active provider/profile.
- Completed: PostgreSQL profile endpoint accepts connection details and can switch the runtime profile.
- Completed: Cognitive Memory route inventory includes database setup routes.
- Completed: Launch settings reference `candoitall_cognitive_memory_followup_20260517_12`.

## Proof Required

- `dotnet build CanDoItAll.slnx --no-restore`
- Integration route proof through `ApiIntegrationTests.Api_openapi_exposes_focused_control_plane_routes`.
- API smoke proof in `validation/evidence/20260517-115640/01-database-selection.json` and `02-cognitive-memory-status.json`.

## Browser Validation Logging

- N/A

## Progression Gate

- Downstream work may continue only after the Cognitive Memory database setup API exists and the planned PostgreSQL database is the configured runtime target.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
