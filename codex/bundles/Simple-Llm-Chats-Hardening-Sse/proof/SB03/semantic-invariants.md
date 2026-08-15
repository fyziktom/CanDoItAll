# SB03 semantic invariants

## Admission and identity

- A public LLM Chat application method acquires its runtime lease before invoking the inner service.
- The captured identity is profile ID, runtime fingerprint, and host generation from the immutable
  canonical runtime database.
- An old host cannot reinterpret a newly selected control-plane profile as its own runtime root.

## Propagation

- One `LlmChatOperationExecutionContext` remains active through application repositories, transcript
  commands, provider resolution/invocation, invocation audit, durable commits, and final return.
- A nested conversation-engine call reuses an existing operation scope.
- Direct engine consumers still receive one self-owned lease and scope.

## Durable ordering

- A root LLM Chat EF transaction executes inside `IDatabaseRuntimeWriteFence`.
- Profile-switch publication and fenced transaction commit are mutually exclusive and total-ordered.
- Once the switch snapshot is published, an old identity cannot enter another fenced durable operation.
- Nested unit-of-work calls share the root transaction and never reacquire the non-reentrant fence.

## Failure semantics

- Profile invalidation returns the stable typed `RuntimeProfileChanged` failure.
- Caller cancellation remains caller cancellation and is not silently relabeled.
- If provider audit/usage committed before switch publication, that evidence remains authoritative while
  assistant finalization is rejected and the active turn remains recoverable.
