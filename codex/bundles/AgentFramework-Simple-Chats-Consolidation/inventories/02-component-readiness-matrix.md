# Component readiness matrix

| Need | Existing component | Decision |
|---|---|---|
| Agent page peer modes | BaseLib SecondaryTabs | Add Simple Chats after Agents; centralize keys. |
| Simple Chat inner modes | BaseLib Tabs | Preserve Conversations and Definitions. |
| Simple Chat definition settings | BaseLib Dialog + Tabs (ModalCompact) + ConversationDefinitionEditorShell | Use Identity, Runtime, and Output and revision tabs with validation above panels and stable footer actions. |
| Agent/Simple Chat avatars | Existing inline AgentDetailsDialog Avatar/Dialog/upload/AI composition | Extract one selector into AgentFramework.Components; both editors consume it through typed value/generation seams. |
| Provider/model distributions | CanDoItAll.Components.Charts CdaChart | Reuse typed chart series/options. |
| Scope selection | BaseLib typed segmented/select control selected through Components MCP at SB09 | Do not hand-build raw div/button state. |
| Usage details | Existing Agent usage dialogs/data grids | Generalize neutral provider/model/consumer inputs; preserve dialog scroll. |
| Metrics | Existing compact metric/stat components | Catalog stats remain separate from scoped usage stats. |
| Loading/empty/error | Existing BaseLib/AppComponents states | Add unknown/unpriced and partial-source failure states. |
| Main conversation UI | LlmChatConversationWorkspace plus Conversations.Components | Move reusable body to MAF Components. |
| Floating conversation UI | Conversations.Shell plus LlmChatConversationShellContributor | Preserve source-neutral contributor; register once. |
| Page layout | PageScaffold/PageHeader in Agent module | Do not render old LlmChatsPage scaffold inside Agent page. |

Components MCP calls failed during preparation and follow-up repair with Transport closed. SB07/SB09 must retry and record the exact recommendation before changing shared controls; the locally verified AgentDetailsDialog BaseLib composition is the fallback evidence, not permission to duplicate it.
