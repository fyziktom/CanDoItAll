# Gap Closure Proof

## Installed Server Evidence

- Installed-server proof file: `C:\repositories\CanDoItAll\candoitall-codeanalytics-gap-closure-bundle-v1\subbundles\03-reinstall-rerun-and-closure\evidence\gap-closure-proof.json`
- Proof snapshot: `snap-20260408230311-8f086597`
- Query path: installed `CanDoItAll.Mcp.CodeAnalytics.exe` invoked through `CanDoItAll.Mcp.ToolHarness`

## Proven Outcomes

- `Zyphonote.MusicTheory.Core` now reports `projectRole = Product`.
- Primary `referencedByProjects` contains only the six product projects from the answer key:
  - `Zyphonote.AI.TranscriptionLab`
  - `Zyphonote.API`
  - `Zyphonote.App`
  - `Zyphonote.App.PdmxTool`
  - `Zyphonote.Components`
  - `Zyphonote.MusicNotation.Editor`
- Supporting reverse references remain visible and explicit:
  - `Zyphonote.MusicTheory.Benchmarks [Benchmark]`
  - `Zyphonote.MusicTheory.Tests [Test]`
- DI regression slice still resolves:
  - `MusicNotation.Editor.Services.IMidiService -> MusicNotation.Editor.Services.MidiService`
  - source `src/App.Blazor/ServiceCollectionExtensions.cs:65`
- Focused-context compatibility is restored:
  - `Behavior -> TroublePath`
  - `TroublePath -> TroublePath`
  - both seed `App.Blazor.Components.NotationEditor.ApplyExternalScoreAsync()`

## Commands Already Proven

- `dotnet test C:\repositories\CanDoItAll.CodeAnalsis\tests\CanDoItAll.CodeAnalytics.Tests.Unit\CanDoItAll.CodeAnalytics.Tests.Unit.csproj --no-restore`
- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CanDoItAll.Mcp.CodeAnalytics.csproj --no-restore`
- `powershell -NoProfile -ExecutionPolicy Bypass -File C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1 -RepoRoot C:\repositories\CanDoItAll`

## Remaining Gate

- Native in-session Codex MCP calls returned `Transport closed` after reinstall because the reinstall script stopped the CodeAnalytics process that this session had originally started.
- Fresh-launch proof is already green through the installed `exe`, so the remaining work is one more native Codex validation pass after the user restarts Codex.
