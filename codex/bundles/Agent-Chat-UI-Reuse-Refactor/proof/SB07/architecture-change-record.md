# SB07 architecture change record

Before SB07, `FloatingAgentChatHost` directly owned the catalog `OverlayWindow`, tab composition, active-chat card rendering, status mapping, and scroll CSS. `FloatingAgentChatSettingsPanel` directly rendered both generic active-chat lifecycle fields and Agent-only preparation fields.

After SB07:

- `CanDoItAll.Conversations.Components` owns source-neutral floating-window presentation, two-panel catalog composition, active-conversation list presentation/callbacks, and retention/capacity fields.
- `AgentActiveChatPresentationMapper` maps Agent handle/run state to opaque neutral presentation and rejects non-Agent keys explicitly.
- `FloatingAgentChatHost` still owns `IFloatingAgentChatCoordinator`, context registry/access, affinity, history, handle lifecycle, window state, close/keep/stop decisions, preparation, and workspace services.
- `FloatingAgentChatSettingsPanel` still owns load/save/notification behavior and Agent-only prepared activation stock.

CodeAnalytics before: `snap-20260816133136-acdf4779`, 3 projects, 439 types, 5022 members, two pre-existing intra-project module/type cycles.

CodeAnalytics after: `snap-20260816134719-acdf4779`, 3 projects, 441 types, 5033 members, the same two pre-existing cycle identifiers, and no blocking diagnostic. Project direction remains `Modules.AgentFramework -> AgentFramework.Components -> Conversations.Components`; the neutral project has no project reference back to Agent code.

The host code-behind fell from 656 to 649 lines and no partial file, service location, source-kind switch, or boolean matrix was introduced.
