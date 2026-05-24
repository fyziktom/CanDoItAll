# Copy-paste prompt for Codex

You are a senior C#/.NET architect working in repository `fyziktom/CanDoItAll`.

Branch:
- Continue from `db-remove-sqlite`.
- Do not work in `CanDoItAll.IPFS`; that repository is out of scope.

Goal:
Codex already performed the first SQLite removal pass. Now finish the cleanup so the main CanDoItAll runtime is truly PostgreSQL-only. Do not leave SQLite as an enum value, UI branch, legacy profile mode, startup branch, or test provider. Snapshot support can be reintroduced later and should not remain as SQLite-related runtime surface.

Read and execute this follow-up bundle in order:
1. SB01 hard-remove SQLite domain model and add legacy catalog quarantine.
2. SB02 clean Data Sources UI.
3. SB03 remove snapshot runtime stubs or isolate them as future-work documentation.
4. SB04 add hard tests and residue audit.
5. SB05 prove PostgreSQL baseline migration has no model drift.
6. SB06 tune process/workflow/automation runtime for PostgreSQL concurrency.
7. SB07 remove or justify unrelated branch artifacts.
8. SB08 run final validation.

Critical observations:
- `DatabaseProfileModels.cs` still contains `DatabaseProviderKind.Sqlite`, SQLite source kinds, `SqliteDatabaseProfileConnection`, and `SqliteDatabasePath`.
- `DatabaseProfileControlPlaneService.cs` still contains SQLite validation/rejection/descriptor/fingerprint branches.
- `DatabaseProfileStartupConnectionResolver.cs` still throws on legacy SQLite active profile. This can brick startup before UI remediation.
- `DatabaseSourcesSettingsPanel.razor` still has a `DatabaseProviderKind.Sqlite` UI branch and a snapshot-deferred section.
- `DatabaseSnapshots.cs` still contains deferred snapshot runtime service/models.
- Process/workflow PostgreSQL-specific tuning is not proven by the first pass and must be audited and implemented after model cleanup.

Hard requirements:
- Remove SQLite enum/model/UI/test references from main runtime.
- Do not merely replace SQLite support with "unsupported legacy SQLite" branches in the typed runtime model.
- Implement raw JSON legacy catalog quarantine so old SQLite catalog entries do not brick startup.
- Keep InMemory only if deliberately test/dev override; do not expose it as a normal persisted runtime profile unless explicitly justified.
- Add residue audit scripts/tests.
- Verify one PostgreSQL baseline migration and no EF model drift.
- Add PostgreSQL-backed concurrency tests for workflow/process/outbox claim paths.
- Keep comments in code in English.

Validation commands:
```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx -m:1 -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Browser&Category!=LiveProcess" -v:minimal
rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|snapshotcache|ipfssnapshot|sqlitewritecoordination" src tests *.slnx
```

Final output:
- update `codex/bundles/postgresql-only-main-runtime-followup-v1/reviews/01-execution-report.md`,
- include proof logs under `codex/bundles/postgresql-only-main-runtime-followup-v1/evidence/`,
- clearly state any intentionally allowed residues.
