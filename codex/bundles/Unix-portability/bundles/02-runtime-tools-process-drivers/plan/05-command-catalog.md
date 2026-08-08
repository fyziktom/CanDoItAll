# Runtime command catalog

## Entry verification

```text
git status --short --branch
git rev-parse HEAD
dotnet --info
python ./scripts/validate_bundle.py --bundle-root . --repo-root <repo> --stage prepared
```

Confirm the commit equals the completed Core C4 handoff before B00.

## Stable regression

```text
dotnet restore ./CanDoItAll.slnx
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test ./CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

## Focused runtime tests

```text
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~Process|FullyQualifiedName~Runtime|FullyQualifiedName~Mcp|FullyQualifiedName~Manager|FullyQualifiedName~Plugin|FullyQualifiedName~ProcessDriver"
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter "Category=UnixRuntimePortability"
dotnet test ./tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj -c Release --filter "Category=UnixRuntimePortability"
```

## Process characterization

Use dedicated fixtures that start known child/grandchild processes, emit bounded stdout/stderr, respond or ignore graceful termination, and write PIDs to a test-owned directory. Do not run kill tests against arbitrary developer processes.

## External dependency characterization

Record exact commands/versions without secrets:

```text
dotnet --info
git --version
docker version
node --version
npm --version
pwsh --version
python --version
```

Absence is a valid capability result. Do not install or modify global tools unless the active subbundle explicitly governs it.

## Bundle scan

```text
python ./scripts/scan_portability.py --repo-root <repo> --output <repo>/artifacts/unix-portability/B00/runtime-scan.json
```
