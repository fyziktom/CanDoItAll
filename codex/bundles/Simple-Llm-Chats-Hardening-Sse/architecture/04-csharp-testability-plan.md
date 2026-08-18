# C# testability plan

## Direct unit-test seams

- `LlmChatOperationReducer`
- request fingerprint canonicalizer
- legal state-transition policy
- cancellation-vs-finalization decision
- attempt/evidence outcome reducer
- streaming accumulator
- retry-before-first-delta policy
- event coalescing policy
- SSE DTO/event mapper
- retention/cleanup policy

These tests must construct only the target type and small immutable inputs.

## PostgreSQL integration seams

Use the real `AppDbContext`/PostgreSQL test harness for:

- create/rename atomicity with injected failure;
- admission atomicity;
- success finalization atomicity;
- compensation and RecoveryRequired;
- claim ownership/heartbeat/expiry;
- two-host claim and cancel races;
- profile switch during each command boundary;
- keyset pagination and context-window bounds;
- event journal sequence, replay and retention;
- migration/model/transfer round trips.

## HTTP host seams

Use a deterministic fake streaming provider behind the real provider contract. Prove:

- POST returns 202 before completion;
- disconnect does not cancel;
- SSE emits ordered deltas and terminal event;
- reconnect resumes with `Last-Event-ID`;
- invalid cursor is 400;
- replay gap is explicit;
- profile switch ends stream;
- cancel from a second client/host wins according to state contract;
- authorization scopes and server-owned origin.

## Negative proof

Each critical subbundle must include at least one test that would have passed under a shallow
implementation but fails under the old source, such as an injected failure between the previous two
transactions or two service providers claiming the same operation.
