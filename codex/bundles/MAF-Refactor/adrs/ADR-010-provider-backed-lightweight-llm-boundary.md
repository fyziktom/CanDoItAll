# ADR-010: Build lightweight LLM invocation above provider runtime, below agent execution

## Status

Accepted for implementation in SB16.

## Context

Ordinary workflow LLM calls currently enter the full agent runtime. The repository already owns SDK-neutral provider chat drivers and provider runtime lifecycle/dispatch infrastructure.

## Decision

Create SDK-free lightweight LLM ports implemented over provider runtime/driver infrastructure. Do not implement the port through `MafAgentRuntime` or an agent with disabled capabilities. Workflow transforms and future ordinary chat use this lower boundary; agent execution remains explicit.

## Consequences

- Provider credentials, lanes, retries, model mapping, and usage are reused.
- Workflow owns workflow schema/usage projection.
- Future ordinary chat owns transcript/application behavior above the stateless port.
- No workspace/product authority exists in the lightweight request.
