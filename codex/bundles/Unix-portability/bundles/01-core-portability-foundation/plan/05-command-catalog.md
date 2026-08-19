# Core command catalog

Commands are templates. Record exact tool versions, OS/profile, exit code, duration, and evidence path.

## Validation cadence

Use the smallest test scope that can disprove the current change:

1. During implementation, build the affected project and run the named regression tests with `--no-build`.
2. Before a subbundle review, run the focused filters from that subbundle's `validation.md`.
3. Run the stable solution suite once per gate and actual host, not after documentation, evidence, checksum, or static-analysis-only edits.

A stable-suite result may be reused only while production code, shared build configuration, test infrastructure, and its host/runtime inputs remain unchanged. A change in one of those surfaces invalidates the affected project or host result, but does not automatically invalidate unrelated suites. Record reused evidence explicitly rather than copying or regenerating it.

## Checkout

```text
git status --short --branch
git rev-parse HEAD
git log -1 --oneline --decorate
dotnet --info
```

## Stable gate (final boundary only)

```text
dotnet restore ./CanDoItAll.slnx
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test ./CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

## Fast implementation loop

```text
dotnet build ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --no-restore
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~<changed-contract-or-regression>"
dotnet build ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --no-restore
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --no-build --filter "FullyQualifiedName~<changed-contract-or-regression>"
dotnet build ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release --no-restore
```

## Publish

```text
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r linux-x64 --self-contained false -o <artifact>/linux-x64
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r osx-arm64 --self-contained false -o <artifact>/osx-arm64
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r osx-x64 --self-contained false -o <artifact>/osx-x64
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r win-x64 --self-contained false -o <artifact>/win-x64
```

## Headless startup

Use explicit non-secret configuration and desktop/runtime features disabled:

```text
ASPNETCORE_URLS=http://127.0.0.1:<port>
ASPNETCORE_ENVIRONMENT=Production
Storage__WorkspaceRoot=<verified-host-root>
ControlPlane__RootPath=<verified-host-root>
FileTools__DesktopLaunch__Enabled=false
```

Secret provider/key-ring/database values follow A04/A06 and must not be written to captured command files.

## Bundle

```text
python ./scripts/validate_bundle.py --bundle-root . --stage portable
python ./scripts/materialize_bundle.py --bundle-root . --repo-root <repo> --output-root <materialized-bundle>
python <materialized-bundle>/scripts/validate_bundle.py --bundle-root <materialized-bundle> --repo-root <repo> --stage prepared
python ./scripts/scan_portability.py --repo-root <repo> --output <repo>/artifacts/unix-portability/A00/scan.json
```
