# Current State

## Relevant Repo State

- `ProcessWorkspace.razor` already uses `ListDetailShell`, but the surrounding `PageScaffold` is not in `FillHeight` mode and the child content is not wrapped in a remaining-height stack. The process-definition list and the detail area therefore grow the page instead of staying inside the visible workspace height.
- The process detail `Tabs` use the shared component, but the current usage does not enable `FillHeight` or panel overflow behavior. The selected tab panel behaves like normal flowing content instead of a bounded workspace pane.
- `ProcessTemplateLibraryDialog.razor` already uses `ListDetailShell` plus pane-level scroll wrappers, but the dialog body uses an extra fixed-height wrapper inside a dialog body that already scrolls. The modal layout therefore risks nested scrolling and weak height propagation.
- `ProcessTemplateMermaidPreview.razor` wraps the rendered Mermaid output in overflow-hidden containers, but the preview host does not fully enforce a bounded viewport for transformed content in the live dialog layout. The user-provided screenshot shows the rendered graph visually bleeding across adjacent modal content.

## Reference Pattern

- `C:\repositories\CanDoItAll.AgentFramework\src\CanDoItAll.AgentFramework.Sandbox\Components\Pages\Chat.razor` demonstrates the target containment pattern:
- `PageScaffold FillHeight="true"`
- an inner `Stack` or `Grid` with `flex-1 min-h-0`
- workspace panes with `h-full min-h-0 overflow-hidden`
- internal list wrappers with `overflow-y-auto`
- `Tabs` configured with `FillHeight="true"` and `PanelOverflowMode="Auto"`

## Scope Boundary

- This bundle does not redesign the processes workspace.
- This bundle does not replace `ListDetailShell`, `Tabs`, or `Dialog`.
- This bundle focuses on correct usage of the existing shared components plus the smallest Mermaid host containment fix needed to stop the visual overflow regression.
