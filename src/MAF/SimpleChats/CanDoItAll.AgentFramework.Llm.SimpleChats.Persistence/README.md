# CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence

## Purpose

PostgreSQL, canonical ordinary-conversation, provider-runtime, database-profile fencing, execution
leases, durable stream events, cancellation, immutable pricing evidence, and database-transfer
adapters for the MAF Simple Chats application boundary.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Focused build:

```powershell
dotnet build src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.csproj --configuration Release
```

## Boundaries

The project owns EF configurations/repositories, the shared-context LLM Chat unit of work, the
PostgreSQL `ILlmConversationStore`, provider/model resolution through the canonical runtime profile
source, the product conversation engine, runtime-generation and execution-lease adapters, durable event
journal storage, retention, operation cancellation, and complete database-transfer participation. The
conversation store uses the same scoped `SimpleChatsDbContext` as the owning unit of work; it must not create an
independent context for canonical transcript mutations. Provider I/O runs after admission commit and
outside database transactions.

The runtime model explicitly contains the nine existing Simple Chats entities and the definition-create receipt. History
outbox/projection writes enlist an independent History context in the owner's actual connection and
transaction through infrastructure coordination. Ordinary reads keep independent owner factories.
The complete application model remains the migration authority. Opaque GUID stamping is not applied
to the existing numeric Simple Chats revisions/concurrency values.

It does not reference Web/Razor, Agent module execution or UI, tools, skills, MCP, memory, processes,
Workbench, or other product UI implementations. The generic ordinary-conversation service is
constructed only inside the scoped runtime engine; it is not globally published. The file conversation
store is not registered in production. See [LLM Chats Product And API](../../../../docs/llm-chats-api.md).

The existing `LlmChats_*` tables and their model snapshot are advanced by the append-only PostgreSQL
migration chain beginning with `20260814163458_AddLlmChats`; pricing evidence was added by
`20260817183339_AddSimpleChatInvocationPricingEvidence`. Migration bootstrap, pending-model validation, event
retention, and database transfer must stay aligned whenever this persistence boundary changes.
## Definition Creation Receipts

The definition writer implements `ILlmChatDefinitionCreateReceiptService` alongside the ordinary
create API. A normalized producer, actor, history namespace, and server-issued intent ID identify
one creation. Its versioned semantic fingerprint covers the submitted definition settings. A replay
returns the original definition ID, revision, concurrency value, and creation time; later human edits
remain untouched. A changed payload under the same intent is rejected. Receipt lookup discloses no
definition settings or conversation content, and current provider metadata is not part of replay identity.

PostgreSQL claims the receipt and writes the definition/revision in the same owner transaction.
The intent primary key remains immediate; the original-revision foreign key is initially deferred so
the claim can precede the referenced revision. Rollback removes both. Post-commit acknowledgement
failure retains the committed receipt. The unit of work also marks an outer transaction rollback-only
when a nested operation fails, even if its caller catches that failure.

`20260910163827_AddLlmChatDefinitionCreateReceipts` advances the complete canonical model. It adds
one table without copying or changing saved definitions. Downgrade is allowed only while that table
is empty. Once a receipt is retained, its migration refuses downgrade: keep the schema or restore a
reviewed pre-admission backup without replaying effects that already occurred. A code revert alone
does not undo database or external effects.

The database-transfer document carries validated receipt identities, fingerprints, and original
revision references unchanged. Transfer to a receipt-free target remains supported. Replacement of
a target containing retained creation receipts is rejected by preview and final validation, so retries
cannot erase deduplication history. This owner guarantee does not close the application's separate
cross-owner transfer coordination or the calling agent's durable admission/checkpoint obligations.
