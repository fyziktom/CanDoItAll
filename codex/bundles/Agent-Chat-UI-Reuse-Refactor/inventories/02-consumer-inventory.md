# Consumer inventory

SB01 must replace this orientation inventory with live CodeAnalytics symbol/reference evidence.

| Surface | Known consumer | Risk |
|---|---|---|
| `ChatWorkspacePanel` | `AgentChatPanel` | primary Agent page behavior |
| `ChatWorkspacePanel` | `ContextualAgentWorkspaceWindows` | contextual windows |
| `ChatWorkspacePanel` | `ProcessWorkspaceShell` | cross-module Process UI |
| `ChatWorkspacePanel` | bUnit/component tests | public rendering contract |
| `AgentSelectionCard` | `AgentCatalogPanel` | catalog grid and managed Agent actions |
| `AgentSelectionCard` | `AgentSwitchDialog` | picker/filter/favorite behavior |
| `AgentCompactList` | `FloatingAgentChatHost` | floating catalog |
| `ProviderModelSelector` | `AgentDetailsDialog` | Agent runtime binding |
| `ProviderModelSelector` | image/runtime provider settings consumers returned by live search | broader settings compatibility |
| `AgentDetailsDialog` | Agents page/dialog orchestration | save/delete/version behavior |
| `FloatingAgentChatSettingsPanel` | settings page/surface returned by live route search | lifecycle and preparation behavior |

For every target symbol, record:

- exact definition;
- public parameters;
- direct references;
- representative consumers;
- owner tests;
- CSS files;
- route/dialog/window context;
- dependency direction.
