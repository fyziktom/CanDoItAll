# PostgreSQL-Only Runtime Notes

## Scope

The current branch removed SQLite. This bundle must not reintroduce SQLite support or compatibility branches.

## Required Behavior

- EF migrations, if needed, must be PostgreSQL migrations only.
- Tests should use the repository's current PostgreSQL integration-test conventions.
- Concurrency and claim behavior should use existing PostgreSQL-compatible EF patterns.
- Do not add SQLite provider checks, SQLite snapshots, SQLite migration projects, or SQLite-specific test baselines.

## Validation

At final closure, Codex must run or explicitly block on:

```powershell
dotnet build CanDoItAll.slnx --no-restore
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"
```

If data model changes are made, also run the repository's PostgreSQL migration/model validation command and record the transcript under `proof/SB06/transcripts/`.
