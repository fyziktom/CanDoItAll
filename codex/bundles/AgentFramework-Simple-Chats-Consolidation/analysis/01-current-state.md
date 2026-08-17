# Current state

The complete current-state inventory is architecture/00-csharp-current-state-inventory.md and inventories/01-source-surface-inventory.md.

## Summary

- The three current Modules.LlmChats projects contain about sixteen thousand source lines.
- The core project mixes domain, Application, ports, operations, and DI.
- Persistence mixes EF/data-profile infrastructure with provider runtime and conversation engine construction.
- UI mixes reusable components/gateways with /chats routing and shell navigation.
- Existing AgentFramework.Llm.* and Conversations.* projects already own reusable generic chat foundations.
- Agent usage is file-backed and price-aware; Simple Chat attempt usage is EF-backed but lacks complete status/cost/pricing provenance.
- /agents has an established SecondaryTabs dashboard; /chats is a separate full page.
- No current LlmChats project cycle was found; unrelated pre-existing AgentFramework cycles must not grow.

## Implication

The work is a boundary extraction plus data/read-model/UI consolidation, not a namespace-only rename.

