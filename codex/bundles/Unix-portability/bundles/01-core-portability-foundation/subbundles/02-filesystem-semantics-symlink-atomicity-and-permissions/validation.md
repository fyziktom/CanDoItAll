# A02 validation

## Focused commands

```text
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~FileSystem|FullyQualifiedName~Storage|FullyQualifiedName~Symlink|FullyQualifiedName~Permission|FullyQualifiedName~Watcher'
```
```text
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=FileSystemPortability'
```
```text
python ./scripts/validate_bundle.py --bundle-root . --repo-root <repo> --stage prepared
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
| Windows | Full Unit project after atomic no-clobber remediation | 5,442/5,442 |
| Windows | FS-008 allocation, durable commit, storage, and stable-identity slice | 60/60 |
| Windows | Required A02 unit filter | 266/266 |
| Windows | `Category=FileSystemPortability` integration | 82/82 |
| Windows | Final Hosting/alias scoped-authority slice | 46/46 |
| Windows | Full Release solution build after atomic no-clobber remediation | 0 warnings/errors |
| Linux Docker | FS-008 allocation, durable commit, storage, and stable-identity slice | 60/60 |
| Linux Docker | Required A02 unit filter | 266/266 |
| Linux Docker | Extended A02-owned slice | 376/376 |
| Linux Docker | `Category=FileSystemPortability` integration | 82/82 |
| Linux Docker | Final Hosting/alias scoped-authority slice | 46/46 |
| Linux Docker | Full Release solution build after atomic no-clobber remediation | 0 warnings/errors |

The authoritative artifact paths, failure classifications, architecture proof, static audit, redaction classification, and residuals are in `../../reviews/09-a02-evidence-report.md`.
