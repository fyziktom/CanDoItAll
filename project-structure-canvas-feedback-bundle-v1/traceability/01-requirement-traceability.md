# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Add the first requirement here. | `path/to/file.md` | `subbundles/01-example` | `dotnet test ...` | `List prerequisite or exception notes here.` |
# Requirement Traceability

| Raw note | Normalized requirement | Owning subbundle | Primary source files | Required proof |
| --- | --- | --- | --- | --- |
| `N001` | `RQ-01` Semantic node visual presets | `01-01-visual-profile-and-palette-foundation` | `ProjectWorkbenchModels.cs`, `ProjectObjectContracts.cs`, `ProjectStructureGraphAdapter.cs` | Component tests plus Playwright screenshots proving distinct rendered colors |
| `N002` | `RQ-02` Multiline inline notes | `03-03-inline-note-multiline-and-note-conversion` | `04-context-menu-and-composer.js`, `CanvasWorkbench.razor`, `ProjectStructurePage.NodeEditing.cs` | Playwright note-edit pass with multiline persistence and screenshot |
| `N003` | `RQ-03` Node id copy actions | `04-04-node-id-copy-and-subtree-clipboard-workflows` | `ProjectStructureSelectionPanel.razor`, `ProjectStructurePage.Workflows.cs`, CanvasLib clipboard files | Playwright action proof for copy id and subtree id structure |
| `N004` | `RQ-04` Subtree cut and paste | `04-04-node-id-copy-and-subtree-clipboard-workflows` | `CanvasWorkbenchEvents.cs`, `07-runtime-entry.js`, `ProjectStructurePage.razor` | Browser keyboard proof for `Ctrl+X` and `Ctrl+V` with descendants |
| `N005` | `RQ-05` Move descendants into subproject | `05-05-subtree-to-subproject-transfer` | `ProjectStructurePage.ProjectHierarchy.cs`, `ProjectStructureSubtreeRecompositionEngine.cs`, `ProjectModels.cs` | Integration plus Playwright proof of moved descendants and refreshed hierarchy |
| `N006` | `RQ-06` Change block type for common blocks | `02-02-catalog-expansion-and-type-mutation-flows` | `ProjectStructureCanvasCatalog.cs`, `ProjectStructureActionCatalogAdapter.cs`, page workflow files | Component and browser proof of type mutation preserving state |
| `N007` | `RQ-07` Common computer block | `02-02-catalog-expansion-and-type-mutation-flows` | `ProjectStructureCanvasCatalog.RichDefinitions.cs`, `ProjectWorkbenchModels.cs` | Toolbox search and create proof with screenshot |
| `N008` | `RQ-08` Convert simple note to block | `03-03-inline-note-multiline-and-note-conversion` | `ProjectStructurePage.NodeEditing.cs`, `ProjectStructureSelectionPanel.razor`, page workflow files | Browser proof showing converted title and retained note content |
| `N009` | `RQ-09` Router and WiFi common blocks | `02-02-catalog-expansion-and-type-mutation-flows` | `ProjectStructureCanvasCatalog.RichDefinitions.cs`, `ProjectWorkbenchModels.cs` | Toolbox creation and type-change proof with screenshots |
| `Mandatory validation` | `RQ-10` Mandatory validation and closure | `06-06-browser-proof-and-closure` | `AppSmokeTests.cs`, bundle review files | Populated execution analytics, screenshot paths, and completed-stage validator pass |
