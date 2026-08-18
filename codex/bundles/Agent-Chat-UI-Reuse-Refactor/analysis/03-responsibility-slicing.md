# Responsibility slicing

## Current AgentChatPanel cluster

| Responsibility | Current owner | Target owner in Phase 1 |
|---|---|---|
| participant header/avatar/name | AgentChatPanel / ChatWorkspacePanel | neutral presentation component |
| thread rail/search/items | AgentChatPanel | neutral presentation component |
| transcript/message bubble/markdown | ChatWorkspacePanel | neutral presentation component |
| composer chrome/draft/send state | ChatWorkspacePanel | neutral presentation component |
| workspace/session loading | AgentChatPanel code-behind | Agent module adapter/orchestrator |
| send/cancel/run orchestration | AgentChatPanel code-behind | Agent module adapter/orchestrator |
| approvals and execution stream | ChatWorkspacePanel + Agent services | Agent facade slot/component |
| voice | ChatWorkspacePanel + voice service | Agent facade slot/component |
| attachments | AgentChatPanel + staging service | Agent facade slot/component |
| prompt gallery | AgentChatPanel | Agent facade slot/component |
| hidden context parsing | ChatWorkspacePanel | Agent presentation adapter |
| runtime dialogs | ChatWorkspacePanel | Agent facade slot/component |

## Current participant-list cluster

| Responsibility | Target |
|---|---|
| card/list visual identity | neutral |
| generic selected/busy/disabled state | neutral |
| generic badges and actions | neutral |
| agent status/workload/capability semantics | agent adapter |
| favorites persistence | agent adapter |
| team filtering/tree | AgentCatalogPanel |
| managed-agent identity actions | AgentCatalogPanel |

## Current settings cluster

| Responsibility | Target |
|---|---|
| editor shell/tabs/layout | neutral composition where source-neutral |
| name/summary/avatar/instructions fields | neutral |
| provider/model choice presentation | neutral options + Agent adapter |
| temperature | neutral optional field |
| thinking effort | Agent adapter or neutral option slot |
| approvals, Memory, Images, capabilities, tools, skills, governance | AgentDetailsDialog |
| save/load/delete/version behavior | AgentDetailsDialog code-behind |

## Current floating cluster

| Responsibility | Target |
|---|---|
| window/catalog/list presentation | neutral seam |
| active-chat retention/capacity fields | neutral settings component |
| coordinator, handles, context, affinity, history | Agent host |
| prepared-agent metadata budget | Agent settings |
| multi-source catalog/filter | deferred |
