# Generic conversation adapter

## Reuse target

The product engine reuses the existing `LlmConversationService` and its transcript invariants.

## Required additive generic changes

### Caller-supplied conversation identity

Add an optional caller-supplied conversation ID to `LlmConversationStartRequest`, preferably as an
init-only property to preserve existing constructors. When absent, current GUID generation remains.

The product service uses the supplied ID so product metadata and transcript can be created in one
database transaction.

### Caller-supplied turn identity

Add an optional caller-supplied turn ID to `LlmConversationTurnRequest`, also backward compatible.
When absent, current GUID generation remains.

The product service sets `TurnId = LlmChatOperationId`. User and assistant entries therefore carry the
persistent operation identity.

### No product concepts in the generic contracts

Do not add:

- definition IDs;
- organization IDs;
- database profile generation;
- API idempotency keys;
- chatbot deployments;
- Project Structure context;
- operation status.

Those belong in the product module.

## Product engine composition

`ILlmChatConversationEngine` is a product-owned narrow façade declared in the domain project. Its
implementation belongs to `CanDoItAll.Modules.LlmChats.Persistence` and creates or wraps a generic
`LlmConversationService` with:

- `EfLlmConversationStore`;
- existing context-window policy;
- a product-only profile-fenced invocation adapter;
- existing `TimeProvider`.

The domain project does not reference the `Llm.Conversations` implementation project. Composition
registers only the named product engine; it does not register the generic service as a general
production interface. `ILlmInvocationPort` registration is owned by an idempotent extension in
`Llm.ProviderRuntime`; the Workflows runtime calls the same extension and then registers only its own
workflow invoker.

## Conversation creation transaction

1. Create product conversation ID.
2. Begin the module unit of work.
3. Resolve and validate the immutable definition revision.
4. call generic `StartAsync` with the supplied conversation ID and revision system prompt;
5. insert product conversation metadata with the same ID;
6. commit.
7. On any failure, neither half remains.

## Turn saga

1. admit/reuse persistent operation;
2. set operation scope and runtime lease;
3. call generic `SendAsync` with turn ID equal to operation ID;
4. generic service admits and completes/compensates transcript;
5. append invocation audit;
6. finalize operation;
7. if a crash occurs after transcript completion, reconcile using the shared turn ID.

This is an explicit recoverable saga. Do not claim that the provider call and all database records form
one ACID transaction.
