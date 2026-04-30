# Shared Contextual Agent Window Contract

## Status

- Status: `Completed`

## Objective

- Build the reusable contextual agent launcher and chat floating-window component.

## Covered Inputs

- R2: Access-based agent list with Read/Write indicators.
- R3: Search line and tag editor for tag search.
- R4: Double-click opens a second floating chat window and creates a new thread.
- R5: Chat reuses the existing chat functions.

## Prerequisites

- None.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentProjectStructureAccessModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentProcessAccessModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\TagEditor.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Core\CanvasFloatingWindow.razor

## Deliverables

- Shared AgentFramework contextual window model types.
- Launcher window with agent list, search input, tag editor, access badges, and double-click activation.
- Floating chat window backed by `ChatWorkspacePanel`.
- Thread creation through `IAgentFrameworkWorkspaceService.GetOrCreateChatSessionAsync`.
- Runtime update handling equivalent to the current chat page.

## Dependency Impact

- Project and process host subbundles cannot start safely until the shared component compiles and exposes stable parameters.
- Incorrect access filtering invalidates both downstream integrations.

## Validation Depth

- Critical UI foundation with build and focused filtering proof.

## Implementation Steps

1. Add strongly typed context/access models in AgentFramework components.
2. Add the contextual floating windows Razor component and CSS.
3. Reuse `ChatWorkspacePanel` for chat body and mirror the existing chat service flow.
4. Add CanvasLib reference to AgentFramework components if required by the shared floating window.
5. Keep test IDs stable for Playwright.

## Scope Exceptions

- Multiple simultaneous chat windows are not required in this phase; one active contextual chat window is acceptable if every double-click creates a new persisted thread.

## Do Not Do

- Do not duplicate `ChatWorkspacePanel` markup.
- Do not change stored access metadata shape.
- Do not expose agents without explicit contextual access.

## Acceptance Checklist

- Launcher renders with search and tag editor.
- Launcher filters agents by project or process access.
- Access badges distinguish Read and Write.
- Double-click opens a new chat window.
- Chat sends prompts through the existing workspace service.

## Proof Required

- Successful build of AgentFramework components and any focused tests.
- Code inspection confirms `ChatWorkspacePanel` reuse.

## Browser Validation Logging

- Browser proof is captured in downstream host subbundles.

## Progression Gate

- Shared component builds and exposes the parameters needed by both project and process hosts.

## Suggested Agent Prompt

```text
Implement the shared contextual agent floating-window component using the shared implementation prompt.
```
