# HITL API Sample Design

## Boundary ownership

The standalone sample owns presentation, its file-backed SimWiki corpus/API, conversation
session state, workflow provisioning, the typed CanDoItAll HTTP client, and the authenticated
SSE proxy. CanDoItAll continues to own workflow catalog/runtime state, checkpoint recovery,
authorization, idempotency, and governed approval.

Live proof confirmed the pending-request usability gap. The Web API now adds an allow-listed
presentation projection containing only the bounded HumanInput prompt and trusted response
contract already embedded in the protected semantic request envelope. Raw request context,
authorization policy, checkpoint payload, executor arguments, and persistence entities remain
excluded by mapper and serialization tests.

## Dependency direction

```text
browser -> sample endpoints/SSE -> typed CanDoItAll HTTP client -> CanDoItAll Web API
                 |
                 +-> SimWiki service -> article JSON files

CanDoItAll Web projection -> workflow public model
CanDoItAll Web projection -X-> EF entities / MAF implementation types
```

The sample references no CanDoItAll source project. HTTP JSON is the tested public boundary.

## Workflow shape

The definition is unrolled rather than cyclic:

```text
start -> greeting LLM -> hobby HumanInput -> topic-plan LLM
      -> search 1 -> evaluate 1 --found--> personalized answer -> end
                              --miss--> search 2 -> evaluate 2
                              --miss--> search 3 -> evaluate 3
                              --miss--> explicit not-found answer -> end
```

Two referenced LLM components supply the greeting and common search-state provider/model/prompt
bindings. Before the process can start or automatically approve a run, the provisioner reads the
resolved exact workflow version and both components back and compares immutable identity,
lifecycle, runtime policy, graph, node settings, routing, schemas, execution policies, and
provider/model/prompt bindings. Drift fails closed. Node-specific system instructions and JSON
schemas define each turn. Search URLs are payload data consumed by
`http.fetch`; private-network access is explicitly enabled only for the local SimWiki test
host. Approval requests created by the governed executor are answered by the server-side
sample actor with stable response idempotency keys.

## State and failure policy

- Conversation state is process-local by design and identified by an opaque GUID.
- The API token is configuration-only and never returned to the browser or logs.
- Start and response operations always carry stable idempotency keys. Ambiguous start transport
  checks typed canonical idempotency evidence, including claim state and resolved version/backend,
  before the single bounded identical replay.
- A `202` response persists the exact operation/run/request/version binding. Operation status is
  resolved before canonical detail can advance the conversation; active states retain the binding,
  successful terminal states clear it, and retryable/terminal/denied/cancelled outcomes are explicit.
- SSE is signal-only. Each relevant signal triggers operation-status reconciliation followed by a
  canonical run-detail reload.
- `stream.gap` triggers a canonical reload and cursor reset; it does not trigger polling.
- A reconnectable SSE transport failure ends that server enumeration without mutating conversation
  state or cursor, allowing the browser to reconnect from the unchanged cursor. Malformed or
  semantically invalid stream data still fails closed.
- Idempotency lookup keys are redacted from both exception messages and the public exception URI;
  default HTTP-client logging is disabled.
- Missing configuration, malformed projections, upstream non-success status, invalid
  workflow output, and unexpected request kinds fail visibly. There is no silent fallback.

## Product hardening discovered by the sample

- The supported InMemory EF profile uses exact provider detection and a process-wide mutation
  semaphore. Unknown non-relational providers fail explicitly; the semaphore is not presented
  as multi-process durability.
- Response continuation publishes backend result events only after its durable state commit,
  and service result mapping gives terminal outcomes precedence over stale intermediate state.
- PostgreSQL response replay takes a request-scoped `FOR UPDATE` lock before reading/updating
  the operation. Claim, commit, and replay retain the same request-before-operation lock order.
- Migration `20260822013043_AddWorkflowNativeCheckpointRequestUniqueness` preflights duplicate
  native tuples, then adds a filtered unique index over
  `(SessionId, BackendRequestId, BackendRequestPortId)` when both native fields are non-null.
  It does not delete or silently reconcile legacy rows.
- Known unique violations map to a typed link conflict. Both current callers roll back the
  PostgreSQL transaction after that result; unexpected database errors still propagate.

## Testability contract

The SimWiki search scorer, article loader, workflow payload/result parsing, SSE frame parser,
configuration matcher, typed client, and conversation transition service are directly testable.
Focused tests cover exact definition/component match and drift, pending/completed launch claims,
bounded start/response ambiguity, exact `202` operation binding and resolution, duplicate approval
suppression, terminal operation failure, URI sanitization, and unchanged-cursor SSE reconnect.
HTTP contract tests cover the safe pending projection when changed. Playwright proves browser
rendering and the real workflow/API/SSE path with success, retry, and terminal not-found conversations.

Focused technical validation passes 61/61 sample tests, 71/71 product Unit tests, and 64/64 product
Integration tests. Three Playwright journeys cover a direct hit, a second-attempt hit, and an
exactly-three-search miss at 1280x900 with one EventSource per conversation and no polling.
Final run `20260822T055150662Z-3853d604` binds all three journeys to the frozen 72-file source
digest. `BG-SB07-02` passed 982/983 with zero failures and one declared opt-in live Ollama catalog
skip. Final SHA-256 ledgers and validators pass. Governed proof remains incomplete because no
authentic failing-first test artifact exists.

## Residual risk boundary

Migration application intentionally fails when legacy duplicate native tuples exist; operator
remediation and a dirty-data runbook remain follow-up work. Future link-conflict callers must
preserve transaction rollback. The replay race directly exercises lease renewal through the
shared lock path; other state mutations use the same lock order but are not separately raced.
Conversation sessions are anonymous, process-local, non-durable, and unbounded for this loopback
test deployment. Provisioned components can be changed externally after a process has cached a
successful exact readback. Convergence remains event-driven without a polling fallback, so a
best-effort upstream signal loss remains an operational risk until a later signal or reconnect.
Upstream access logs necessarily observe path-based idempotency keys even though sample logs,
exception messages, and public exception URIs redact them.
