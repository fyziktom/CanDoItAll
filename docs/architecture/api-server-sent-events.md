# API server-sent events

## Status

Accepted for the basic API event-propagation increment.

## Context

External clients need to drive agents, observe provider calls, and receive lightweight workflow and process lifecycle notifications. The existing runtime already owns the authoritative state:

- agent operations publish bounded, sequenced activity streams;
- workflow runs persist `WorkflowEventRecord` values and publish them through `IWorkflowEventSink`;
- process runtime events are durably persisted and projected after commit.

The HTTP API must not become a second source of truth. It also must not let a slow or disconnected HTTP client delay runtime execution.

## Decision

### Transport boundary

SSE is an API transport concern in `CanDoItAll.Web`. A shared writer owns framing, cursor parsing, heartbeats, proxy-buffering headers, and explicit replay-gap events.

Workflow and process notifications use bounded, in-memory replay streams with monotonically increasing API cursors. The runtime publisher performs a bounded write and queues one coalesced wake-up; it never performs client I/O. A subscriber reads from the bounded replay window and receives an explicit gap event when its cursor is older than retained data.

Each replay stream is pinned to the current database profile ID and runtime generation. A profile change atomically replaces the replay buffer, preserves increasing cursor values for the host lifetime, and cancels subscribers pinned to the retired profile. Every open and publish also compares the canonical runtime snapshot so isolation does not depend only on notification delivery.

Agent operations continue to use the existing canonical `AgentExecutionActivityCoordinator`. The API only resolves the current profile-scoped stream identity and projects its existing sequenced events to SSE.

### Domain adapters

- `WorkflowApiEventSink` implements the existing `IWorkflowEventSink` boundary and publishes signal-only envelopes. It omits workflow payload JSON.
- `ApiNotifyingProcessRuntimeProjector` decorates the canonical projector. It publishes only after projection succeeds, preserves the durable global sequence in the envelope, and masks restricted event details.
- Provider chat SSE reports accepted, running, completed, and failed states around the existing full-response provider call. It does not claim token streaming because the provider abstraction does not expose token deltas.

### API contract

- global workflow and process streams notify clients that a run changed;
- run-specific workflow and process streams apply a strongly typed run-id filter to the same source;
- agent activity streams are keyed by a client-visible operation ID;
- agent commands accept an optional client-generated operation ID, allowing the client to subscribe while the command is running;
- image attachments are uploaded through the existing bounded workspace staging service and referenced by its returned relative path;
- approval decisions continue through the existing execution-run command and may carry a new operation ID for the continuation stream.

SSE payloads are deliberately small. Existing detail endpoints remain the way to retrieve canonical run state, approvals, artifacts, and history.

### Configuration

`Api:SwaggerUiEnabled` controls the Swagger UI independently. It is effective only when `Api:OpenApiEnabled` is also enabled. `Api:ServerSentEvents` contains bounded retention, batch, and heartbeat settings.

When API bearer authorization is enabled, a reverse proxy or another deliberate authentication mechanism must make the protected Swagger document available to the browser UI. Disabling the UI does not disable the OpenAPI document; `Api:OpenApiEnabled` controls both documents.

## Responsibility inventory

| Concern | Owner |
| --- | --- |
| Runtime truth and lifecycle transitions | Existing agent, workflow, and process services |
| Event-to-public-envelope mapping | Domain-specific Web API adapter |
| Profile-isolated bounded replay and cursor gaps | Shared Web API streaming primitive |
| SSE framing and heartbeat | Shared Web API response writer |
| Authorization | Existing `/api` route group policy |
| Detail retrieval | Existing domain query endpoints |

## Pattern selection

- Observer is used at existing lifecycle notification boundaries.
- Decorator is used for process projection so notification happens only after the canonical projection succeeds.
- A bounded replay buffer is used instead of an unbounded channel or one database poller per subscriber.

Rejected alternatives:

- publishing directly from API command handlers, because background and recovery paths would be missed;
- duplicating runtime state inside an API event model;
- awaiting per-subscriber writes from workflow or process publishers;
- advertising provider token streaming without a token-streaming provider contract;
- adding the implementation to the already-large agent, workflow, or process endpoint files.

## Consequences and limits

- Workflow and process API cursors are host-local and replay only the configured bounded window. A gap is explicit; clients then query canonical detail/history endpoints.
- A process restart creates a new host lifetime. Clients must discard a cursor from the previous host; durable cross-restart resume is not part of this basic increment.
- Workflow API cursor order is publication-arrival order, not canonical event-store order.
- Process projection notification is at-least-once. Clients deduplicate process signals by `EventId` or `GlobalSequence`.
- Process envelopes retain the durable global sequence for deduplication, diagnostics, and future durable resume work.
- All run-specific readers currently share the profile-global wake-up and copy bounded batches under one lock. This is appropriate for local/basic fan-out, not a claim of high-fan-out scalability. Benchmark before introducing token-rate events or thousands of subscribers; likely next steps are keyed run partitions and pre-serialized/batched frames.
- Native browser `EventSource` cannot attach a bearer token. Authorized clients should use an HTTP streaming client that supports request headers.
- SSE is one-way. Commands, attachment uploads, and approval decisions remain normal HTTP requests.

## Validation

- unit and integration tests cover bounded replay, gap detection, profile-switch isolation, filtering, process sensitivity masking, and independent subscriber cancellation;
- integration tests cover successful and failed same-request agent streams, canonical versus id-less frames, operation IDs, admission errors, input validation, attachment staging, Swagger UI enablement, and OpenAPI contracts;
- architecture dependency analysis must remain cycle-free;
- performance review checks bounded memory, bounded publisher work, subscriber cleanup, cancellation, task ownership, and absence of synchronous client I/O;
- the full solution is rebuilt and the API is smoke-tested on port 5032.
