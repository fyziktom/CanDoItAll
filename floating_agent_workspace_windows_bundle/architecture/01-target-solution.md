# Target Solution

## Component Boundary

- Add a reusable AgentFramework component, `ContextualAgentWorkspaceWindows`, that renders a `CanvasFloatingWindow` launcher with search, tag filters, access badges, and agent rows.
- Add a second `CanvasFloatingWindow` chat using the existing `ChatWorkspacePanel`.
- Keep context filtering inside the shared component using strongly typed `ContextualAgentWorkspaceKind` and existing access metadata readers.
- Keep durable chat behavior through `IAgentFrameworkWorkspaceService.GetOrCreateChatSessionAsync` and `SendMessageAsync`.

## Host Integration

- Project structure adds an Agents toolbar icon, state key, toggle method, state persistence, and the shared component in `OverlayContent`.
- Processes adds an Agents toolbar icon, process canvas state, presenter members, and the shared component in the Steps canvas `OverlayContent`.

## Boundaries

- Do not change the agent access storage shape or migrations.
- Do not duplicate the full Agents page left rail inside the floating chat.
- Do not introduce Radzen components unless already used by the touched surface.
- Do not hand-roll generic chat UI when `ChatWorkspacePanel` already owns the chat body.
