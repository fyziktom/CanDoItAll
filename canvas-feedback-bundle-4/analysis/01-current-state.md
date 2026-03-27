# Current State

## Confirmed Owners

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
  - owns the selection inspector markup, action buttons, summary tiles, and create-next-to-source card
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
  - owns the inspector-specific floating-window styling, summary grid styling, and action-row spacing
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.CreateCatalog.cs`
  - already hydrates create actions and dynamic select options for typed canvas composer fields
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs`
  - together own the typed create definitions and field metadata that can be reused for edit composition
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCreateRequestComposer.cs`
  - maps canvas composer field values into typed metadata envelopes for create flows
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
  - defines the typed metadata payloads that edit persistence must continue to honor
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
  - owns persisted object update methods and metadata validation
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\CanvasWorkbenchContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
  - already own the shared canvas composer and support prefilled `inputValues`

## Verified Findings

- The selected-node inspector currently repeats the node label and title in the top card and keeps all six summary tiles at the same priority level.
- The `Typed details` card is always expanded as a separate panel even when the details are secondary to the current task.
- Progress, Priority, and Marker are rendered as independent summary tiles, not as a deliberate compact status row.
- The `Node actions` area is a flat button pile with uneven visual weight and no icon affordance.
- The shared canvas composer already accepts prefilled request values, but the workbench page only uses it for create flows.
- `CanvasWorkbenchNodeEditRequest` currently only carries `NodeId`, `Title`, and `Notes`, and `HandleNodeEditedAsync` only updates note nodes.
- The workbench service exposes `UpdateObjectAsync` and `UpdateObjectMetadataAsync`, but there is no single typed path that updates title, subtitle, notes, metadata, and schedule together for general node editing.

## Existing Test Surface

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
