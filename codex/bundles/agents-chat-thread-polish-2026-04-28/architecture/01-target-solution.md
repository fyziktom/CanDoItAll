# Target Solution

- Add a strongly named favourite tag constant in the AgentFramework model layer.
- Add a chat-session rename operation to `IAgentFrameworkWorkspaceService`, the workspace facade, and the execution service.
- Replace the left-rail `SelectionListItem` thread rendering with a compact, rail-safe custom button that uses `TooltipTarget`.
- Add `Editable<T>` to the selected chat header title and raise a title-changed event to the page component.
- Extend `AgentSwitchDialog` with a `TextBox` search, `TagEditor` filter, local favourite sorting, and a star button that calls a supplied persistence delegate.
- Keep custom CSS local to the affected components where shared components do not express text clamping or nested button/card behavior.
