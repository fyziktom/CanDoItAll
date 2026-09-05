# State and intent contract

| State | Authority / lifetime | Boundary |
|---|---|---|
| Workspace section, selected agent/team, Simple Chat and usage selection | Page/workspace coordination | Typed semantic state, current route mapping only where it already applies |
| Requested route agent/team | Compatibility route input | Reconciled with catalog readiness; must not silently resolve to a different entity |
| Open editor target | Host/workspace coordination | Existing target or create target with per-instance identity; distinct from selected catalog item |
| Editor section | Explicit semantic section for this editor | Enum and callback mapped to current Tabs index |
| Mutable draft/edit context, expected version, local validation | One editor instance/session | Supplied by load operation; copied as needed; retained across same-target section changes |
| Local search/expansion/hover/scroll/busy/confirmation | Owning UI instance | Not automatically committed URL state |
| Accessible chat context/readiness | Workspace projection from loaded catalog/selection | Preserve existing context access callback and AgentChatContextSurfaceProvider inputs |

Typed catalog intents distinguish select, open, create, edit team/members, delete, repair/reload and chat. They contain meaningful IDs/payloads, not URLs, component instances or global service access. Avoid a generic command dispatcher when a small callback family is clearer.

Load requests and results carry enough target/session identity to reject stale completions. Busy flags alone are not identity. Cancellation limits work where supported; stale-result suppression remains required even when an API cannot cancel.

Retain current /agents query keys: tab, agentId, teamId, simpleChatView, definitionId, conversationId, usageScope. Existing mappings decide when NavigateTo uses replace; this child adds no outbound URL update for ordinary local selection/section changes.

Use the [editor contract](09-editor-session-and-host-contract.md) for lifetime, reset, result and draft rules. Unknown/new bookmark policies remain in the [handoff](../plan/03-sandbox-and-navigation-handoff.md).
