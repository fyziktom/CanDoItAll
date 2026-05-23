# SB03 - Remove SQLite UI and Dev Endpoints

## Objective

Remove user-visible SQLite profile actions and dev endpoints.

## Inputs

Known files:

```text
src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
src/CanDoItAll.Modules.Workspace/DatabaseProfileWorkspaceService.cs
src/CanDoItAll.Web/Program.cs
```

## Required UI removals

Remove:

```text
Managed SQLite
Open SQLite
SQLite source
SQLite file path
Materialized SQLite path
database-profile-new-managed
database-profile-new-external
database-profile-managed-sqlite-info
database-profile-sqlite-path
```

Replace onboarding copy with PostgreSQL-oriented flow.

## Required endpoint removals

Remove:

```text
/_dev/database/profiles/managed-sqlite
```

Remove any helper that creates `DatabaseProviderKind.Sqlite` from Web/Program or dev endpoints.

## Browser proof

Use Playwright or existing browser validation to prove:

- SQLite actions are absent.
- PostgreSQL profile creation/selection remains visible and functional.
- Current profile display works.
- Empty state does not reference SQLite.

## Validation

```powershell
rg -n -i "managed sqlite|open sqlite|sqlite source|sqlite file|database-profile-new-managed|managed-sqlite|/_dev/database/profiles/managed-sqlite" src tests
dotnet build .\CanDoItAll.slnx
```

## Required proof

```text
proof/SB03/manifest.md
proof/SB03/semantic-invariants.md
evidence/SB03/ui-audit.log
evidence/SB03/browser-proof.md
```
