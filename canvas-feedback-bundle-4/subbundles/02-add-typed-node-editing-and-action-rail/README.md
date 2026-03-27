# 02 Add Typed Node Editing And Action Rail

## Objective

Introduce a deliberate inspector action rail with icon-backed buttons and add a typed edit flow that reuses the shared canvas composer to update existing nodes.

## Covered Notes

- `N004`
- `N005`
- `R004`
- `R005`
- `R006`
- `R007`
- `R008`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.CreateCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCreateRequestComposer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\CanvasWorkbenchContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`

## Deliverables

- explicit inspector action model with stable button ordering, icons, and stronger layout
- `Edit` action wired to the shared canvas composer with current values preloaded
- typed update path for existing nodes that validates metadata and preserves schedule fields when applicable

## Implementation Steps

1. Build a small action descriptor model for inspector buttons so label, icon, tone, and order are explicit.
2. Add an `Edit` action that opens the shared canvas composer for supported node types.
3. Reuse existing create definitions and metadata parsing to prefill text fields and typed input values.
4. Extend the workbench update path so edit submission persists typed fields for existing nodes.
5. Add focused tests for the edit action visibility, prefill path, and persistence behavior.

## Do Not Do

- do not create a second custom edit modal outside the shared canvas composer
- do not persist edit data through unvalidated raw JSON string concatenation
- do not move Delete ahead of the safer day-to-day actions

## Acceptance Checklist

- action buttons show consistent visual treatment with icons
- Delete is last in the inspector action order
- supported nodes show an Edit action
- Edit opens with current field values preloaded
- saving the edit updates the node through a typed path and refreshes the surface

## Proof Required

- focused component coverage for action ordering and edit affordance
- focused integration or unit coverage for the typed update path
- execution report updated with the exact validation command and result

## Suggested Agent Prompt

```text
Implement subbundle 02 only.

Add a typed inspector edit flow that reuses the shared canvas composer for existing nodes. Keep the metadata handling strongly typed, make the action rail read as an intentional shared UI, and preserve all existing inspector-side behaviors.
```
