# SB08 consumer migration closure

Every live reference discovered by the baseline inventory and final repository scan reaches the neutral owner either directly or through a purposeful Agent compatibility facade.

| Consumer | Route to neutral owner | Agent-owned behavior retained |
|---|---|---|
| `AgentChatPanel` | direct `ConversationThreadRail`; `ChatWorkspacePanel` facade to workspace/header/transcript/composer | session load, send, approval, voice, attachments, context and orchestration |
| `FloatingAgentChatHost` | direct floating window/catalog/active list; `AgentCompactList` facade to compact participant list | coordinator, preparation, affinity, history and lifecycle effects |
| `AgentCatalogPanel` | `AgentSelectionCard` facade to participant card | managed-Agent actions and navigation |
| `AgentSwitchDialog` | direct participant picker/card through Agent projection mapper | Agent selection and favorite effects |
| `AgentDetailsDialog` | direct definition-editor shell/identity fields; provider facade to neutral provider/model selector | persistence, versioning, runtime policy and Agent-only tabs |
| `ContextualAgentWorkspaceWindows` | `ChatWorkspacePanel` facade | contextual workspace load, send, approvals and refresh effects |
| `ProcessWorkspaceShell` | `ChatWorkspacePanel` facade from the existing Agent components reference | Process context publication and manager-chat orchestration |
| `AgentTeamMembersDialog` | `AgentSelectionCard` facade | team membership choice |
| `WorkflowCanvasEditor` | `ProviderModelSelector` facade | workflow provider binding |

The compatibility facades are retained because they preserve public Agent contracts used by multiple modules. They now map Agent records into typed neutral presentations and supply Agent-only render fragments/callbacks; they do not duplicate the neutral card, picker, thread, transcript, composer, provider, floating, or lifecycle-field implementations.

Superseded Agent-local card/list/history CSS owners were deleted after migration. No live consumer requires `Modules.LlmChats`, and no Simple Chat source, filter, tab, route, context button, API client, or SSE client was activated.

## Compact UI composition

- Primary surfaces: participant collection, conversation workspace, settings editor, and floating Agent chat.
- Supporting content: Agent-only actions, runtime badges, history, affinity and preparation fields remain composed around the primary neutral surfaces.
- Stats treatment: compact badges/counts only; no new metric cards.
- First viewport: the conversation or collection remains immediately usable at the 1600x1000 application target.
- Scroll owners: neutral picker/list/transcript/catalog body own collection scrolling; floating window chrome and settings page do not add competing nested page scroll.
- Dialog/window sizing: existing Agent dialog sizes and the 560x720 catalog / 760x720 chat geometry are preserved.

CP2 and CP3 browser evidence remained valid because SB08 made no production change after those checkpoints.
