# C# Testability Plan

## Testability objective

Each extracted responsibility must be directly constructible through explicit typed dependencies. Unit proof must not require `WebApplication`, `AppDbContext`, `WorkflowRuntimeManager`, a service locator, or reflection into private implementation. `TimeProvider` controls leases and expiry. Fakes model external providers/stores only; they may not fabricate the production signal being proved.

## Characterization tests to retain

- `WorkflowFoundationTests`: compile/execute/wait/response compatibility and workflow semantics.
- `MafWorkflowAdapterIsolationTests`: MAF dependency containment and composition ownership.
- `MafWorkflowEventNormalizerTests`: stable public event normalization.
- `WorkflowRuntimeLifecycleRedGateTests`: lifecycle transition and cancellation semantics.
- `WorkflowExecutorTests` and policy-observability tests: approval, retry, timeout, audit, and side effects.
- `WorkflowRuntimePersistenceLifecycleTests`: current persistence lifecycle/legacy compatibility.
- SB00 frozen floors: Approval 12, AdapterIsolation 4, Lifecycle 13, API integration 16. Later additions may raise, never lower, the applicable floor.

## SB03 isolated unit tests

- `MafJsonCheckpointStoreAdapterTests`: exact JSON, typed identity mapping, parents, ordered index, duplicate/missing/corrupt/hash/cancellation outcomes.
- `MafWorkflowHitlBindingCompilerTests`: normal/HumanInput/approval bindings, internal/external edges, deterministic role IDs and fingerprint, immutable original executor input.
- `MafHitlBoundaryCorrelatorTests`: both event orders, duplicate/unrelated/incomplete facts, consecutive boundaries, terminal event during half correlation.
- `MafWorkflowTurnResultMapperTests`: directly construct the cohesive mapper; reject an incomplete request/checkpoint boundary; project a waiting turn to a safe request and trusted checkpoint; project a terminal turn with captured start input and terminal checkpoint.
- `MafWorkflowHumanInLoopTests`: directly construct the rehydration verifier and response-only `MafWorkflowExternalResponseDriver`; exercise persisted request/port translation, approve/deny/HumanInput typed JSON, disposed-run reconstruction, consecutive waits, and no `StartAsync` fallback against real MAF. Production native execution is routed separately through `MafWorkflowNativeStartDriver`.
- Approval authorization/scope tests: exact-match token accepted without re-prompt; missing active run, wrong run/workflow/version/node/executor/capability set/approval requirement/token/input hash, and explicit denial all stop executor invocation. These are the fields enforced in SB03.

## SB04 isolated unit tests

- Pure response-operation transition table covers every allowed and forbidden edge and terminal immutability.
- Continuation coordinator tests record dependency call order and inject crashes at accepted-before-claim, claimed-before-delivery, and delivered-before-finalize.
- Lease policy tests use `FakeTimeProvider` for active conflict, expiry takeover, owner/epoch/version mismatch, bounded attempts, cancellation.
- Executor invocation-key factory tests prove all stable identity material and input hash participate.
- Dedup decorator tests use a counting inner invoker for completed replay, live conflict, mismatch, takeover, policy exclusion, and secret-bearing result handling.
- Response-operation tests prove the durable operation identity is bound to continuation and invocation identity; SB03 does not claim this field.

## SB05 isolated unit tests

- External-response facade tests assert load-safe-context, authorize, validate, create/replay,
  and continue order, plus status reads. Unauthorized/invalid inputs cause no operation,
  backend, or executor mutation. Recording fakes also prove there is one facade contract, not
  caller-specific orchestration.
- Authorizer Strategy tests cover unauthenticated/untrusted actor, missing persisted
  policy/scope, wrong canonical profile generation, cross-profile or unauthorized scope,
  missing capability/intended approver, autonomous agent/service approval,
  stale/superseded request, wrong request/run, and workflow/model/agent self-approval. Agents
  require exact admitted scope; organization-scoped human/API authority may cover a
  server-verified Project/Process target only within the same canonical profile. Tests assert
  only evidence the repository actually owns; they do not fake a per-user project ACL.
- Authorization-grant factory tests bind the operation actor and authoritative
  `AcceptedAtUtc` to the boundary's exact target scope/lifetime/policy fingerprint. They
  recompute the durable actor + target-scope + policy fingerprint, derive Approve, Deny, or
  SubmitInput from the validated protected payload, and reject missing, corrupt, legacy,
  mismatched, or expired evidence without backend or executor invocation. Channel, current
  profile generation, and caller capability are submission-time authorizer checks, not
  recoverable fingerprint fields. These are SB05 guarantees, not SB03 guarantees.
- Approval/ToolApproval/HumanInput validator tests cover kind, schema, version, linkage,
  request state, checkpoint resumability, depth/size, duplicate properties, and disallowed
  fields. A caller-provided action is never accepted.
- HTTP adapter/mapper tests cover every documented status/outcome, typed `JsonElement`
  binding, bounded content type/body/header/depth, and absence of the old encoded-JSON DTO.
- Projection/redaction tests assert domain launch origin, raw event/request/response JSON,
  protected payload, checkpoint JSON/native IDs/references/hashes, artifact/storage paths,
  credentials, secrets, and original governed arguments never appear in run, pending, or
  operation output.
- Caller-adapter tests prove the Web POST, `WorkflowsPage.razor.cs`, and
  `WorkflowAgentRuntimeToolProvider` all preserve their trusted context while invoking the
  same facade. The agent negative proves an autonomous agent cannot approve even when it can
  submit HumanInput.

## Negative architecture tests

- No `Microsoft.Agents.AI` namespace or package reference in Models, Workflows.Abstractions/Core/Runtime, executor contracts, Web DTOs, or persistence entities.
- No EF/Npgsql reference in neutral projects.
- No Runtime/Core/Abstractions reference to MafAdapter.
- No new handwritten production partial class in the affected source set.
- Extracted responsibilities are top-level and directly instantiated by tests.
- Source assertions prove new native paths neither throw nor catch `WorkflowExternalRequestPendingException`.
- Source assertions prove response mutation callers use the common service and cannot call raw manager submission after SB05.
- Source assertions enumerate exactly the three production callers and reject
  `SubmitExternalResponseAsync`, `RespondToExternalRequestAsync`, or direct compatibility
  coordinator calls outside the facade implementation/test fixtures.
- Source/schema assertions prove SB05 uses existing `OriginJson`,
  `AuthorizationPolicyJson`, operation actor/accepted-time/fingerprint, and protected payload
  properties, with no new EF property, model-snapshot change, or relational migration.
- Line/responsibility assertions prove the old compiler/backend/manager/persistent-store cluster shrinks or becomes a thin facade; moving code to a partial/nested forwarding helper fails the test.

## Integration and composition smoke

- Resolve compiler/checkpoint adapter/streaming driver/`MafWorkflowNativeStartDriver`/response-only `MafWorkflowExternalResponseDriver`/`MafWorkflowTurnResultMapper` through production MAF registrations.
- Real MAF integration: start -> pre-wait marker -> native request plus real JSON checkpoint -> dispose all first-run objects -> reconstruct -> typed response -> complete or wait again. The marker remains one.
- Approval integration: governed side effect count is zero before approval, one after approve, and zero after deny.
- Module composition resolves persistent stores and exactly one dedup decorator without recursion or an in-memory fallback.
- Real PostgreSQL applies migrations and proves ordered checkpoint index, hash read, unique constraints, concurrent claim, lease takeover, stale-owner rejection, atomic boundary persistence, crash recovery, and legacy non-resumability.
- Real API host plus PostgreSQL plus real MAF exercises the SB05 HTTP/auth/idempotency matrix.
- Recovery integration accepts an operation through the authorized facade, disposes the
  request scope, and resumes through the background recovery path. Valid unexpired evidence
  succeeds; missing policy, scope/fingerprint mismatch, expired lifetime, corrupt protected
  payload, and autonomous approval all fail closed with zero backend/executor delivery.
- Real-host safe-projection assertions inspect run detail, event, pending request, and
  operation status JSON and OpenAPI metadata; forbidden domain/persistence fields never
  serialize.
- PostgreSQL/model verification proves SB05 has no pending relational model change and no
  generated migration; the previously proven SB04 migration remains the latest required HITL
  schema change.

## Fake provider/tool/driver policy

Allowed fakes are deterministic external LLM providers, executors, clocks, and storage ports used to observe interactions. Governed proof still uses the real MAF request/checkpoint/response protocol and production application/persistence boundary. A fake resume backend, manually seeded checkpoint, metadata-only checkpoint, mocked EF provider, or hand-created operation state cannot prove native resumability or durability.

For crash-after-side-effect proof, use a participating deterministic executor/provider that durably honors the propagated idempotency key. Report the guarantee precisely as exactly-once response acceptance and deduplicated participating governed effects, never arbitrary external exactly-once execution.

## Discovery and proof discipline

Before each subbundle implementation, declare exact focused filters and expected discovery floors. Record discovery and execution separately, with expected/actual counts and failing-first evidence for new behavior. Governed SB03–SB06 proof includes direct-test transcripts, source assertions, anti-stub audit, production composition evidence, changed-file hashes, and a downstream smoke.

For SB05, retain the entry characterization floor of 50 focused Unit tests and 7 focused
Integration tests. The final selector must exceed 75 tests because the full HTTP matrix and
direct facade/authorizer/validator/grant/mapper/caller-bypass proof are additive. Record
discovery independently from execution and preserve failing-first transcripts before product
implementation.
