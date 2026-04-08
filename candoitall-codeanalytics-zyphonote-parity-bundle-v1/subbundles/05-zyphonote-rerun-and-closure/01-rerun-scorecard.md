# Zyphonote Rerun Scorecard

## Context

- Installed server under test: `C:\repositories\CanDoItAll\.artifacts\mcp-installs\CanDoItAll.Mcp.CodeAnalytics\current\CanDoItAll.Mcp.CodeAnalytics.exe`
- Harness: `C:\repositories\CanDoItAll\tools\CanDoItAll.Mcp.ToolHarness\bin\Debug\net10.0\CanDoItAll.Mcp.ToolHarness.exe`
- Fresh snapshot: `snap-20260408215645-36a986a3`
- Baseline for comparison: previous CodeAnalytics run `37 / 50`

## Scenario Results

| Scenario | Tool path used | Result summary | Score |
| --- | --- | --- | --- |
| `1. Architecture dependency discovery` | `snapshot_build` -> `solution_inventory_get` -> `project_inventory_get` | Correct product projects were recovered directly from project references. Raw inventory also included `Zyphonote.MusicTheory.Tests` and `Zyphonote.MusicTheory.Benchmarks`, so the answer needs a product-project filter for maximum precision. | `9 / 10` |
| `2. DI registration resolution` | `services_get` -> `symbols_search` | Returned the registration file and line, exact interface and implementation types, and enough path evidence to name `App.Blazor` as the host layer. | `10 / 10` |
| `3. Persistence surface discovery` | `symbols_search` -> `symbol_members_get` -> `symbol_definition_get` | Returned the DbContext file, all `20` declared `DbSet` properties, and the repeated `NormalizePendingChanges()` save-path behavior. It still takes a small stitched query chain instead of one persistence-specific answer. | `9 / 10` |
| `4. Method behavior reconstruction` | `symbols_search` -> `symbol_definition_get` -> `document_symbols_get` -> `document_source_get` | Returned the method body, guard conditions, state resets, selection sync, and editor canvas collaborators directly from the installed server. | `10 / 10` |
| `5. Polymorphism and consumer discovery` | `symbols_search` -> `symbol_implementations_get` -> `symbol_references_get` | Returned both `INotationRenderer` implementations and identified `NotationRenderService` as the consumer constructing them. | `9 / 10` |

## Total

- Updated CodeAnalytics result: `47 / 50`
- Improvement vs previous CodeAnalytics run: `+10`

## Scenario Evidence

- Scenario 1 raw direct references: `Zyphonote.AI.TranscriptionLab`, `Zyphonote.API`, `Zyphonote.App`, `Zyphonote.App.PdmxTool`, `Zyphonote.Components`, `Zyphonote.MusicNotation.Editor`, `Zyphonote.MusicTheory.Benchmarks`, `Zyphonote.MusicTheory.Tests`
- Scenario 1 product-filtered references: `Zyphonote.AI.TranscriptionLab`, `Zyphonote.API`, `Zyphonote.App`, `Zyphonote.App.PdmxTool`, `Zyphonote.Components`, `Zyphonote.MusicNotation.Editor`
- Scenario 2 registration: `src/App.Blazor/ServiceCollectionExtensions.cs:65`
- Scenario 3 DbContext path: `src/App.PdmxTool/Data/PdmxWorkstationDbContext.cs:5`
- Scenario 4 method path: `src/App.Blazor/Components/NotationEditor.razor.cs:600`
- Scenario 5 primary consumer: `MusicTheory.Core.NotationEditor.Rendering.NotationRenderService`

## Interpretation

The updated MCP now reaches or exceeds the practical benchmark bar for the five Zyphonote scenarios when the query flow follows the new deterministic path:

- use inventory tools for solution/project graph questions
- use exact symbol tools for method and type inspection
- use document tools for file-level inspection
- use focused context only after the seed is known and the broader stitched view is actually needed
