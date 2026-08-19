# A06 validation

## Focused commands

```text
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r linux-x64 --self-contained false -o <artifact>/linux-x64
```
```text
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r osx-arm64 --self-contained false -o <artifact>/osx-arm64
```
```text
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r osx-x64 --self-contained false -o <artifact>/osx-x64
```
```text
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r win-x64 --self-contained false -o <artifact>/win-x64
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
