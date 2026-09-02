# Validation Command Catalog

Run from a clean shell. Adapt path separators to the host. Do not combine source-mode and
package-mode outputs.

## Common preflight

```powershell
git -C ../CanDoItAll fetch --all --prune
git -C ../CanDoItAll.Components fetch --all --prune
git -C ../CanDoItAll.FileTools fetch --all --prune

git -C ../CanDoItAll status --short --branch
git -C ../CanDoItAll.Components status --short --branch
git -C ../CanDoItAll.FileTools status --short --branch
```

## Components assets and tests

```powershell
Push-Location ../CanDoItAll.Components

npm ci
npm ci --prefix Tailwind
npm run build:tailwind
npm run assets:verify

dotnet restore ./CanDoItAll.Components.slnx --configfile ./NuGet.config
dotnet build ./CanDoItAll.Components.slnx --configuration Release --no-restore
dotnet test ./CanDoItAll.Components.slnx --configuration Release --no-build --no-restore

Pop-Location
```

### Governed standard snapshot update

Run the failing tests first without update. Review the diff. Then, only if intended:

```powershell
Push-Location ../CanDoItAll.Components

$env:CDA_UPDATE_STANDARD_APPROVALS = "1"
try {
    dotnet test `
        ./tests/CanDoItAll.Components.BaseLib.Tests/CanDoItAll.Components.BaseLib.Tests.csproj `
        --configuration Release `
        --no-build `
        --no-restore `
        --filter "FullyQualifiedName~StandardPublishingApprovalTests"
}
finally {
    Remove-Item Env:CDA_UPDATE_STANDARD_APPROVALS -ErrorAction SilentlyContinue
}

git diff -- tests/CanDoItAll.Components.BaseLib.Tests/fixtures/approvals
Pop-Location
```

The Canvas static-asset manifest has a separate owning test. Locate it with `rg`, inspect actual
versus expected assets, and update only after review.

### Deterministic BaseLib CSS

```powershell
Push-Location ../CanDoItAll.Components
npm run build:tailwind
git diff --exit-code -- `
    src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css
Pop-Location
```

## Components package build

```powershell
Push-Location ../CanDoItAll.Components

$componentsPackages = ./tools/deployment/nugets/Build-NuGets.ps1 `
    -Configuration Release `
    -NoBuild `
    -NoRestore `
    -Version $Version

Pop-Location
```

Inspect every package and static web asset; use repository validators where available.

## FileTools full gate

```powershell
Push-Location ../CanDoItAll.FileTools

dotnet restore ./CanDoItAll.FileTools.slnx --configfile ./NuGet.config
dotnet build ./CanDoItAll.FileTools.slnx --configuration Release --no-restore -warnaserror
dotnet test ./CanDoItAll.FileTools.slnx --configuration Release --no-build --no-restore
dotnet format ./CanDoItAll.FileTools.slnx --verify-no-changes --no-restore

$fileToolsPackages = ./tools/deployment/nugets/Build-NuGets.ps1 `
    -Configuration Release `
    -NoBuild `
    -NoRestore `
    -Version $Version

./tools/validation/Test-NuGetPackages.ps1 `
    -PackageDirectory $fileToolsPackages.OutputDirectory

Pop-Location
```

## CanDoItAll source-reference mode

Delete prior `obj/bin` or use a dedicated clean workspace before switching modes.

```powershell
Push-Location ../CanDoItAll

dotnet restore ./CanDoItAll.slnx -p:UseLocalCanDoItAllLibraries=true
dotnet build ./CanDoItAll.slnx `
    --configuration Release `
    --no-restore `
    /m:1 `
    -p:UseLocalCanDoItAllLibraries=true

dotnet restore ./tests/Solutions/CanDoItAll.Tests.Stable.slnx `
    -p:UseLocalCanDoItAllLibraries=true
dotnet build ./tests/Solutions/CanDoItAll.Tests.Stable.slnx `
    --configuration Release `
    --no-restore `
    /m:1 `
    -p:UseLocalCanDoItAllLibraries=true

dotnet test ./tests/Solutions/CanDoItAll.Tests.Stable.slnx `
    --configuration Release `
    --no-build `
    --no-restore `
    --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true" `
    /m:1 `
    -p:UseLocalCanDoItAllLibraries=true

./tools/Validation/Test-Documentation.ps1

Pop-Location
```

## Targeted icon/component tests

Use exact current test names after merge:

```powershell
dotnet test ./tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj `
    --configuration Release `
    --no-build `
    --no-restore `
    --filter "FullyQualifiedName~AgentCompactListTests|FullyQualifiedName~AgentCatalogPanelTests|FullyQualifiedName~PresentationBadgeListTests|FullyQualifiedName~AppShellTests|FullyQualifiedName~MainLayout"
```

## Package-reference mode with a temporary local feed

Create a clean local feed containing all Components and FileTools packages at `V`. Generate a
temporary NuGet config below `.artifacts/`; do not edit the user's global configuration.

```powershell
dotnet restore ./CanDoItAll.slnx `
    --configfile ./.artifacts/ui-refactoring-integration/NuGet.local.config `
    -p:UseLocalCanDoItAllLibraries=false

dotnet build ./CanDoItAll.slnx `
    --configuration Release `
    --no-restore `
    /m:1 `
    -p:UseLocalCanDoItAllLibraries=false
```

Use a separate clean `obj/bin` graph from source mode.

## Browser proof

Use the repository's existing Playwright host/fixtures. Required assertions:

- large-desktop viewport,
- no page exception,
- no failed static asset request,
- BaseLib `material-symbols.css` returns 200,
- BaseLib `output.css` returns 200,
- no `.rz-icon-fallback` on inspected surfaces,
- representative controls retain layout and interaction,
- FileBrowser and FileInteraction open and function.

## Container proof

Use the repository's maintained Docker validation first, then a real source-context build:

```powershell
./tools/Validation/Test-Docker.ps1
```

```bash
docker build \
  --build-context components=../CanDoItAll.Components \
  --build-context filetools=../CanDoItAll.FileTools \
  --file src/App/CanDoItAll.Web/Dockerfile \
  --tag candoitall-ui-refactoring-integration:proof \
  .
```

Podman may be used equivalently when available. Record the engine and version.
