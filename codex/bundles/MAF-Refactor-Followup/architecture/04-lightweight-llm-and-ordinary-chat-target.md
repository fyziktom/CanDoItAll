# Lightweight LLM and ordinary chat target

## Three separate products

```text
Stateless LLM invocation
  -> ILlmInvocationPort
  -> provider runtime/driver

Ordinary LLM conversation
  -> ILlmConversationService
  -> canonical transcript
  -> ILlmInvocationPort per turn

Agent execution
  -> tools, memory, context, authority, approvals, handoffs, MAF
```

## Stateless port requirements

- immutable ordered messages
- explicit provider/model selection policy
- bounded text and attachments
- cancellation + deadline
- correlation/operation ID
- JSON/JSON Schema response contract
- typed sanitized failures
- usage including cached/reasoning tokens
- exactly one safe empty-response retry for a non-actionable stateless call
- no agent/session/tool/workspace/process concepts

## Ordinary conversation requirements

- application transcript is canonical
- provider conversation state is optional acceleration only
- optimistic transcript revision/concurrency control
- provider/model snapshot and explicit switch policy
- no tools, memory, approvals, finalizers, or agent catalog
- future context-window/summarization policy is explicit and non-destructive by default

`SB15` may be deferred. `SB14` is required because workflows already use the stateless port.
