# C# Pattern Selection Records

Each record names the force, selected pattern, rejected alternatives, target types, testability gain, and proof. A selected pattern is permission to extract one responsibility, not permission to add a generic framework.

## PSR-03A — MAF checkpoint boundary

- Problem force: MAF 1.18 requires `ICheckpointStore<JsonElement>` and a checkpoint manager, while workflow contracts, persistence, and APIs must remain SDK-neutral. Index order and payload integrity are protocol invariants.
- Selected pattern: a narrow Adapter over an application-owned checkpoint-payload port.
- Rejected: EF directly implementing the MAF interface; MAF objects in Models/API; a generic repository; a process-local manager as durability; GUID/time-derived ordering.
- New types: strongly typed checkpoint/session/version/fingerprint/payload records in Models; `IWorkflowBackendCheckpointPayloadStore` in Workflows.Abstractions; `MafJsonCheckpointStoreAdapter` in MafAdapter.
- Testability: instantiate the adapter with an in-memory fake port without compiler, backend, DI, or database.
- Proof: exact JSON round-trip, parent/session translation, explicit ordinal ascending order, duplicate/missing/corrupt/hash-mismatch outcomes, cancellation, composition resolution, and source isolation from neutral projects.

## PSR-03B — deterministic HITL compiler topology

- Problem force: one MAF executor binding per business node cannot represent request-port entry/exit/internal edges, and identities must survive reconstruction.
- Selected pattern: a focused Builder producing an immutable compiled-node binding; the existing compiler remains a facade.
- Rejected: more branches in the 493-line compiler; a second full compiler; a partial-class split; random hidden IDs; service location; an interface around a single private implementation.
- New types: `MafCompiledNodeBinding`, `MafWorkflowHitlBindingCompiler`, and `MafWorkflowTopologyFingerprintFactory` in MafAdapter; explicit approval authorization data in Models/Executor Abstractions.
- Testability: direct builder tests do not construct the public compiler or runtime.
- Proof: normal/HumanInput/approval topology, `source.Exit -> target.Entry`, stable role IDs from exact version/node/role, order-independent canonical fingerprint, changed topology/version changes the fingerprint, original executor input remains immutable, response data cannot replace executor/settings/input, and the old compiler delegates and shrinks.

## PSR-03C — request/checkpoint correlation

- Problem force: native request and super-step checkpoint events can arrive in either order; waiting is valid only for a correlated usable pair.
- Selected pattern: a small explicit State object driven by an extracted streaming driver.
- Rejected: event-order assumptions; mutable flags in the backend; Observer with hidden side effects; `AsyncLocal`; delay/channel heuristics; waiting on request alone.
- New types: `MafHitlBoundaryCorrelator`, immutable correlation state/boundary records, and `MafWorkflowStreamingRunDriver` in MafAdapter.
- Testability: feed normalized facts directly, without real MAF or backend construction.
- Proof: request-first/checkpoint-first, duplicates, unrelated session/super-step/port, half-correlated completion/error, consecutive boundaries, exactly one emitted pair, and a real-MAF integration proof.

## PSR-03D — native external-response rehydration

- Problem force: resume must reconstruct a fresh workflow/stream from persisted exact identities, never reuse a disposed run or initial input.
- Selected pattern: separate concrete native-start and response-only resume drivers plus an explicit pure rehydration verifier, sharing the streaming driver and turn-result mapper. The existing backend remains the interface-facing thin facade because runtime selection requires execution and resume on the same backend instance.
- Rejected: retaining the original streaming run; `StartAsync` fallback; latest-version fallback; exception-as-pause; one driver owning both start and response flows; response logic in the already-large backend.
- New types: typed neutral `WorkflowBackendResumeRequest`; concrete `MafWorkflowNativeStartDriver`, response-only `MafWorkflowExternalResponseDriver`, `MafWorkflowRehydrationVerifier`, and internal typed rehydration context in MafAdapter. Do not add a one-implementation public driver interface or change backend-catalog selection.
- Testability: verifier, native-start driver, and response-only driver are directly constructible with explicit compiler, catalog, payload, streaming, request-mapping, and result-mapping collaborators. `MafWorkflowHumanInLoopTests` exercises verifier/response-driver construction without `WorkflowRuntimeManager` and the real MAF reconstruction path.
- Proof: backend `ResumeAsync` contains only delegation; descriptor support is true only with the real driver and `IsDurable` remains false; fail closed on wrong session/request/port/version/fingerprint/hash, missing/corrupt/legacy payload, or tampering; real MAF start/wait/dispose/new instances/resume/complete; pre-wait marker remains one; approval executes once only after approve and never after deny.

## PSR-03E — native turn-result projection

- Problem force: native start and external-response resume produce the same waiting/terminal boundary shapes, while event normalization, start-input capture, safe request projection, payload/artifact capture, and checkpoint metadata form one projection policy. Keeping that policy in either lifecycle driver creates a broad dependency cluster and invites drift between paths.
- Selected pattern: a cohesive Mapper shared by the two lifecycle drivers.
- Rejected: duplicate start/resume mapping; static helper methods; a nested extracted service; a partial-class split; retaining mapping and artifact policy in `MafWorkflowExternalResponseDriver`.
- New types: `MafWorkflowTurnResultMapper`; `MafWorkflowNativeStartDriver` delegates start-turn projection to it, and response-only `MafWorkflowExternalResponseDriver` delegates resumed-turn projection to it.
- Testability: `MafWorkflowTurnResultMapperTests` constructs the mapper directly with explicit payload, request-mapping, normalization, checkpoint, payload-policy, and time dependencies.
- Proof: direct negative coverage rejects an incomplete request/checkpoint boundary; direct positive coverage maps a waiting turn to a safe request and trusted checkpoint and maps a terminal turn with start input and terminal checkpoint. Production native-start and response-only paths both call `MapTurnAsync`.

## PSR-04A — response-operation lifecycle

- Problem force: `RespondedAtUtc` cannot express accepted, claimed, resuming, retryable, terminal, and recovered work; current code consumes the request before continuation is recoverable.
- Selected pattern: explicit State enum plus pure transition policy and a single application continuation coordinator.
- Rejected: timestamp/boolean accumulation; exception-driven transitions; expanding the public run enum; a manager switch; event sourcing.
- New types: operation ID/state/outcome and typed claim/result records in Models; operation/continuation ports in Workflows.Abstractions; transition rules in Workflows.Core; `WorkflowExternalResponseContinuation` in Runtime.
- Testability: transition rules and continuation are tested with fakes and `FakeTimeProvider`, without manager, host, or DbContext.
- Proof: every legal and illegal transition, immutable terminal state, stale version/owner, active lease, cancellation race, missing/non-resumable checkpoint, and crash injection before claim, before delivery, and before finalization.

## PSR-04B — distributed claim and lease

- Problem force: correctness spans processes and failures, so a read-then-write flow or in-memory lock is insufficient.
- Selected pattern: business-specific CAS/lease port implemented by conditional PostgreSQL operations, with a pure lease policy using `TimeProvider`.
- Rejected: generic Repository/Unit of Work veneer; active-run registry as distributed guard; read-then-save; lease without optimistic concurrency; unbounded silent retry.
- New types: focused persistent checkpoint and operation stores plus separate EF entity/configuration files; no additions to `PersistentWorkflowStores.cs`.
- Testability: policy is directly tested; stores run against real PostgreSQL.
- Proof: parallel CAS, expiry takeover, stale owner cannot finalize, explicit ordinal ordering, reconstruction across DbContexts/process-shaped scopes, migration-up, and legacy rows readable but non-resumable.

## PSR-04C — governed executor replay protection

- Problem force: replay protection crosses side-effecting executor invocation, but a database claim alone cannot guarantee arbitrary external exactly-once after effect-before-commit.
- Selected pattern: Decorator around the existing invoker for governed side effects, plus a stable key factory, durable store, and propagated participant idempotency key.
- Rejected: caching all executor/LLM calls; dictionaries; driver-only guards; claiming arbitrary exactly-once; persisting secret-bearing results by default.
- New types: invocation key/state/record in Models; dedup port and explicit execution-context key in Executor Abstractions; `WorkflowExecutorInvocationKeyFactory` and `DeduplicatingWorkflowExecutorInvoker` in Executor Core; focused EF store/configuration.
- Testability: direct decorator tests with counting inner invoker and fake store; no workflow backend required.
- Proof: completed replay does not call inner, live claim blocks, input mismatch fails, bounded expired takeover works, non-governed calls are not cached, secret-policy results are not persisted, DI decorates exactly once without recursion, PostgreSQL race produces one claim, and a participating probe deduplicates the propagated key.
- Guarantee: exactly-once response acceptance and deduplicated participating governed effects; no arbitrary external exactly-once claim.

## PSR-05A — governed mutation boundary

- Problem force: HTTP, Blazor, and agent-tool callers currently submit raw responses through different paths; authorization, validation, replay, and continuation must be identical.
- Selected pattern: exactly one thin application-service Facade over the authorizer Strategy, validator, operation store, continuation, and status read.
- Rejected: endpoint orchestration; a second approvals API; a second manager/helper god class; caller-specific authorization; service location.
- New types: typed command/result/status and service/authorizer/validator decisions in Workflows.Abstractions; one focused facade and validator/grant collaborators in Workflows.Runtime; no new production project.
- Testability: instantiate the service with recording fakes and assert call order and early termination.
- Proof: authorization and validation precede operation claim; every stable operation outcome maps distinctly; the Web POST, `WorkflowsPage.razor.cs`, and `WorkflowAgentRuntimeToolProvider` all use the same service; raw manager submission and the synthesized compatibility actor are unreachable from production.

## PSR-05B — request authorization

- Problem force: user/service/agent callers share a command contract, while scope, capability, persisted-origin, request kind, approver, and self-approval checks require trusted application context. The repository does not have an authoritative per-user project-membership ACL.
- Selected pattern: Strategy through `IWorkflowExternalRequestAuthorizer`, with the concrete implementation in Modules.AgentFramework.
- Rejected: endpoint policy alone; actor/scope/capability from body; anonymous service fallback; caller-specific checks; trusting workflow/model identity; `IHttpContextAccessor` in Core; pretending ordinary JWT claims prove project membership.
- New types: trusted authorization context/decision in Workflows.Abstractions and a module implementation over canonical profile/generation, persisted origin/policy, workspace scope, agent governance, and existing capability services.
- Testability: direct authorizer tests cover unauthenticated/untrusted actor, missing persisted policy/scope, cross-profile or unauthorized scope, profile generation/capability/intended approver, autonomous approval, and workflow/model/agent self-approval. Agents require exact admitted scope; same-profile organization-scoped human/API authority is also tested against server-verified Project/Process targets. Service tests prove no claim/backend call after denial.
- Proof: Web, page, and agent tool derive trusted actor context from their authenticated/session boundary and cannot override it through JSON or tool arguments. Exact scope is required for agents; same-profile organization-scoped human/API authority may cover a server-verified narrower target. Unavailable membership/assignment evidence fails closed and is not reported as an implemented ACL.

## PSR-05C — HTTP projection

- Problem force: current API accepts JSON inside a string and current run/detail output can expose domain origin, event/request/response JSON, artifact paths, and checkpoint identity; stable safe HTTP semantics must remain an edge concern.
- Selected pattern: typed DTO Adapter plus pure mapper in focused Web files.
- Rejected: exposing Models/EF/MAF records; raw `RequestJson`; endpoint lifecycle switch; retaining local-resume 502 mapping; partial `WorkflowsApi` expansion.
- New types: typed request/response/run/event/pending/operation DTOs, one pure safe projection/outcome mapper, actor resolver, bounded body reader, and idempotency-key parser.
- Testability: mapper and projection tests run without a host; real-host integration covers binding/auth/metadata.
- Proof: full 200/202/400/401/403/404/409/410/422/503/500 matrix, bounded body/header, disallowed unknown/body-owned security fields, typed OpenAPI metadata, and a materially thinner endpoint file. Safe projections exclude launch-origin internals, raw event/request/response JSON, protected payload, checkpoint native IDs/references/hashes, artifact/storage paths, credentials, and governed original arguments.

## PSR-05D — response validation

- Problem force: approval, tool-approval, and HumanInput payloads have distinct persisted contracts, while request version, state, run linkage, schema, size/depth, and resumability must be checked identically for every caller and retry.
- Selected pattern: one cohesive validator behind `IWorkflowExternalResponseValidator`; it produces a typed validated response/action and never mutates operation or runtime state.
- Rejected: validation in endpoints/tools/pages; relying only on JSON deserialization; reusing raw transition exceptions as an API contract; silently accepting missing legacy policy or schema.
- New types: neutral validation command/result and a focused Runtime validator that reuses the persisted response contract and JSON-schema validator without Web/MAF/EF types.
- Testability: direct table-driven tests cover Approve, Deny, SubmitInput and every state/version/link/schema/size/depth/resumability negative; service tests assert mutation is not reached on failure.
- Proof: the action is derived only from validated canonical protected payload; neither request headers nor caller context can assert Approve/Deny/SubmitInput.

## PSR-05E — recoverable authorization grant

- Problem force: background recovery has no HTTP principal but must enforce the same accepted authorization, including expiry and policy binding, without adding duplicated action/expiry columns.
- Selected pattern: a pure Factory reconstructs a short-lived executor authorization grant from the operation's persisted actor, `AcceptedAtUtc`, actor-scope fingerprint, and protected payload plus the boundary's persisted exact target scope, lifetime, policy fingerprint, and response contract. The fingerprint covers actor, exact target scope, and policy; channel, current profile generation, and caller capability remain submission-time authorizer checks rather than recoverable fingerprint inputs.
- Rejected: re-authorizing as a fabricated service; trusting operation existence alone; persisting a caller-provided action; adding action/expiry/grant columns; allowing legacy or expired evidence to resume.
- New types: neutral reconstructed-grant/decision values and a focused Runtime grant factory invoked by continuation before backend/executor delivery. Scope/lifetime/policy evolve in existing `OriginJson`/`AuthorizationPolicyJson`; no relational migration or new project is permitted.
- Testability: direct factory tests freeze `TimeProvider`, derive each action from protected JSON, recompute the fingerprint, and reject missing/corrupt/legacy/mismatched/expired evidence without a backend.
- Proof: initial continuation and lease-takeover/background recovery call the same factory; all rejection paths have zero backend and executor invocations; no SB05 EF entity/configuration/model-snapshot diff or migration exists.

## Global rejection record

The following block every architecture checkpoint: new handwritten production partials, nested extracted services, broad `*Manager`/`*Helper` types, service locators or `BuildServiceProvider`, tests only through the old manager/full host, duplicate state policy, framework dependencies flowing inward, and DI bypassing the directly tested seam.
