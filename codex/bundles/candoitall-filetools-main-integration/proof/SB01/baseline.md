# SB01 Baseline Proof

Date: 2026-07-12.

## Source State

- Main: branch `file-tools-browsing`, commit `a0ad73fdc`; bundle-tracking commit is the only change from the prepared main source pin before execution edits.
- FileTools: branch `main`, commit `bdfa4a3`; the two-file culture-stability repair below is the only source diff.
- Components: branch `main`, clean at the current `origin/main` commit.

## Required Baseline Repair

The first FileTools test run failed only under the machine's `cs-CZ` culture because `FileBrowserDisplayFormatter.FormatSize` used current-culture separators while its package contract and tests require stable compact output. The repair uses `CultureInfo.InvariantCulture` and adds an explicit `cs-CZ` regression test.

Changed source hashes:

```text
c7679bc0082bace217ed418d837fb9b1cce9cd9e3f935bf89e7ae22e34233d2e *src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserUiModels.cs
3df9fd3ab1f6f25a1fb668171e879a3b17dbe9c2e7b585fa43c1ba7e5628a87d *tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserDisplayFormatterTests.cs
```

## Commands And Results

- User-local SDK provisioning: official `dotnet-install.ps1 -Version 10.0.301`; Pass. The repository pin was not changed.
- `dotnet restore .\CanDoItAll.FileTools.slnx`; Pass.
- `dotnet build .\CanDoItAll.FileTools.slnx -c Release --no-restore -warnaserror`; Pass, 0 warnings/errors.
- `dotnet test .\CanDoItAll.FileTools.slnx -c Release --no-build --no-restore`; Pass, 434 tests.
- `dotnet format .\CanDoItAll.FileTools.slnx --verify-no-changes --no-restore`; Pass.
- `.\scripts\pack-release.ps1 -Configuration Release -NoBuild -NoRestore`; Pass, seven nupkg and seven snupkg artifacts at version `0.1.0`.
- `.\scripts\validate-packages.ps1`; Pass. Hashes: `bundle://proof/SB01/package-hashes.sha256`.
- `dotnet restore .\CanDoItAll.slnx`; Pass.
- `dotnet build .\CanDoItAll.slnx -c Release --no-restore -warnaserror`; Pass, 0 warnings/errors.
- Unit Storage filter; Pass, 35 tests.
- Integration Storage filter; Pass, 10 tests.

## Architecture And Tool Baseline

- Main CodeAnalytics: `snap-20260713012357-7a36997e`, seven scoped product projects loaded, no blocking snapshot error.
- FileTools CodeAnalytics: `snap-20260713013754-65c579d0`, 15 projects, 225 documents, 388 types, 2,849 members, no dependency cycle, no blocking error.
- Components MCP: libraries and large-screen file-browser recommendations returned successfully after direct server recovery. BaseLib `Dialog`, `Alert`, `LoadingState`, `EmptyState`, and OverlayLib `OverlayWindow` are the relevant candidates; exact component/example calls remain phase-local before UI edits.
- Shared dotnetwatch: healthy detached backend with SourceWatch, SourceRun, BuildTest, published-candidate, and browser-capable lanes available.

## Gate Decision

`Pass`. SB02 may enter. The FileTools source repair changes the package hashes and therefore becomes part of the current SB01 provenance; any further FileTools drift reopens SB01. UI remains subject to phase-local Components MCP and browser proof.

## SB06 Provenance Re-entry

SB06 exposed one semantic contract gap: FileTools could not express provider-native ordering, while the accepted native Storage providers explicitly reject global ordering. Mapping provider order to `Name` would have been false behavior, so SB01 reopened narrowly.

FileTools added the typed `FileBrowserSortField.ProviderNative` value and stable-order behavior in Core/provider comparers. New source hashes:

```text
f145a10d0b30951beaff82a7d3e9345d55549d960a20b3097c79755499dafdde  src/CanDoItAll.FileTools.Abstractions/FileBrowser/FileBrowserCapabilities.cs
dbc284ec55f1717ee918d0d917611d45554c2ff88a94b76aeba5c8f771870eae  src/CanDoItAll.FileTools.FileBrowser.Core/Runtime/FileBrowserItemOrdering.cs
db5c6fab871ffb26dc0a469567ef6ebace537768a451b192b415c62960a9ba59  src/CanDoItAll.FileTools.Providers.FileSystem/FileSystemFileBrowserItemComparer.cs
9d294d4e4b18666e91bc4b43cf69f0ed1069f5ad378a46b2cb9350c728878ee9  tests/CanDoItAll.FileTools.FileBrowser.Core.Tests/FileBrowserFocusedCollaboratorTests.cs
```

Re-entry pipeline: restore Pass; Release build warnings-as-errors Pass; 435 tests Pass; format Pass; seven packages plus seven symbol packages pack/validate Pass. `package-hashes.sha256` and all 14 artifacts in `ExternalPackages` now reflect this accepted package set. SB01 returns to `Pass` and SB06 may continue.

## SB10 Search-Budget Re-entry

SB10's shallow-pass review found that progressive FileTools search still exposed only item and container limits. A page-size cap could therefore hide excessive first-result latency or retained snapshot state. SB01 reopened with SB06-SB09 because the package contract needed typed duration, concurrency, match-count, and retained-byte budgets before a real UI could consume it.

The repair forwards a validated session budget, bounds progressive work and retained matches, cancels an in-flight browse when its duration expires, reports retained items/bytes, peak concurrency, and elapsed duration, and preserves those facts across continuation pages. The provider-native adapter remains responsible for its already-proven native Storage budget; no recursive fallback was introduced.

Changed source hashes:

```text
ea8258545d47a73e65a365e686f97a03fdbab8a75fa3679adfd235efc651906b *src/CanDoItAll.FileTools.Abstractions/FileBrowser/FileBrowserPages.cs
da46ea896ac08fec5f3f0b585f1b331bd2003ab5467a391dd3ff61210c95aa8c *src/CanDoItAll.FileTools.Abstractions/FileBrowser/FileBrowserQueries.cs
f06eac4b4a411cbbfffbb616129163215df8c1b00628d30b9af491d7c91cbc24 *src/CanDoItAll.FileTools.FileBrowser.Core/Runtime/FileBrowserSessionModels.cs
66cbf7c8875d9a86901f7926b25921cd984cd0383e3285dcb0a505d0e84a5954 *src/CanDoItAll.FileTools.FileBrowser.Core/Search/FileBrowserSearchCoordinator.cs
2306266660164ef2de4b6d045425aadcc97f779d9b739119e4bfb650ce4f43e2 *src/CanDoItAll.FileTools.FileBrowser.Core/Search/FileBrowserSearchMatching.cs
e2c0983ecfed2169f400f01dd367a14bb2ea8dd0a8f44545a071cd9832de13ef *src/CanDoItAll.FileTools.FileBrowser.Core/Search/FileBrowserSearchRetentionMeasure.cs
b94f77cc520febe2adc7a7a81039ca17e88e30c4bd29d92da323cca645b95041 *src/CanDoItAll.FileTools.FileBrowser.Core/Search/ProgressiveFileBrowserSearchStrategy.cs
2ecd2fa66d6fb8927de315977c6c4628e98567352179a8764e581818ba42b5ce *src/CanDoItAll.FileTools.FileBrowser.Core/Search/ProgressiveSearchContinuationStore.cs
59d1efaaccb9db75ccabf75f66f1a33ea1a0fd69071c4e2fa088cc57104257d8 *tests/CanDoItAll.FileTools.FileBrowser.Core.Tests/FileBrowserFocusedCollaboratorTests.cs
2450d1777999c50246f4d177072b2902ce281b6ea32f0e54d14d173d9493f860 *tests/CanDoItAll.FileTools.FileBrowser.Core.Tests/FileBrowserSessionBoundaryTests.cs
f852668ecd40d62983d924800397f027b8702b722f2e984710ea9b745b208634 *tests/CanDoItAll.FileTools.FileBrowser.Core.Tests/SearchStrategyTests.cs
```

Re-entry pipeline: 61 focused failing-first/boundary tests Pass; all 440 FileTools tests Pass; Release build warnings-as-errors Pass; format Pass; seven packages plus seven symbol packages validate; FileTools CodeAnalytics snapshot `snap-20260713055310-9277b469` loads 15 projects and 226 documents with no blocking error or dependency cycle. The main repository consumes the seven replacement packages, restores successfully, builds Web with zero warnings/errors, and passes 98 affected unit tests plus eight HTTP host tests. SB01 and dependent SB06-SB09 return to `Pass`; SB10 may continue.

## SB10 Compact Browser Layout Re-entry

The first original-resolution pilot screenshot exposed a package-level Compact-layout defect: with one declared source, the source-navigation grid row stretched through the available dialog height and displaced the actual browser. The repair belongs in FileBrowser.Components, not as a host CSS override.

The component now emits a typed `has-source-navigation` state and defines explicit `auto minmax(0, 1fr)` grid rows for Compact/Minimal layouts. The regression contract asserts the emitted class and stylesheet rule. Changed source hashes:

```text
e5345dbc67085840babd3db11eb5344325f28af85c0b184b9c78bd3f6cf5e9dc *src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowser.razor
6a1a985a457c2646e1db59a8b7e866d80963cf7707ffe13cf26627e807237f8d *src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowser.razor.cs
77740ec0f084975bd10bf431dddfa24c86185be3bec2e9b42239f2358e59d984 *src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowser.razor.css
2353e2d59df7ca0beb0399bc44f20be376fec82ec66fff283e4d7c78bfef694f *tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserComponentContractTests.cs
```

FileBrowser component tests Pass 45/45; user-local SDK 10.0.301 project-mode format and folder-workspace whitespace verification Pass. FileBrowser.Components was repacked and validated, the main local package/cache was refreshed, and the current package hashes are recorded in `package-hashes.sha256`. The replacement main Web build passes with zero warnings/errors, static assets resolve, and accepted desktop screenshots confirm the defect is closed. SB01 remains `Pass`.
