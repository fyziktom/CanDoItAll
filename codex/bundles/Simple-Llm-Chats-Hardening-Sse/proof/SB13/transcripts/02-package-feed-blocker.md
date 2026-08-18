# Package-feed blocker

Official read-only query:

```powershell
Invoke-RestMethod https://api.nuget.org/v3-flatcontainer/candoitall.filetools.fileinteraction.spreadsheet/index.json
```

Result: HTTP 404 on 2026-08-15. The NuGet Gallery search returns no package with the exact ID.

Repository evidence:

- `NuGet.Config` clears all sources and configures nuget.org only.
- `Directory.Build.props` pins all FileTools packages to 0.1.18.
- `CanDoItAll.Modules.Workbench.csproj` directly references the Spreadsheet package.
- Sibling `CanDoItAll.FileTools` commit `c95dd07208a6d48724443317cdc6cfe67a13020a` exists on
  `origin/development` and contains the Spreadsheet source.
- The sibling CI restores, builds, tests, packs, and validates nine packages, but has read-only contents
  permission and explicitly publishes nothing.
- No NuGet credential environment variable is configured on this runner.

Publishing a public package is an external, durable release action outside the current repository and
requires operator authority plus a NuGet publishing credential. Vendoring the package, duplicating its
source, or changing CI to hide the package graph would violate the selected dependency contract.

Resumption condition: publish `CanDoItAll.FileTools.FileInteraction.Spreadsheet` 0.1.18 to nuget.org (or
provide an approved source/configuration change that resolves the exact package graph), then resume the
still-unused SB13 single-shot gate.
