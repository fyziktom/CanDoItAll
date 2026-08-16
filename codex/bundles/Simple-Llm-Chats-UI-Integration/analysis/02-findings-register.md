# Findings Register

| ID | Severity | Finding | Decision |
|---|---|---|---|
| F-001 | positive | A real backend-neutral Razor boundary now exists | Retain the project and build the Simple Chat UI on it; do not merge it back into AgentFramework.Components. |
| F-002 | positive | Agent components are thin adapters over neutral presentation contracts | Preserve the adapter direction and existing public Agent component contracts. |
| F-003 | positive | Committed closure records broad component and targeted browser parity | Use as historical evidence, then reconcile its missing artifact paths in SB01. |
| F-004 | positive | User manually confirmed settings changes and Project Structure Agent chat still work | Record as user-supplied regression evidence; do not infer untested floating, cancellation, or streaming cases. |
| F-005 | high | Governed proof paths are ignored and absent while closure/checksums cite them | Reconcile and commit bounded proof or downgrade/remove unsupported claims before CP0. |
| F-006 | medium | Several allegedly immutable presentation records retain caller-owned list instances | Copy and validate collections at construction boundaries. |
| F-007 | low | ConversationPresentationKey validates nonblank only | Normalize, bound, and prove opaque key round-trips without encoding backend authority. |
| F-008 | high | Pending message rendering is hard-coded as a black User Sending bubble | Introduce role-driven transient messages before consuming response deltas. |
| F-009 | high-security | Markdown disables raw HTML but has no explicit safe URI-scheme policy | Add a tested safe-link/image policy before rendering broader untrusted LLM output. |
| F-010 | medium | ConversationActiveList still owns Agent-shaped Open and Stop actions | Use declared generic actions and map Agent Open/Stop through its adapter. |
| F-011 | high | Conversation projections expose HasActiveTurn but not the operation identity needed to reconnect | Add ActiveOperationId and prove exact profile-fenced ownership. |
| F-012 | positive | A reusable application-level durable event stream session already exists | Use it directly from server-side UI through a UI adapter; do not loop back through HTTP. |
| F-013 | expected-gap | No Simple Chat Razor module, route, navigation, or component assembly is registered | Create a dedicated UI project after CP1. |
| F-014 | expected-gap | The application shell still owns an Agent-only floating host and launcher label | Introduce a neutral contributor-based floating shell only after the main Simple Chat page passes CP2. |
| F-015 | high-security | API policies exist, but direct in-process UI calls need equivalent read/manage/execute authorization gates | Add a typed UI authorization facade; do not assume page visibility grants all actions. |
| F-016 | scope-boundary | Simple Chats still have no typed project-context aggregate or source adapter | Leave the existing ContextActions slot unused and plan Project Structure context as a later bundle. |
| F-017 | historical-test | The predecessor Stable closure recorded three unrelated LLM Chat integration failures | Classify exact current cases without repeatedly running Stable; one final Stable gate remains authorized in SB12. |

## Source Anchors

### F-001 — A real backend-neutral Razor boundary now exists
- `repo://src/UI/CanDoItAll.Conversations.Components/CanDoItAll.Conversations.Components.csproj`
- `repo://src/UI/CanDoItAll.Conversations.Components/README.md`

### F-002 — Agent components are thin adapters over neutral presentation contracts
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentParticipantPresentationMapper.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentConversationPresentationMapper.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`

### F-003 — Committed closure records broad component and targeted browser parity
- `repo://codex/bundles/Agent-Chat-UI-Reuse-Refactor/EXECUTION-PROGRESS.md`
- `repo://codex/bundles/Agent-Chat-UI-Reuse-Refactor/CLOSURE-AUDIT.md`

### F-004 — User manually confirmed settings changes and Project Structure Agent chat still work
- `input://inputs/01-user-request.md`

### F-005 — Governed proof paths are ignored and absent while closure/checksums cite them
- `repo://.gitignore`
- `repo://codex/bundles/Agent-Chat-UI-Reuse-Refactor/CHECKSUMS.sha256`
- `repo://codex/bundles/Agent-Chat-UI-Reuse-Refactor/bundle-status.json`

### F-006 — Several allegedly immutable presentation records retain caller-owned list instances
- `repo://src/UI/CanDoItAll.Conversations.Components/Presentation/ConversationParticipantPresentation.cs`
- `repo://src/UI/CanDoItAll.Conversations.Components/Presentation/ConversationThreadPresentation.cs`
- `repo://src/UI/CanDoItAll.Conversations.Components/Presentation/ConversationProviderOption.cs`

### F-007 — ConversationPresentationKey validates nonblank only
- `repo://src/UI/CanDoItAll.Conversations.Components/Presentation/ConversationPresentationKey.cs`

### F-008 — Pending message rendering is hard-coded as a black User Sending bubble
- `repo://src/UI/CanDoItAll.Conversations.Components/ConversationTranscript.razor`
- `repo://src/UI/CanDoItAll.Conversations.Components/ConversationMessageBubble.razor`

### F-009 — Markdown disables raw HTML but has no explicit safe URI-scheme policy
- `repo://src/UI/CanDoItAll.Conversations.Components/ConversationMarkdownRenderer.razor`

### F-010 — ConversationActiveList still owns Agent-shaped Open and Stop actions
- `repo://src/UI/CanDoItAll.Conversations.Components/ConversationActiveList.razor`
- `repo://src/UI/CanDoItAll.Conversations.Components/Presentation/ConversationActiveItemPresentation.cs`

### F-011 — Conversation projections expose HasActiveTurn but not the operation identity needed to reconnect
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Ports/LlmChatExecutionPorts.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationContracts.cs`

### F-012 — A reusable application-level durable event stream session already exists
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationEventStreamSession.cs`
- `repo://src/App/CanDoItAll.Web/Api/Streaming/LlmChatOperationEventReplayReader.cs`

### F-013 — No Simple Chat Razor module, route, navigation, or component assembly is registered
- `repo://src/App/CanDoItAll.Composition/ModuleAssemblies.cs`
- `repo://src/App/CanDoItAll.Web/Components/Routes.razor`

### F-014 — The application shell still owns an Agent-only floating host and launcher label
- `repo://src/App/CanDoItAll.Web/Components/Layout/MainLayout.razor`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor`

### F-015 — API policies exist, but direct in-process UI calls need equivalent read/manage/execute authorization gates
- `repo://src/App/CanDoItAll.Web/Api/ApiAuthorizationPolicies.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs`

### F-016 — Simple Chats still have no typed project-context aggregate or source adapter
- `repo://docs/architecture/llm-chats-boundary-and-handoffs.md`

### F-017 — The predecessor Stable closure recorded three unrelated LLM Chat integration failures
- `repo://codex/bundles/Agent-Chat-UI-Reuse-Refactor/CLOSURE-AUDIT.md`
