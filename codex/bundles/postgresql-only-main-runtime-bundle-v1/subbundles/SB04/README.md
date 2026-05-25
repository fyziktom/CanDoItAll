# SB04 - Convert Tests and Test Support Away From SQLite

## Objective

Remove SQLite from main test support and convert persistence/integration tests to PostgreSQL-backed behavior.

## Inputs

Known files:

```text
tests/CanDoItAll.Tests.Support/TestDatabaseProviderKind.cs
tests/CanDoItAll.Tests.Support/TestDatabaseProfile.cs
tests/CanDoItAll.Tests.Support/CanDoItAllTestEnvironment.cs
tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
tests/**/*.cs
```

## Required changes

- Remove `TestDatabaseProviderKind.Sqlite`.
- Remove test helper methods that create SQLite profiles.
- Remove test cleanup that clears SQLite pools.
- Remove `Microsoft.Data.Sqlite` and EF SQLite package references unless a non-main-runtime test has a strong documented reason.
- Convert persistence integration tests to PostgreSQL.
- Keep pure unit tests narrow and do not use them as a replacement for PostgreSQL integration tests.

## PostgreSQL test approach

Prefer, in order:

1. Existing repository PostgreSQL test fixture if available.
2. Testcontainers PostgreSQL fixture if already used or acceptable.
3. Configured local PostgreSQL test profile with explicit skip/failure policy.

Do not silently downgrade to `InMemory`.

## Validation

```powershell
dotnet test .\CanDoItAll.slnx --filter "Category!=Browser&Category!=LiveProcess"
rg -n -i "TestDatabaseProviderKind\.Sqlite|CreateManagedSqliteProfile|Microsoft\.Data\.Sqlite|SqliteConnection|SqliteConnection\.ClearAllPools" tests
```

## Required proof

```text
proof/SB04/manifest.md
proof/SB04/semantic-invariants.md
evidence/SB04/test-audit.log
evidence/SB04/test-results.log
```
