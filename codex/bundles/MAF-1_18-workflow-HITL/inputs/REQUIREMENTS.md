# Normalized Requirements

## MAF upgrade

- **RQ-001** Update stable Microsoft Agent Framework packages from `1.17.0` to `1.18.0`.
- **RQ-002** Update MAF A2A preview packages from `1.17.0-preview.260804.1` to `1.18.0-preview.260818.1`.
- **RQ-003** Restore and build all direct MAF package consumers and resolve actual breaking changes, including the agent-isolation provider rename if discovered.
- **RQ-004** Record the resolved package graph and reject a silent mixed 1.17/1.18 MAF graph.
- **RQ-005** Preserve existing streaming, non-streaming, approval, session serialization, provider, workflow, and telemetry behavior affected by the update.

## Tool invocation safety

- **RQ-006** Keep tool invocation serial by default.
- **RQ-007** Explicitly configure application-owned `ChatClientAgentOptions.AllowConcurrentInvocation` as false after the upgrade.
- **RQ-008** Inspect custom `FunctionInvokingChatClient` composition so an already-enabled concurrent setting cannot bypass the central policy.
- **RQ-009** Add a behavioral regression proving multiple order-sensitive tool calls execute in deterministic order and do not overlap.
- **RQ-010** Do not add a public parallel-tool setting in this initiative.
- **RQ-011** Do not enable the experimental declaration-only tool storage option.
- **RQ-012** Preserve approval-loop and approval-session protections, including replay and cross-session rejection.

## Native workflow HITL

- **RQ-013** Replace exception-as-pause for resumable HumanInput nodes with native MAF external requests.
- **RQ-014** Route approval-required workflow executor nodes through a native external request before invoking the governed executor.
- **RQ-015** Run HITL-capable workflows with MAF streaming execution and a checkpoint manager.
- **RQ-016** Persist the MAF checkpoint created at the external-request boundary.
- **RQ-017** Rebuild and resume the exact saved workflow definition version with stable executor/port identities.
- **RQ-018** Verify a deterministic topology fingerprint before resume.
- **RQ-019** Support consecutive external requests in one run.
- **RQ-020** Support both approval and denial as typed governed outcomes.
- **RQ-021** Preserve cancellation and failure semantics while waiting and resuming.
- **RQ-022** Keep the in-process backend classified as non-durable while accurately advertising external-response resume support.
- **RQ-023** Never restart from initial input when checkpoint recovery fails.

## Persistence and replay safety

- **RQ-024** Implement an application-owned checkpoint payload port and a MAF `ICheckpointStore<JsonElement>` adapter without leaking MAF types into domain contracts.
- **RQ-025** Preserve MAF checkpoint commit order from oldest to newest for each session.
- **RQ-026** Store checkpoint session ID, checkpoint ID, parent, commit ordinal, payload, format/version, workflow version, topology fingerprint, and timestamps.
- **RQ-027** Claim a response operation atomically so only one active resume can exist for a request/run.
- **RQ-028** Make same-key/same-payload API retries idempotent and reject same-key/different-payload conflicts.
- **RQ-029** Preserve a recoverable response-operation state if the host stops after acceptance but before completion.
- **RQ-030** Use stable workflow-executor invocation/deduplication keys so replay does not intentionally repeat governed side effects.
- **RQ-031** Do not claim arbitrary external effects are exactly once.

## API and governance

- **RQ-032** Reuse and complete the existing pending-request and response endpoints rather than creating a second API.
- **RQ-033** Provide typed response payload handling without requiring clients to double-encode JSON.
- **RQ-034** Resolve actor identity from authenticated claims or service identity, never from the response body.
- **RQ-035** Authorize access to the run/request/project/tenant and enforce who may answer approval requests.
- **RQ-036** Prevent the requesting workflow/model from approving its own governed request.
- **RQ-037** Validate request kind, response schema, expected request version, payload size, and response policy before acceptance.
- **RQ-038** Audit actor, action, request, payload hash, idempotency hash, timestamps, outcome, and correlation without persisting secrets in logs.
- **RQ-039** Return stable typed outcomes and meaningful HTTP status codes for replay, conflict, unsupported state, authorization failure, stale topology, and recovery failure.
- **RQ-040** Expose enough request/operation status for a client to distinguish waiting, resuming, completed, denied, retryable failure, and terminal failure.

## Validation and closure

- **RQ-041** Use focused test filters with recorded discovered counts; zero discovered tests is failure.
- **RQ-042** Run broad solution validation once at a named frozen checkpoint after focused proof passes.
- **RQ-043** Update API/runtime documentation and package/version documentation.
- **RQ-044** Keep the upgrade wave independently reviewable and revertible from the HITL wave.
- **RQ-045** Close every original request item with implementation and proof evidence.
