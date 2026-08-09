# A03 validation

## Focused commands

```text
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~Storage|FullyQualifiedName~ControlPlane|FullyQualifiedName~DatabaseProfile|FullyQualifiedName~FileApplicationPreference|FullyQualifiedName~MigrationBackupIntegrity'
```
```text
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=StorageMigration'
```
```text
dotnet build ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release
```

## Required proof

- Failing-first or characterization result.
- Focused unit/integration/actual-host result.
- Stable Windows regression result.
- Linux/macOS result when the subbundle changes platform behavior.
- Migration/rollback/failure-injection result where applicable.
- Redaction scan.
- Source/reference/requirement update.
- Independent review required by the active gate.

## Failure handling

Do not skip, quarantine, weaken policy, or broaden the allowlist to obtain green tests. Classify the failure, update the finding/requirement, and invoke the named correction/recovery path when a foundational invariant fails.

## Executed proof

| Host | Validation | Result |
|---|---|---:|
| Windows | Full Unit Release regression | 5,499/5,499 |
| Windows | Required A03 Release unit filter | 275/275 |
| Windows | `Category=StorageMigration` integration, including PostgreSQL | 3/3 |
| Windows | Web Release build | 0 warnings/errors |
| Linux Docker | Required A03 Release unit filter excluding only `RequiresHostDocker=true` | 273/273 |
| Linux Docker | Portable `Category=StorageMigration` integration | 1/1 |
| Linux Docker | Web Release build | 0 warnings/errors |
| Windows/Linux | Filesystem atomicity preservation | 27/27 per host |

The authoritative artifact paths, failure classifications, architecture proof, static audit, redaction result, and residuals are in `../../reviews/11-a03-evidence-report.md`.
