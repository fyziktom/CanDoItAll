# Normalized Requirements

## Workspace Entry

- Replace the current `Seed development baseline` action with a `Templates` action in the Process management workspace.
- The empty-state actions must align with the same `Templates` entry point.

## Modal Shell

- Clicking `Templates` must open a fullscreen modal based on the shared BaseLib dialog system.
- The modal must keep a left list panel and a right preview panel visible on large screens.
- The left list panel must remain scrollable and searchable.
- The left list panel must expose tabs for `Processes`, `Roles`, and `Artifacts`.

## Template Cards And Selection

- Each category tab must show selection cards for the templates in that category.
- Search must filter the visible cards by title, summary, and nearby metadata within the active category.
- Selecting a card must update the preview pane without leaving the modal.

## Preview Surface

- The right preview panel must show a structure tree for the selected template.
- The right preview panel must render markdown previews using Markdig.
- The right preview panel must render json previews using JsonViewer.Blazor.
- The right preview panel must render mermaid diagrams using MermaidJS.Blazor.
- Mermaid previews must support pan and zoom so users can inspect large diagrams.

## Import Behaviors

- Process templates must support `Add to my processes` and create a new process definition through the existing process import seam.
- Role templates must support `Add to my roles` and append the role to the current definition editor without closing the modal.
- Artifact templates must support `Add to my artefacts` and append the artifact expectation to a user-selected step in the current definition editor without closing the modal.
- Process previews must expose direct add actions for roles referenced by that process so users can import a role without importing the full process.

## Feedback And Overlay Behavior

- Successful imports must raise a notification through the shared BaseLib notification service.
- The notification must remain visible above the fullscreen modal overlay.
- The modal must stay open after a successful import.

## Validation

- Add component tests for the modal entry, category filtering, and selective import behaviors.
- Add browser proof that covers modal open, preview rendering, selective import, and notification visibility above the modal.
