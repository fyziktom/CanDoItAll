# C# Boundary Map

## Target Projects

### Existing, retained

- `CanDoItAll.Conversations.Components` — pure presentation models and reusable Razor components.
- `CanDoItAll.Modules.LlmChats` — domain/application contracts and durable event session.
- `CanDoItAll.Modules.AgentFramework` / `.Components` — Agent product and adapters.

### New

- `CanDoItAll.Modules.LlmChats.Ui` — Simple Chat product presentation mapping, page/dialog components, UI gateways, and operation projection reducer.
- `CanDoItAll.Conversations.Shell` — application-level floating catalog/window composition contracts and neutral host; no Agent or Simple Chat backend references.

## Ownership Rules

- Presentation components never load data or resolve providers.
- UI gateway services call LLM Chat application contracts, not EF or Web endpoints.
- The durable operation session remains owned by `CanDoItAll.Modules.LlmChats`.
- Agent and Simple Chat floating contributors own their product-specific lifecycle and actions.
- The neutral shell merges contributor snapshots and renders source-owned window descriptors.
- Web composes markers/hosts; it does not implement chat business logic.

## Forbidden Ownership

- No LLM Chat persistence in Razor components.
- No Agent execution records in neutral conversation presentation contracts.
- No `IServiceProvider` service location inside core UI behavior.
- No universal `ChatService` that branches on Agent versus Simple Chat.
- No new partial class as the final extraction boundary.
