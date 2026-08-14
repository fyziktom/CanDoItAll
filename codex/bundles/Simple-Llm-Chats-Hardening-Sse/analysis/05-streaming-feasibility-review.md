# Streaming feasibility review

## Feasibility

The repository already has the HTTP mechanics needed for robust SSE:

- bounded replay streams;
- monotonic sequence cursors;
- `Last-Event-ID` and query cursor handling;
- replay-gap events;
- heartbeats;
- immediate flushing;
- proxy-buffering disablement;
- profile-generation lifetime cancellation.

What is missing is upstream true provider streaming and durable operation-event ownership.

## Why polling is insufficient

Polling only provides operation status after durable milestones. It cannot make a slow local LLM feel
responsive, and repeatedly fetching an ever-growing response is inefficient. Slicing a completed
response into synthetic chunks would be misleading and would not shorten time-to-first-token.

## Provider protocol plan

- OpenAI/Azure OpenAI: use the provider-supported streaming form of the chosen Chat Completions or
  Responses API and parse server events incrementally.
- Ollama: request streaming and parse its newline-delimited incremental JSON.
- Other drivers: support a documented completed-response fallback that emits one text delta and a
  terminal completion; never claim it is token streaming.

Provider protocol parsing stays in provider driver projects. The product module consumes a provider-
neutral stream contract.

## Retry boundary

A provider dispatch may retry only while no user-visible delta has been accepted into the operation
event journal. After the first delta, a transport failure is terminal/recoverable; silently retrying
would duplicate or splice text.

## Canonical-state boundary

- Deltas are non-canonical operation events.
- The stream accumulator is bounded and reconstructable from event rows while the operation is live.
- The final assistant text is committed exactly once with terminal success.
- A failed/cancelled operation does not create a canonical assistant transcript message.
- Partial text may be retained as redacted diagnostic/event evidence according to retention policy, but
  must never masquerade as a completed assistant answer.
