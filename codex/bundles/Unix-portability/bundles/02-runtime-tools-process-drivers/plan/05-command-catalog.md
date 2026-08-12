# Runtime command catalog

## Validation cadence

Use a layered loop so runtime work stays fast without weakening its gates:

1. Build the affected project and run the named regression tests with `--no-build` after each implementation edit.
2. Run only the active subbundle's focused unit, integration, or Playwright filters before its review.
3. Run the stable solution suite once per gate and actual host. Documentation, evidence, checksum, and static-analysis-only edits do not trigger it again.

A stable result remains reusable only when production code, shared build configuration, test infrastructure, and host/runtime inputs relevant to that result have not changed. Invalidate only the affected result, record the reuse, and leave unrelated green evidence intact.

## Entry verification

```text
git status --short --branch
git rev-parse HEAD
dotnet --info
python ./scripts/validate_bundle.py --bundle-root . --repo-root <repo> --stage prepared
```

Confirm the main and sibling commits equal the accepted core handoff anchors before B00. When the provisional exception is active, also verify that hosted/macOS support remains explicitly deferred.

## Stable regression (final boundary only)

```text
dotnet restore ./CanDoItAll.slnx
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test ./CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

## Focused runtime tests

```text
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~Process|FullyQualifiedName~Runtime|FullyQualifiedName~Mcp|FullyQualifiedName~Manager|FullyQualifiedName~Plugin|FullyQualifiedName~ProcessDriver"
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --no-build --filter "Category=UnixRuntimePortability"
dotnet test ./tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj -c Release --no-build --filter "Category=UnixRuntimePortability"
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
