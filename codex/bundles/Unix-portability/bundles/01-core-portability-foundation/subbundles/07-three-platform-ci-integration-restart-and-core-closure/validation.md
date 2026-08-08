# A07 validation

## Focused commands

```text
dotnet restore ./CanDoItAll.slnx
```
```text
dotnet build ./CanDoItAll.slnx -c Release --no-restore /m:1
```
```text
dotnet test ./CanDoItAll.slnx -c Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```
```text
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=UnixPortabilityCore'
```
```text
python ./scripts/validate_bundle.py --bundle-root . --repo-root <repo> --stage completed --bundle core
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
