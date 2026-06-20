# SB33 Agent Chat Load Analysis

## Live Observation

Captured from the unchanged development instance on `http://localhost:5032`:

- `GET /api/agents?includeTemplates=false`: 152 ms.
- `GET /api/agents/{agentId}/chat-sessions`: 15003 ms.
- `GET /api/agents/{agentId}/chat-workspace`: 14630 ms.
- `GET /api/project-structure/projects`: 23 ms.
- `POST /api/project-structure/projects/{projectId}/structure/read`: 78 ms.

The selected agent was `.NET Application Developer`. The selected project was `TetrisGame`. The runtime node summary was `Run app`, subtype `dotnet-runtime`, with `metadataJson = null` and `actionCapabilities = null`.

## Bottleneck

The slow path is backend chat workspace/session access, not project-structure rendering. The old chat service path used full execution workspace load/update for session list/create/rename and latest-run bootstrap even though split chat projections and execution-run detail stores already existed.

The UI compounded the backend delay by waiting for `GetOrCreateChatSessionAsync` and workspace loading before it opened the floating chat window.

## Repair

`AgentFrameworkWorkspaceExecutionService` now uses `ISandboxWorkspaceChatQueryStore`, `ISandboxWorkspaceChatSessionStore`, and `ISandboxWorkspaceExecutionRunStore` for the hot chat paths. `FileSandboxWorkspaceStore` persists chat-session create/update operations directly to split session files, execution indexes, workspace indexes, and chat projections without rewriting the full execution document.

`ContextualAgentWorkspaceWindows` now opens the chat shell immediately, clears stale state, and shows busy state while the session/workspace request completes.

## Residual Risk

The running 5032 process was intentionally left alive, so this proof does not include an after-deploy browser timing measurement. The code path is covered by build, unit, and integration proof; live speed should be verified after the development instance is restarted on the new build.

