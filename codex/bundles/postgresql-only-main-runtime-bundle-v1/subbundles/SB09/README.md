# SB09 - Final Validation, Documentation, CI, and Anti-Stub Audit

## Objective

Prove the repository is PostgreSQL-only for main runtime and no SQLite residue remains in main app paths.

## Required audits

```powershell
rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|sqlitewritecoordination|legacysqlitemigrationbootstrap|snapshotcache|ipfssnapshot" src tests docs
rg -n -i "Microsoft\.Data\.Sqlite|Microsoft\.EntityFrameworkCore\.Sqlite" **/*.csproj
dotnet build .\CanDoItAll.slnx
dotnet test .\CanDoItAll.slnx --filter "Category!=Browser&Category!=LiveProcess"
```

Remaining SQLite matches are allowed only if:

- They are outside main CanDoItAll runtime, or
- They are documentation explaining removed/deferred functionality, or
- They are explicit unsupported legacy error messages.

Document every allowed match.

## Browser proof

Validate:

- Workspace/Data Sources has no SQLite create/open actions.
- PostgreSQL flow works.
- Current profile display works.
- Unsupported old SQLite profiles show clear guidance if encountered.

## Anti-stub audit

Check:

- No TODO-only replacement.
- No empty methods replacing real behavior.
- No catch-all exception swallowing.
- No `if sqlite then return` hidden branch.
- No tests deleted without replacement where behavior still matters.
- No hidden fallback to `InMemory`.

## Required final report

Create:

```text
reviews/01-execution-report.md
```

It must include:

- Summary of completed subbundles.
- Build/test/browser/migration proof links.
- Remaining risks.
- Manual real database guidance.
- Confirmation that CanDoItAll.IPFS was not modified.
- Confirmation that PostgreSQL is the only main persistent runtime provider.
- Confirmation that snapshots were removed/deferred.
