# SB12 semantic invariants

## SBI-12-01 — admission is not provider execution

- Expected behavior: the HTTP turn endpoint returns `202 Accepted` after durable admission and the
  hosted dispatcher owns provider execution under a recoverable lease.
- Disallowed shallow implementation: calling `ILlmChatConversationEngine.SendAsync` or an executor
  from the request-owned application/API path.
- Passing proof: the architecture guard requires the dispatcher signal, hosted registration, and
  `Results.Accepted`, and rejects direct engine/executor ownership in the request path.

## SBI-12-02 — canonical transcript mutations share the owning transaction

- Expected behavior: `EfLlmConversationStore` uses the scoped `AppDbContext` shared by the LLM Chat
  unit of work.
- Disallowed shallow implementation: creating an independent context inside the canonical store or
  using post-commit callbacks to persist evidence.
- Passing proof: the guard requires the scoped context, rejects `IDbContextFactory` and
  `CreateDbContextAsync` in that store, and permits post-commit work only for event-reader notification.

## SBI-12-03 — the durable journal is the only LLM Chat SSE authority

- Expected behavior: Web reuses the shared `ServerSentEventResponseWriter` to replay committed journal
  sequences, gaps, heartbeats, and one terminal event.
- Disallowed shallow implementation: a product-owned or endpoint-local SSE writer, or a transient
  signal treated as durable history.
- Passing proof: the SSE guard validates the durable contract and the architecture guard requires one
  production writer plus its reuse by `LlmChatOperationsApi`.

## SBI-12-04 — deferred work stays outside the product

- Expected behavior: no Razor/UI/floating-chat/Project Structure integration and no dormant enterprise
  deployment fields exist in the LLM Chat product or persistence projects.
- Disallowed shallow implementation: pre-staging tenant, participant, channel, moderation, quota,
  retention, residency, legal-hold, or handoff fields on internal definitions/conversations.
- Passing proof: the architecture guard checks source, project dependencies, and all changed paths
  since reviewed feature commit `16b6aa4b60dc88a6134dd6c9c9e634c064ac5847`. The architecture handoff
  document assigns each future concern to a separate owner.

## SBI-12-05 — proof remains budgeted

- Expected behavior: SB00–SB12 use only focused tests and affected builds; SB13 owns the single stable
  solution gate.
- Disallowed shallow implementation: an unfiltered Unit/Integration project, a solution test before
  SB13, or a forbidden Playwright/live/long/quarantined lane.
- Passing proof: the test-policy validator passes every recorded subbundle command. SB12 ran no test or
  build because it changed documentation and guard scripts only.
