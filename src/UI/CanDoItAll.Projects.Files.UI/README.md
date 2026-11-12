# Projects Files rendering

`ProjectFilesDialogView` and `ProjectFilesPortfolioPaneView` render the complete Files
surfaces: status, loading/error/retry, source navigation, bounded search, read-only
preview, host-action controls and feedback. They use the actual neutral FileBrowser and
FileInteraction components. The portfolio retains its Back action; the dialog retains
its existing close/reopen navigation.

`ProjectFilesViewState` contains presentation values and callbacks. The host supplies a
browser session and an independently authorized read-only content source. Removing a
renderer does not dispose those resources. The Projects module's effect hosts own
acquisition, actor/profile checks, accepted workspaces, cancellation, download/native
actions and exact cleanup. FileTools retains authorization and trusted-path policy.

The leaf has no Projects implementation, Infrastructure, EF, Workspace, AppComponents
or FileTools Integration reference. P1's `ProjectsBoard.FilesContent` slot and its lean
graph remain unchanged. Source mode currently evaluates eight projects for this leaf.
Existing BaseLib/FileTools assets supply styling; no feature-owned JS was introduced.

The [independent sandbox](../../Sandboxes/CanDoItAll.Projects.Files.UiSandbox/README.md)
uses these exact renderers. Its simulated actions are not evidence of native authority.
See the [architecture and validation record](../../../docs/architecture/projects-files-ui-decoupling.md).
