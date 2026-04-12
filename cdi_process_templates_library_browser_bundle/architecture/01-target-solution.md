# Target Solution

## UI Shape

- Keep `ProcessWorkspace` as the page orchestrator and move the new modal into a dedicated component instead of bloating the page markup.
- Use the shared BaseLib `Dialog` in `ModalSize.Full` mode.
- Use `ListDetailShell` for the overall left-panel and right-panel split.
- Keep category navigation inside the list pane with BaseLib `Tabs`.
- Keep the structure outline in a dedicated right-side support rail inside the preview pane with BaseLib `TreeView`.

## Service Shape

- Extend the Processes template catalog surface with browser-oriented list and preview models instead of reusing only the old toolbox seed records.
- Reuse `ProcessTemplateProjectionService` for full process import.
- Reuse the current editor model for role import and artifact import instead of introducing a new persisted library abstraction.
- Add a small mermaid pan-zoom host integration inside the repo because `MermaidJS.Blazor` renders the diagram but does not provide deep inspection controls itself.

## Import Boundaries

- `Add to my processes` must call the existing import seam and reload the workspace definitions.
- `Add to my roles` must mutate the in-memory `ProcessDefinitionEditorModel` and refresh the canvas surface.
- `Add to my artefacts` must mutate a selected `ProcessStepEditorModel.ArtifactExpectations` collection and refresh the canvas surface.
- Keep import actions explicit and fail visibly when the current authoring context cannot accept the selected resource.

## Validation Boundaries

- Component tests must prove the workspace-level orchestration and the selective import mutations.
- Browser proof must prove the external libraries are actually rendering and interacting inside the real modal.
