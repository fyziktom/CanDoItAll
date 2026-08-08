# Core command catalog

Commands are templates. Record exact tool versions, OS/profile, exit code, duration, and evidence path.

## Checkout

```text
git status --short --branch
git rev-parse HEAD
git log -1 --oneline --decorate
dotnet --info
```

## Stable gate

```text
dotnet restore ./CanDoItAll.slnx
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test ./CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

## Focused projects

```text
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release
dotnet build ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release
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
