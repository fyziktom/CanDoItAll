# SB07 — Provider-neutral streaming contracts and drivers

Status: **Locked**  
Proof tier: **Governed**  
Depends on: **SB06**

## Outcome

Add true provider-neutral incremental output without coupling Simple Chats to concrete provider SDKs or breaking existing complete-response callers.

## Owned requirements

- `RQ-019` — Provide an additive provider-neutral incremental LLM invocation port without breaking existing non-streaming callers.
- `RQ-020` — Implement true incremental streaming for OpenAI, Azure OpenAI, and Ollama, with a deterministic fallback policy.
- `RQ-021` — Retry a stream only before its first emitted delta and never after partial output is externally visible.
- `RQ-026` — Audit actual provider attempts with deterministic outcomes shared by direct and recovery reducers.

## Scope

- Add ILlmStreamingInvocationPort and immutable transport-neutral streaming update contracts beside ILlmInvocationPort.
- Add an optional provider streaming chat-completion capability contract in AgentFramework.Providers.
- Implement ProviderBackedLlmStreamingInvocationAdapter in Llm.ProviderRuntime.
- Implement true streaming for OpenAI Chat Completions/Responses, Azure OpenAI, and Ollama at current HTTP driver boundaries.
- Provide a bounded complete-response fallback that emits one delta and one completion when policy allows.
- Retry only before the first externally visible non-empty delta; after a delta, surface typed failure without reissuing.
- Record each actual provider dispatch attempt with monotonic ordinal and deterministic outcome.
- Keep credentials, raw frames, and raw exception text out of public updates.

## Explicit non-goals

- No SSE yet.
- No event/transcript persistence yet.
- No agent or tool streaming.

## Current-source entry points

- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/LlmInvocationContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderCapabilityContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/OllamaProviderDriver.cs`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Add true provider-neutral incremental output without coupling Simple Chats to concrete provider SDKs or breaking existing complete-response callers.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Optional provider capability plus provider-neutral adapter; existing non-streaming port remains compatible.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Fragmented SSE/NDJSON protocol-parser tests.
- Retry-before-first-delta and no-retry-after-first-delta tests.
- Cancellation, timeout, malformed-frame, empty-response, usage, and redaction tests.

Critical database/lifecycle claims require real PostgreSQL proof; mocks alone are supporting evidence.

## Partial Class Policy

No new production partial file may be the final boundary. A temporary extraction partial is allowed only
with a named deletion step inside this same subbundle and proof that it is removed before closure.

## Architecture Proof Required

- before/after owner and dependency evidence;
- direct test of the new owner;
- negative test that fails against the previous shallow implementation;
- source assertion that superseded behavior is no longer reachable;
- no cycle and no forbidden dependency;
- actual commands and commit SHA in the proof manifest.

## Validation budget

Follow `test-budget.json` and `plan/04-test-budget-and-gates.md`. During this work unit:

- no solution-wide test command;
- no unfiltered Unit or Integration project;
- no Playwright/LiveProcess/LongRunning/Quarantined gate;
- at most the declared focused command budget;
- do not rerun an unchanged failed command without a concrete fix or diagnostic reason.

## Acceptance checklist

- [ ] Existing ILlmInvocationPort callers remain source- and behavior-compatible.
- [ ] OpenAI, Azure OpenAI, and Ollama produce incremental text through one provider-neutral contract.
- [ ] A non-incremental supported driver uses a deterministic single-delta fallback or typed unsupported result.
- [ ] No automatic retry occurs after the first emitted delta.
- [ ] Every actual provider dispatch attempt receives a distinct monotonic audit ordinal and deterministic outcome.
- [ ] Streaming failures expose no credentials, raw frames, or raw provider errors.

## Reopen triggers

- a provider protocol cannot yield deterministic terminal usage/result
- streaming contracts acquire Web/SSE dependencies
- non-streaming workflow behavior regresses

## Progression decision

Unlock SB08 after this work unit passes, unless a checkpoint applies.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
