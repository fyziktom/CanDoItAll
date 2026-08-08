# ADR-008: Ordinary workflow LLM calls use the lightweight LLM invocation port

## Status

Accepted; expanded by ADR-010.

## Decision

Ordinary workflow LLM components call the SDK-free lightweight LLM port. They do not create product agents, agent sessions, capabilities, memory, context contributors, approvals, handoffs, or finalizers, and they do not infer workspace authority from payload content.

Explicit tool-capable agent workflow nodes remain a separate node/executor kind.

## Rationale

A workflow transform and an agent execution have different cost, authority, determinism, persistence, and testability requirements. Using the full agent runtime for both hides these differences.

## Consequences

- Workflow mapping preserves provider/model settings, usage, cancellation, and response schema.
- The provider-backed implementation is reusable by future ordinary chat.
- Agent/runtime contracts stay out of the ordinary workflow path.
