# Lightweight LLM invocation and ordinary-chat foundation

## Architectural distinction

```text
Lightweight LLM invocation
  messages + model settings + response format
  -> provider runtime/driver
  -> response + usage

Ordinary LLM chat application
  transcript + conversation preferences + compaction
  -> lightweight LLM invocation per turn
  -> persisted assistant message

Agent execution
  agent identity + tools + memory + context + authority + approvals + finalizer
  -> agent runtime adapter (MAF)
```

These are three different application concepts. Ordinary chat is not an agent with every capability switched off.

## Reusable existing foundation

The repository already has:

- provider profile/model normalization and feature matrices;
- provider runtime descriptors, handles, pools, and dispatch lanes;
- SDK-neutral provider chat-completion driver contracts;
- OpenAI, Azure OpenAI, and Ollama driver implementations;
- credential resolution;
- provider usage/results and image attachments;
- provider health and model administration paths.

The new lightweight port should reuse these intentionally while avoiding the broad MAF provider gateway shape.

## Proposed contracts

```csharp
public interface ILlmInvocationPort
{
    Task<LlmInvocationResult> InvokeAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IStreamingLlmInvocationPort
{
    IAsyncEnumerable<LlmInvocationUpdate> InvokeStreamingAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default);
}
```

The request should own only inference concerns:

- provider/profile snapshot or stable reference resolved before dispatch;
- model;
- immutable system instructions;
- ordered repository-owned messages;
- bounded attachments;
- temperature/reasoning/max-output and supported model settings;
- JSON/text response format and optional schema;
- correlation/causation IDs;
- deadline/budget;
- streaming preference.

It must not contain:

- `AgentDefinition`;
- chat agent session state;
- capabilities/tools/skills/MCP;
- memory/RAG context;
- handoff/A2A;
- approvals/finalizer;
- workspace scope/authority;
- process or workflow product semantics;
- UI observation.

## Provider-backed implementation

Prefer an implementation above the provider runtime pool and `IProviderChatCompletionDriver`. Resolve credentials, dispatch limits, provider model compatibility, and retry/usage through existing provider infrastructure. Keep workflow mapping and future transcript persistence outside this adapter.

## Workflow migration

The ordinary workflow LLM node:

1. resolves its explicitly configured provider/model;
2. builds a lightweight request from immutable node instructions and workflow input;
3. invokes the port;
4. maps usage to workflow observations;
5. validates workflow-owned response schema;
6. returns the result.

It does not parse `projectId` from arbitrary payload to choose workspace scope.

## Future ordinary LLM chat

A later feature can add:

```csharp
public interface ILlmConversationService
{
    Task<LlmConversationTurnResult> SendAsync(
        LlmConversationTurnCommand command,
        CancellationToken cancellationToken = default);
}
```

This service may own:

- conversation/thread identity;
- persisted ordered transcript;
- provider/model preference;
- system prompt/profile;
- compaction/summarization policy;
- input attachments;
- streaming projection;
- usage/cost;
- retry/idempotency.

It delegates inference to `ILlmInvocationPort`. It receives no product tools or application-surface authority unless a future explicit feature defines a separate, reviewed context boundary.

## Performance proof

Measure or assert that lightweight invocation does not:

- enumerate capability catalogs;
- build workspace/file/MCP plugins;
- load memory or skills;
- construct MAF agent/session objects;
- create approval/finalizer tools;
- capture floating UI context.

## Failure and usage semantics

Use one provider result/usage source of truth. Streaming and non-streaming adapters must not double-count usage. Provider failures remain sanitized and typed. Cancellation/deadline must propagate without fabricating success usage.

## Ordinary-chat source-of-truth invariants

- The application conversation store is the canonical transcript. A provider-native conversation ID, cache key, or opaque session payload is integration state only.
- A provider-native session may accelerate continuation only when its envelope is compatible with provider, model, system profile, message fingerprint, and adapter schema. Incompatibility falls back to a tested canonical transcript replay or fails explicitly; it never silently drops messages.
- User and assistant turns are persisted atomically with idempotency keys. A retry cannot append the same assistant result twice.
- Usage/cost is attributed once to the invocation and projected into the conversation; the conversation service does not independently reparse provider usage.
- Summaries/compaction are derived artifacts with source message coverage and revision fingerprints. They do not overwrite canonical transcript history unless a future retention policy explicitly owns that decision.
- Product/UI context is absent by default. A future contextual ordinary-chat feature must capture an explicit, consented, independently authorized snapshot; it must not read the floating-agent registry ambiently.
- Adding tools, autonomous actions, approvals, handoffs, or execution authority is a transition to agent execution or another explicitly reviewed product, not a hidden expansion of ordinary chat.
