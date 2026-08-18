# SB07 semantic invariant contract

Changed-file hashes: `bundle://proof/SB07/changed-files.sha256`.
Positive command: `bundle://proof/SB07/transcripts/01-current-head-gates.md`.
Negative/source command: `bundle://proof/SB07/transcripts/02-negative-and-source-guards.md`.

## SBI-07-01 — additive true incremental provider boundary

- Source raw note: “Add true provider-neutral incremental output without coupling Simple Chats to
  concrete provider SDKs or breaking existing complete-response callers.”
- Expected behavior: the unchanged `ILlmInvocationPort` remains green; a separate immutable
  `ILlmStreamingInvocationPort` receives real OpenAI, Azure OpenAI, and Ollama deltas before provider
  completion.
- Disallowed shallow implementation: buffer a completed response and label arbitrary string slices
  as provider streaming, or leak provider/HTTP JSON types through the LLM abstraction.
- Failing-first proof: the SB06 source guard finds no stream port, provider capability, or adapter;
  the old boundary cannot satisfy the fragmented protocol tests.
- Passing proof: `ProviderStreamingDriverTests` reads byte-fragmented OpenAI/Azure SSE and Ollama
  NDJSON, while `ProviderBackedLlmInvocationAdapterTests` remains part of the 86/86 union.
- Changed source: Llm invocation contracts; provider capability/request contracts and registry;
  OpenAI/Azure/Ollama drivers; provider streaming protocol; provider runtime adapter and registration.
- Production assertion: drivers use `ResponseHeadersRead`; provider JSON terminates inside Providers;
  the runtime channel yields neutral deltas while its dispatch task still owns the lane.
- Red-team negative: OpenAI Responses reasoning and Ollama `thinking` fields are present in fixtures
  but absent from public deltas.
- Downstream dependency: SB08 may consume the neutral update sequence but must not parse provider
  frames or replace the provider runtime owner.

## SBI-07-02 — fallback, retry, bounds, and cancellation

- Source raw note: “Retry only before the first externally visible non-empty delta and never after
  partial output is externally visible”; completed-only fallback must be deterministic and bounded.
- Expected behavior: a profile/driver that permits incremental delivery streams it; otherwise a
  supported completed driver emits one delta and one completion labelled `CompletedFallback`.
  Failure/empty completion may retry once only before any delta.
- Disallowed shallow implementation: restart a provider after emitting partial text, splice two
  attempts, buffer without bounds, or leave a producer running after the consumer disconnects.
- Failing-first proof: the SB06 completed-only boundary cannot express retry visibility, delivery
  mode, or a terminal partial-stream failure.
- Passing proof: focused tests cover retry-before-delta, exactly one call after a visible delta,
  completed fallback, empty completion, deadline, and caller cancellation.
- Changed source: `ProviderBackedLlmStreamingInvocationAdapter.cs` and streaming update contracts.
- Production assertion: retry is guarded by `!emittedDelta`; accepted assistant characters are capped
  at the canonical message bound; provider frames/events and the channel are bounded; enumerator
  disposal cancels and awaits the producer.
- Red-team negative: a driver yields `partial` then throws; the result is a terminal failure with one
  dispatch and no retry.
- Downstream dependency: SB08 must preserve this attempt boundary when persisting deltas and terminal
  events.

## SBI-07-03 — actual attempt ordinal and usage audit

- Source raw note: “Record each actual provider dispatch attempt with monotonic ordinal and
  deterministic outcome.”
- Expected behavior: each real dispatch has a started update and exactly one completed/failed outcome;
  durable audit stores its ordinal and attempt-local usage. Final completion retains aggregate usage.
- Disallowed shallow implementation: one audit row for a two-dispatch retry, duplicate ordinal 1,
  aggregate both attempts into the successful row, or create an audit attempt for runtime preparation
  that never dispatched a provider.
- Failing-first proof: the prior completed audit hard-codes ordinal 1 and has no streaming retry
  terminal updates.
- Passing proof: `StreamAsync_retries_before_the_first_delta_and_exposes_monotonic_attempt_outcomes`
  proves ordinals 1/2; `Streaming_audit_records_each_actual_attempt_with_its_own_usage_and_ordinal`
  proves failed and succeeded durable rows with distinct usage.
- Changed source: streaming adapter/update contracts and `AuditedLlmChatStreamingInvocationPort.cs`.
- Production assertion: the audited consumer records terminal updates through
  `ILlmChatOperationEvidenceSink`; runtime-preparation rejection is a typed exception before attempt
  start and creates no false row.
- Production artifact matrix: see `bundle://proof/SB07/manifest.md`.
- Red-team negative: the audit test would fail if the completed row contained the aggregate retry
  usage rather than the second attempt’s usage.
- Downstream dependency: SB08 recovery and event reducers must consume these same ordinals/outcomes,
  not invent a second attempt counter.

## SBI-07-04 — public failure redaction and terminal validation

- Source raw note: “Keep credentials, raw frames, and raw exception text out of public updates.”
- Expected behavior: malformed/missing terminal frames and provider failures become stable typed state;
  raw exceptions are available only to structured internal logging with provider id/kind/model,
  correlation id, ordinal, and partial-output state.
- Disallowed shallow implementation: copy HTTP error bodies, JSON frames, credentials, provider
  exception messages, or hidden chain-of-thought into public updates.
- Failing-first proof: before the typed streaming hierarchy, no neutral terminal failure existed.
- Passing proof: malformed raw-frame, raw-provider-secret, OpenAI reasoning, and Ollama thinking tests
  assert that those strings do not appear publicly.
- Changed source: provider streaming protocol, streaming adapter, immutable update contracts.
- Production assertion: parser errors use stable messages; adapter failure updates contain only enum,
  usage, retry state, ordinal, and timestamp; structured logs retain the exception object.
- Red-team negative: frames deliberately contain `not-json-and-secret`, `hidden reasoning`, and
  `private chain`; none is emitted.
- Downstream dependency: SB09/SB10 must serialize only the neutral fields and retain the same
  redaction boundary for SSE/API responses.

## Anti-stub result

The scoped production audit reports no TODO/FIXME, `NotImplementedException`, fixture-specific or
test-only branch, or stub marker. Tests drive real provider `HttpClient` streams through fragmentation
and the production runtime adapter; no production-only signal is manually seeded.
