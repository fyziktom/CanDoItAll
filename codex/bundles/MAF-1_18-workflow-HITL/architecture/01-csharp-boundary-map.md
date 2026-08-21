# C# Target Boundary Map

## Project-boundary decision

No new production project is justified. The existing layers already provide the needed dependency direction. If implementation appears to require a reverse reference, move the shared contract downward into Models or Workflows.Abstractions; do not add a bridge project or cycle.

## Contracts, implementations, and target ownership

| Owner | Target top-level types and responsibility |
|---|---|
| `AgentFramework.Models` | Strongly typed backend session/checkpoint IDs, commit ordinal, topology fingerprint, compiler/checkpoint format versions, external-response operation ID/state/result, launch/request authorization scope and lifetime snapshots, approval authorization, and executor invocation key/state records |
| `Workflows.Abstractions` | Native checkpoint payload port, typed resume request/result, response operation and atomic boundary ports, one external-response facade contract with typed command/result/status, trusted caller context, authorizer/validator decisions, and the executor invocation-dedup port |
| `Workflows.Core` | Pure compatibility/topology and response-operation transition policy where no higher-layer dependency is required |
| `Workflows.Runtime` | The single external-response application facade, response validation and authorization-grant reconstruction, continuation, plus proof-only process-local in-memory implementations; no production durability claim |
| `WorkflowExecutors.Abstractions` | Explicit invocation context carrying optional scoped approval authorization; no MAF type |
| `WorkflowExecutors.Core` | Approval-token validation and side-effect dedup coordination around the existing invoker |
| `Workflows.MafAdapter` | Compiled node binding, HITL binding compiler, JSON checkpoint adapter, event-correlation accumulator, streaming run driver, native-start driver, response-only external-response driver, and cohesive turn-result mapper |
| `Modules.AgentFramework` | The persisted-scope authorizer Strategy, trusted launch-scope resolution, the Blazor and agent-tool adapters, and focused EF/PostgreSQL stores created by SB04 |
| `CanDoItAll.Web` | Thin HTTP adapter: typed wire contracts, bounded body/idempotency binding, trusted principal resolution, safe run/request/operation projections, and a pure outcome-to-HTTP mapper |
| `Migrations.PostgreSql` | SB04 generated migration/model snapshot only; SB05 changes JSON contract content in existing columns and must not add a relational migration |
| Tests | Direct responsibility tests, architecture negatives, composition smoke, and real PostgreSQL migration/CAS/recovery proof |

Names may adapt to established repository conventions, but responsibility ownership and dependency direction may not drift silently.

`WorkflowExternalRequestRecord` also evolves without breaking its current positional construction where practical. New neutral top-level Model values cover a monotonic request version; explicit Pending/ResponseClaimed/Responded/Denied/Superseded/Cancelled/LegacyNonResumable state; response kind/schema/version/hash/maximum-payload contract; backend session/request/port/checkpoint link with compiler/topology identity; and an immutable safe authorization-policy snapshot. `RequestJson` must not carry schema, native linkage, authorization policy, and governed original arguments simultaneously. Legacy rows with absent contract/link remain explicitly non-resumable; no values are synthesized.

SB05 extends the serialized launch origin and authorization-policy snapshots rather than the
relational schema. The trusted launch scope is serialized through the existing run
`OriginJson`; the exact response scope, authorization lifetime, and policy fingerprint are
serialized through the existing boundary `AuthorizationPolicyJson`. The operation already
persists protected canonical response payload, actor kind/subject, actor-scope fingerprint,
and `AcceptedAtUtc`. No action, expiry, scope, or authorization-grant column is added.

## Boundary responsibilities

### Neutral checkpoint contract

The neutral checkpoint record owns workflow/run/version/backend identity, strongly typed opaque backend session/checkpoint/parent IDs, commit ordinal, canonical JSON payload, SHA-256, format/compiler versions, topology fingerprint, external-request linkage, and timestamps. Its port supports ordered creation and exact read. It exposes no MAF SDK object.

### MAF topology compilation

A focused binding compiler creates deterministic internal roles for each business node: entry, request/decision where applicable, execution, and exit. External graph edges connect source exit to target entry. Stable role IDs derive from workflow version, node ID, and role. `MafWorkflowCompiler` stays as a thin public facade and delegates this responsibility.

### Streaming request/checkpoint correlation

A deterministic accumulator accepts native stream events and yields a waiting boundary only when a correlated external request and usable checkpoint are both present. It supports either arrival order and rejects incomplete or conflicting pairs. `MafWorkflowStreamingRunDriver` owns the MAF start/resume stream lifecycle, enumeration, cancellation, and disposal. `MafWorkflowNativeStartDriver` owns only native HITL start orchestration and delegates common result projection to `MafWorkflowTurnResultMapper`.

### Resume translation

`MafWorkflowExternalResponseDriver` is the response-only concrete collaborator of `MafInProcessWorkflowExecutionBackend`. The existing backend continues to implement both execution and external-response backend contracts because `WorkflowRuntimeManager` selects and casts the same backend instance; its resume method is a thin compatibility facade delegating to the response driver. The response driver loads and compiles the persisted exact workflow version, verifies the checkpoint identity, creates a fresh streaming run, maps the trusted persisted request/port to a native response, and consumes continued events. It does not own native start orchestration or result projection: `MafWorkflowNativeStartDriver` owns the former and both drivers delegate the latter to `MafWorkflowTurnResultMapper`. No driver loads a latest definition or restarts from initial input when compatibility validation fails. The descriptor advertises resume only when the real response driver is supplied; durability remains false in SB03.

### Turn-result projection

`MafWorkflowTurnResultMapper` is the single cohesive owner for transforming a completed or waiting native stream turn into `WorkflowBackendStartResult`. It owns normalized event projection, start-input capture, waiting request/checkpoint projection, payload/artifact handling, and terminal result construction. It does not start or resume MAF runs, load workflow definitions, or deliver external responses. `MafWorkflowNativeStartDriver` and the response-only `MafWorkflowExternalResponseDriver` both delegate to this mapper, preventing duplicated projection policy while keeping lifecycle orchestration separate.

### Approval authorization

SB03 carries a server-created approval request ID and immutable authorization through the explicit executor invocation context. Before invocation, the executor enforces an active run plus exact run, workflow, workflow version, node, executor, required-capability set, approval requirement, original-input hash, and expected/presented token equality. Token comparison is fixed-time. A denial stops invocation, and response JSON cannot supply any of those values or the original input. The request ID is checkpoint-owned context in SB03; it is not treated as a client assertion or claimed as a standalone invoker comparison.

The authorization contract is intentionally phase-staged. SB04 has added the durable response-operation identity and bound it into persisted response/invocation identity. SB05 binds trusted actor identity, authoritative decision time, target scope, policy fingerprint, and expiry enforcement at the common authorized response boundary. At initial acceptance and on every background retry, a focused grant factory reconstructs authorization from the operation actor and `AcceptedAtUtc` plus the boundary's persisted exact target scope, lifetime, and policy fingerprint. It revalidates the protected canonical payload against the persisted response contract and derives Approve, Deny, or SubmitInput from that validated payload; a caller-supplied or separately persisted action is never trusted. The operation actor-scope fingerprint binds the actor kind/subject, exact target-scope kind/key, and policy fingerprint. Trusted channel, current profile generation, and caller capabilities are authoritative submission-time authorizer checks; they are not claimed as durable fingerprint inputs. Missing, legacy, expired, or mismatched evidence fails closed before backend or executor invocation. These SB05 guarantees are not SB04 closure claims.

### External-response application service

One `IWorkflowExternalResponseService` application facade accepts a typed command and trusted
caller context. It loads only the context needed for a decision, invokes
`IWorkflowExternalRequestAuthorizer`, invokes the response validator, creates or replays the
operation, and delegates continuation. The authorizer and validator are cohesive
collaborators, not caller-specific policy copies. The facade also exposes focused operation
status through the same contract. There is no second approvals API, response manager, or
parallel facade.

All three production mutation callers converge on this facade:

1. the existing Web response POST through a focused Web adapter/mapper;
2. `WorkflowAgentRuntimeToolProvider` through trusted agent-governance context;
3. `WorkflowsPage.razor.cs` through a trusted authenticated/local-operator context factory.

Raw `IWorkflowRuntimeManager` response submission and the compatibility coordinator are not
production bridges after SB05. The old encoded-JSON wire DTO is removed rather than versioned
because entry inventory found no client that requires it.

### Authorization scope and honest enforcement

The authorizer Strategy evaluates the canonical current database profile/generation, the
trusted caller channel and capabilities, the launch origin, the persisted target
organization/project/process scope, the request kind, intended approver/capabilities, and
self-approval rules. API claims and request/tool bodies do not create scope. Agents require
their exact admitted workspace scope. Organization-scoped human/API authority may cover a
server-verified Project or Process target only within the same canonical profile; cross-
profile access is denied. Autonomous agent or generic service actors cannot approve Approval
or ToolApproval requests; an authenticated human originator is not automatically treated as
workflow/model self-approval.

The repository has no authoritative per-user project-membership ACL service. SB05 therefore
does not claim one: it proves the persisted target is inside the caller rule above and uses
only authorization data that actually exists. If a policy requires membership/assignment
that cannot be established from trusted persisted data, authorization fails closed instead
of fabricating access. Missing scope/policy is an incompatible boundary, while an
authenticated caller with the wrong known scope/capability is forbidden.

### Web projection boundary

The Web adapter maps typed `JsonElement` request bodies and idempotency/correlation headers to
the common command, and a pure mapper maps service outcomes to the documented HTTP matrix.
Run detail, pending request, response-operation status, and event output use explicit safe Web
DTOs. They do not serialize domain `WorkflowLaunchOrigin`, `RequestJson`, `ResponseJson`, raw
event payload JSON, artifact/storage paths, checkpoint native IDs/references/hashes, protected
payloads, credentials, or governed original arguments. `WorkflowsApi.cs` loses response and
projection decisions to focused top-level Web files and must shrink.

### Persistent resume boundary

A focused PostgreSQL store atomically commits the data that belongs together: run transition/lifecycle event, emitted events, next external request, linked checkpoint metadata/payload, prior-request final state, and response-operation state/result. External MAF execution remains outside the database transaction; recovery uses durable operation states, leases, and participating executor-invocation deduplication. The precise guarantee is exactly-once response acceptance and deduplicated participating governed effects.

### Executor invocation deduplication

The existing invoker remains the enforcement point. A focused coordinator/store surrounds governed side-effect execution after approval validation. Stable identity includes run, exact workflow version, node, causation request/operation, logical generation, executor contract version, and input hash. Completed results replay only under explicit payload/security policy; live leases prevent parallel execution; any hash mismatch fails closed.

### Persistence implementation

New EF entities, configurations, and stores use separate source files. Existing persistence retains legacy run/request/metadata-checkpoint compatibility but does not absorb the new native payload, operation, resume-boundary, or dedup responsibilities.

SB05 does not introduce another EF entity or alter a relational column. Authorization scope,
lifetime, and policy fingerprint use the existing serialized origin/policy properties. The
operation's existing actor, accepted time, actor-scope fingerprint, and protected-payload
properties are the durable grant reconstruction inputs. The fingerprint covers actor,
exact target scope, and policy; accepted time enforces lifetime and the protected payload is
revalidated to derive the action. Submission-time channel/profile-generation/capability
checks are intentionally not represented as recoverable fingerprint fields. A generated SB05
migration would be architecture drift and requires reopening this decision before
implementation continues.

## SDK-neutral boundary rules

- Models, Workflows.Abstractions/Core/Runtime, and executor contracts must not reference `Microsoft.Agents.AI`.
- Only MafAdapter may use native request ports, checkpoint managers, checkpoint info, stream events, and external responses.
- Only Modules.AgentFramework/Infrastructure may use EF Core or Npgsql.
- EF entities never cross persistence ports.
- MAF checkpoint JSON is opaque infrastructure data and is never returned by public APIs or logged.
- Exact workflow version comes from the existing catalog service.
- Runtime depends only on a neutral external-response backend and never constructs/casts to MafAdapter types.
- API authorization and actor extraction stay outside the adapter; SB05 supplies a trusted typed actor context to the application service.
- Web may depend on the neutral facade/DTO mapping inputs, but Workflows.Abstractions/Core/Runtime must not depend on ASP.NET claims or HTTP result types.

## Composition-root responsibilities

- Runtime registration owns the external-response service and explicit in-memory proof implementation.
- MAF adapter registration owns compiler, checkpoint adapter, streaming driver, `MafWorkflowNativeStartDriver`, response-only `MafWorkflowExternalResponseDriver`, and `MafWorkflowTurnResultMapper` implementations.
- AgentFramework module composition replaces in-memory ports with persistent implementations.
- In-memory implementations are proof-only, process-local, non-durable, and non-snapshot-isolated; they do not establish host-restart or multi-host correctness.
- PostgreSQL conditional writes, unique constraints, and transactions are authoritative for production CAS and atomic persistence.
- Resume capability is advertised only when the complete native driver and checkpoint-store composition is registered; `IsDurable` remains false because the in-process backend is not a durable orchestration host.
- Module composition registers exactly one production facade, one persisted-scope authorizer
  Strategy, one validator path, and trusted Web/page/agent context resolvers. Background
  recovery reaches continuation only after fail-closed grant reconstruction; it never uses a
  fabricated compatibility actor or skips authorization because no request principal exists.

## Old-class responsibilities to remove or leave

| Existing type | Leave | Remove/delegate |
|---|---|---|
| `MafWorkflowCompiler` | public validation/compile facade | native HITL topology construction and role wiring |
| `MafInProcessWorkflowExecutionBackend` | backend contract facade and native/legacy route selection | native start orchestration to `MafWorkflowNativeStartDriver`, resume orchestration to the response-only `MafWorkflowExternalResponseDriver`, and common result projection to `MafWorkflowTurnResultMapper` |
| `WorkflowRuntimeManager` | 738-line start/cancel public runtime facade with thin factory entry points and one-line response delegation | response operation coordination, exact-version resume sequence, and recovery state transitions are delegated to focused collaborators; compatibility construction is test-only |
| `WorkflowExecutorInvoker` | catalog resolution, policy, timeout/retry/audit facade | approval-token matching and deduplicated side-effect coordination |
| `PersistentWorkflowStores.cs` | existing legacy stores | all new native checkpoint, operation, boundary, and dedup types |

## Temporary bridges and removal plan

- Legacy exception-as-pause and metadata-only checkpoints may remain readable for runs created before SB03, but new native runs must not use that path.
- The compatibility construction path is test-only. SB05 must not expose it or use it to bypass the common authorized response service.
- The raw response methods on `IWorkflowRuntimeManager` and the synthesized
  `workflow-runtime-compatibility` actor are removed from production reach when the three
  callers move to the facade; there is no silent fallback.
- Existing request/checkpoint projections remain for operator inspection; native payload never enters those projections.
- Every bridge must have a source assertion and deletion checkpoint in SB03 or SB04 proof. No bridge may silently fall back from failed native resume to initial execution.

## Testability contract

Every extracted responsibility must be directly constructible with explicit ports and `TimeProvider`, without the web host, `AppDbContext`, or `WorkflowRuntimeManager`. Required seams are the binding compiler, checkpoint adapter, correlation accumulator, streaming driver, native-start driver, response-only external-response driver, `MafWorkflowTurnResultMapper`, response-operation policy/service, authorizer Strategy, response validator, authorization-grant factory, Web mapper/safe projection, dedup coordinator, and persistent stores against real PostgreSQL. Facade tests must prove the authorizer and validator run before mutation. Recovery tests must prove protected-payload action derivation, fingerprint/expiry validation, and zero backend/executor calls for incomplete authorization evidence.
