# Preview Renderers And Selective Import Flows

## Status

- `Completed`

## Objective

- Close the right-side preview experience and the selective import actions for processes, roles, and artifacts.

## Covered Inputs

- Render mermaid with pan and zoom.
- Render markdown with Markdig.
- Render json with JsonViewer.Blazor.
- Show a structure tree.
- Import full processes, direct roles, and step-targeted artifacts.
- Allow importing a role directly from a process preview.

## Prerequisites

- `subbundles/01-library-foundation-and-preview-models`
- `subbundles/02-fullscreen-template-dialog-and-list-shell`

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.DefinitionCrud.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEditorModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateCatalogService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateProjectionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackResourceModels.cs
- C:\repositories\CanDoItAll\Templates\Processes

## Deliverables

- Right preview panel with a structure tree and segmented preview content.
- Mermaid preview wrapper with repo-owned pan and zoom behavior around MermaidJS.Blazor output.
- Markdown preview rendered through Markdig output.
- Json preview rendered through JsonViewer.Blazor.
- Full process import action that keeps the modal open and reloads the definitions list.
- Role import action for both the role category and the role cards inside a process preview.
- Artifact import action that requires a target step in the current editor and keeps the modal open after success.

## Dependency Impact

- The regression phase depends on this subbundle because browser proof only matters once the real preview and import behavior exists.
- Weak proof here would hide the most likely failures: wrong sidecar resolution, mermaid render failure, ambiguous artifact target selection, and incomplete import wiring.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Build strongly typed preview tabs or panels for overview, markdown, diagrams, and json.
2. Add the structure tree to the right preview rail.
3. Wrap MermaidJS.Blazor with repo-owned pan and zoom interop.
4. Implement full process import through the existing projection and import seam.
5. Implement role import into the current definition editor and expose it from both role and process previews.
6. Implement artifact import with explicit target-step selection and canvas refresh.

## Scope Exceptions

- Final regression reporting and bundle closure live in later subbundles.

## Do Not Do

- Do not silently guess an artifact target step.
- Do not replace JsonViewer.Blazor or MermaidJS.Blazor with custom renderers.
- Do not import a full process when the user explicitly chose a role-only action.

## Acceptance Checklist

- Process previews show real markdown, json, mermaid, and structure content from the template pack.
- Resource previews show the correct sidecar content when available.
- Process import adds a new definition and keeps the modal open.
- Role import appends the role into the current editor without importing the whole process.
- Artifact import appends an artifact expectation into the selected step and refreshes the canvas state.

## Proof Required

- Updated component tests for selective process, role, and artifact imports.
- Browser proof for mermaid rendering and pan or zoom interaction.
- Browser proof for role import directly from a process preview.
- Browser proof for artifact import with an explicit target step.

## Browser Validation Logging

- Route under test: `/processes`
- Required viewports: desktop `1900x1200`
- Required Playwright actions: open the modal, select a process, inspect the preview tabs, zoom or pan a mermaid diagram, import a process, import a role from a process preview, import an artifact to a selected step, confirm the modal stays open.
- Required screenshots: process preview, mermaid preview, role import success, artifact import success.
- Required screenshot review questions: is the tree legible, is the diagram inspectable, and do the import actions communicate the target context clearly.

## Progression Gate

- Regression work may continue only after process import, direct role import, and artifact-to-step import are all proven with both component and browser evidence.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Close the preview surfaces and selective import behaviors for process, role, and artifact templates.
```
