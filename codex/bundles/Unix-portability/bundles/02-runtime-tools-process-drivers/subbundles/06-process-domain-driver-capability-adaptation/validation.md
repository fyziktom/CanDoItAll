# B06 validation

## Focused commands

```text
dotnet build ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --no-restore
pwsh ./codex/bundles/Unix-portability/scripts/run_b06_focused_tests.ps1
```
```text
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --no-restore --filter 'FullyQualifiedName~ProcessCapabilityPortabilityIntegrationTests'
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

## Governed result

- Windows Release exact B06 unit slice: all 124 governed method patterns executed as 206 passed cases, 0 failed, 0 skipped.
- Windows Release Process capability integration: 1 passed, 0 failed, 0 skipped.
- Pinned Ubuntu 24.04 Docker, same prebuilt assemblies: the same 124 method patterns executed as 206 passed unit cases and 1 passed integration case, with no failures or skips.
- Modules.Processes, Unit, and Integration Release builds: zero warnings and zero errors.
- Source-reference manifest: 171 records, 171 unique IDs, 171 unique paths, zero missing paths.
- Project graph: 106 projects, 639 in-repository references, zero cyclic projects.
- Actual macOS and hosted CI: deferred to B07 by explicit operator instruction.

## Failure handling

Do not skip, quarantine, weaken policy, or broaden the allowlist to obtain green tests. Classify the failure, update the finding/requirement, and invoke the named correction/recovery path when a foundational invariant fails.
