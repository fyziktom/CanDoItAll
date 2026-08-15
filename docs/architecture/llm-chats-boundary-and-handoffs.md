# LLM Chats Boundary And Future Handoffs

LLM Chats is a backend/API product boundary. The product project owns definitions, conversations,
durable operation rules, leases, event semantics, and ports. Persistence owns EF, PostgreSQL, provider
runtime adapters, and database transfer. Composition owns hosted dispatcher lifetime. Web owns HTTP,
authorization, OpenAPI, transport DTOs, server-owned API origin, and reuse of the shared SSE writer.

Turn admission and execution are deliberately separate. Admission commits the operation, active turn,
user message, and accepted event before returning `202 Accepted`. The hosted dispatcher claims work by
durable lease and performs provider I/O without holding the admission transaction. Completion commits
the assistant transcript, audit, terminal state, and terminal events. SSE replays only committed journal
events; its transient signal is a wake-up optimization, not an alternative source of truth.

The product and persistence projects must not depend on Web, Razor, shared UI components, Agent Core or
MAF execution, tools, skills, MCP, memory, processes, Workbench, Projects, Project Structure, or
Workspace state. Reusing provider-neutral LLM/model contracts and provider runtime adapters does not
activate an agent. The generic ordinary-conversation implementation remains scoped inside the product
engine and is not globally registered.

## Deferred Ownership Handoffs

| Future work | Owning boundary | Required constraints |
|---|---|---|
| Shared-component isolation | `CanDoItAll.Components`, in its own reviewed component bundle | Provide reusable Razor wrappers and styling without moving LLM Chat domain, persistence, or API behavior into the component library. |
| LLM Chats UI and floating surface | A later Web/UI integration bundle consuming the public application/API contracts | Start only after the backend final gate; use existing component wrappers; keep operation state explicit; reconnect SSE by durable cursor; never execute providers or persist transcripts in components. |
| Project Structure context | A later Projects/Workbench application adapter bundle | Resolve authorized project context outside LLM Chats and pass only an explicit, bounded product input through a new reviewed contract; do not add Projects/Workbench references or ambient context to the LLM Chat product. |
| Enterprise chatbot channels | A later `LlmChatDeployment` aggregate and transport-adapter bundle | Own tenant/deployment/channel/participant identity, ingress authentication, conversation-create idempotency namespace, moderation, quotas, retention, residency, legal hold, and human handoff. Do not pre-stage nullable fields on internal definitions or conversations. |

Until those bundles exist, no Razor page, floating chat, shared-component refactor, Project Structure
context, public widget, external participant, or deployment channel is part of LLM Chats. Conversation
creation through HTTP remains non-idempotent because a safe caller-key namespace belongs to
`LlmChatDeployment`; turn admission remains retry-safe through its caller-supplied operation ID.
