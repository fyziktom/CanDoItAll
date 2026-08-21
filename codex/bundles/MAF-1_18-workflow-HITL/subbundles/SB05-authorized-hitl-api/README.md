# SB05 — Authorized and Idempotent Workflow HITL API

## Status

Proven — governed proof passed and CP-WB3 is Pass.

CP-WB2 remains trusted. The post-SB05 source/schema/API state is frozen for SB06; any
semantic edit to an owned source, API, persistence-format, DI, or test-fixture input
invalidates this closure and reopens the owning phase.

## Outcome

Complete the existing workflow pending-request/response API over one common application
service with typed JSON, trusted actor identity, resource authorization, validation,
idempotency, audit, stable outcomes, safe read projections, and focused operation status.

## Owned requirements

RQ-028, RQ-032 through RQ-040, and the API/documentation portions of RQ-043.

The owned raw intent is that the existing HITL response flow must be safe for every
production caller: same-key retries are stable, caller identity and scope are trusted,
workflow/model self-approval is impossible, invalid input is rejected before mutation,
audit is durable and redacted, and clients can distinguish waiting, resuming, completed,
denied, retryable failure, and terminal failure.

## Non-goals

- adding a second approval execution API;
- workflow UI redesign or client polling UI;
- accepting actor, database profile, scope, tenant, project, tool, policy, or capability
  arguments from an HTTP body, agent-tool input, or UI-selected value;
- adding per-user/project membership claims that the repository does not currently model;
- exposing raw MAF request/checkpoint objects, protected payloads, governed arguments,
  storage paths, or credentials;
- preserving the obsolete double-encoded `ResponseJson` HTTP DTO;
- adding a relational persistence migration for SB05;
- weakening current authentication or manufacturing an anonymous service actor;
- making endpoint lambdas, the Blazor component, or the agent tool own domain transitions.

## Prerequisites

Satisfied. SB04 is Proven and CP-WB2 is Pass with governed
checkpoint/operation/recovery proof under `proof/SB04`.

- Unit prerequisite selector: 419/419.
- Integration prerequisite selector: 16/16, including real PostgreSQL persistence and
  production composition.
- Migration `20260821021747_AddWorkflowHitlRecovery`: applied model matches the snapshot.
- CodeAnalytics snapshot `snap-20260821044013-44e660f5`: no project cycle and exactly the
  two named baseline non-project cycles.
- The persistent operation already owns actor, acceptance time, correlation, actor-scope,
  idempotency and payload fingerprints, protected canonical payload, attempts, state,
  outcome, and safe result. The external-request boundary already persists response
  contract, continuation, and authorization policy.

No external blocker prevented SB05 implementation. The user-owned web host was preserved;
validation used the authorized environment path when user-local or output locks required it.

## Reopen triggers

- IK-13: authorization is endpoint-only, a caller reaches raw manager submission, current
  database profile cannot be bound to persisted target scope, or recovery can resume
  without reconstructing and revalidating authorization.
- IK-14: a concrete repository client is found that requires the old double-encoded
  `ResponseJson` DTO. Stop for an explicit compatibility decision; do not silently add a
  second wire shape.
- IK-16: a selector discovers zero tests or a discovery count differs from its frozen
  expectation.
- IK-17: SB05 changes SB04 operation/CAS/recovery semantics or later behavior contradicts
  SB04 proof. Reopen SB04 and every dependent phase.
- IK-18: markup, layout, polling, or other UI redesign becomes necessary. Keep it out of
  SB05 unless scope is explicitly expanded and browser proof is added.
- Existing `OriginJson` or `AuthorizationPolicyJson` cannot round-trip the required
  server-owned scope snapshot. Stop and repair the plan before adding relational schema.
- Any requirement for a caller-supplied authorization grant, action, actor, or resource
  scope. Reject the requirement as unsafe rather than adding a fallback.
- Safe operation status cannot be authorized using the linked operation, request, run,
  and persisted scope without widening a lower-layer query surface.

## Exact sources and discovery

Exactly three production callers currently submit external responses:

1. `src/App/CanDoItAll.Web/Api/WorkflowsApi.cs` accepts raw `ResponseJson` and calls
   `IWorkflowRuntimeManager.SubmitExternalResponseAsync`.
2. `src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/WorkflowAgentRuntimeToolProvider.cs`
   accepts raw response JSON and currently discards trusted
   `AgentRuntimeToolProviderContext` governance at submission.
3. `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs` submits
   the response text through `RespondToExternalRequestAsync`.

No repository client requires the old HTTP DTO. It is removed, not versioned.

Primary implementation surfaces:

- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowServiceContracts.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowExternalResponseOperationContracts.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowResumeBoundaryContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowLaunchModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExternalRequestContinuationModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExternalResponseOperationModels.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowExternalResponseFingerprintFactory.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowExternalResponseSubmissionCoordinator.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowExternalResponseContinuation.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeManager.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowExternalRequestBoundaryStore.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowExternalResponseOperationStore.cs`
- `src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccessScopeNames.cs`
- `src/App/CanDoItAll.Web/Api/ApiAuthorizationPolicies.cs`
- `src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs`
- `docs/api-control-plane.md`

Tests are owned by:

- `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`

## Implementation boundary

### 1. One common application service

Add/use `IWorkflowExternalResponseService` and one concrete
`WorkflowExternalResponseService`. Its command contains only:

- request ID;
- positive expected request version;
- typed `JsonElement` response;
- typed idempotency key;
- server correlation ID;
- closed trusted actor context created by the caller adapter.

The service order is:

1. load safe run/request/boundary context;
2. authorize the trusted actor against current profile and persisted scope/policy;
3. validate kind, version, schema, payload, and native linkage;
4. create the canonical payload and scope-bound fingerprint;
5. atomically create or replay the durable operation;
6. continue through the existing SB04 lease/recovery path;
7. return one typed result for HTTP, tool, and UI mapping.

Authorization and validation run on every submission. Recovery separately reconstructs
and revalidates the authorization grant from the durable operation plus request boundary;
it must not trust an earlier in-memory decision.

All three production callers use this service. Remove the production raw manager response
methods and the compatibility coordinator, or reduce any unavoidable internal bridge to a
fail-closed test-only adapter. A fabricated
`Service("workflow-runtime-compatibility")` actor is forbidden.

### 2. Typed HTTP and tool contracts

The existing command route remains:

`POST /api/workflows/external-requests/{requestId}/response`

The only HTTP body is:

```json
{
  "expectedRequestVersion": 3,
  "response": {
    "approved": true,
    "message": "Reviewed and approved."
  }
}
```

`response` is a JSON value, never a JSON string containing encoded JSON. Unknown
top-level members are rejected. Require exactly one bounded `Idempotency-Key`. Enforce
JSON content type, body bytes, nesting depth, and duplicate-property rules for content
length and chunked requests.

The agent tool receives request ID, expected version, typed `JsonElement` response, and a
caller-stable idempotency key. It receives no actor or scope fields. The UI parses its
existing textarea once and uses the pending request's persisted version plus deterministic
key `workflow-ui:{requestId}:{version}`.

### 3. Trusted identity, profile, scope, and approval policy

The common actor context is server-created and records trusted channel, authenticated
actor, canonical database profile, workspace scope, response capabilities, policy
fingerprint, and authentication time/expiry when the channel supplies them.

- HTTP resolves the stable subject from authenticated claims and requires the exact
  workflow-response API scope. Anonymous or missing-subject principals remain
  unauthenticated.
- Agent tool submission derives identity, database profile, workspace scope, and
  capabilities from `AgentRuntimeToolProviderContext.Governance`, then re-reads active
  agent assignment/capability through module authorization.
- Blazor uses the authenticated user when authentication is enabled. In explicitly
  authentication-disabled local mode it may use a named server-owned local-operator
  identity bound to the current database profile; it never uses UI input for identity.

Persist the target database profile/workspace scope additively in existing JSON-backed
state: run `OriginJson` and/or request-boundary `AuthorizationPolicyJson`. Project launches
retain Project scope; governed agent launches retain their admitted governance scope;
other server launches use the canonical current-profile Organization scope unless a
narrower server-owned scope exists. Cross-profile access is always denied. Agents require
their exact admitted workspace scope; organization-scoped human/API authority may cover a
server-verified narrower run only within the same canonical profile. Missing, legacy, or
unresolvable scope/policy fails closed. This does not claim a per-user project ACL that the
repository does not implement.

For Approval and ToolApproval, autonomous agent/workflow/model actors cannot approve, and
an originating autonomous service/agent cannot approve its own request. An authenticated
human who launched a workflow is not automatically self-approval; assignment, capability,
profile, scope, and persisted policy still decide access.

### 4. Validation, action, and recovery without a relational migration

Validate before operation creation:

- request/run relationship, pending-compatible state, and expected version;
- response contract kind and schema version;
- approval object with `approved: boolean` and optional bounded `message`;
- HumanInput against its persisted JSON schema;
- UTF-8 size, explicit depth, syntax, duplicate properties, and payload policy;
- native continuation/checkpoint linkage and exact workflow/topology compatibility.

Do not pre-reject a same-key replay merely because the request has advanced to
`ResponseClaimed`, `Responded`, or `Denied`; the operation store must first resolve the
existing same-fingerprint operation.

No SB05 relational column, table, model-snapshot edit, or EF migration is planned:

- scope/policy is additive JSON in existing `OriginJson`/`AuthorizationPolicyJson`;
- actor, authoritative decision time, hashes, correlation, protected payload, and outcome
  already exist on the durable operation;
- recovery reconstructs the authorization grant from operation plus boundary and
  reauthorizes it;
- action is derived only after validating the protected canonical payload:
  `approved=true` is Approve, `approved=false` is Deny, and HumanInput is SubmitInput.

Do not accept or persist a caller-provided action or authorization grant. Historical rows
with missing scope/policy or an unclassifiable payload fail closed with a typed safe
outcome. If implementation proves the existing JSON persistence insufficient, reopen this
entry decision before adding schema.

### 5. Idempotency and focused operation status

Use the existing operation store semantics:

- same key, actor scope, request/version, and canonical payload replays the same operation;
- same key with different payload or actor scope is conflict;
- a same-operation replay in Accepted/Claimed/Resuming maps to active 202 status;
- a completed operation returns its stable terminal projection;
- a competing operation cannot claim the same request;
- every retry and recovery remains bounded by SB04 CAS, lease, and attempt rules.

Add the focused read endpoint:

`GET /api/workflows/external-response-operations/{operationId}`

It loads the operation, authorizes against its linked request/run/profile/scope, and
returns only safe operation state/outcome, replay-independent timestamps, run state,
safe message, and next pending request identity/projection. It does not add a second
mutation API.

### 6. Safe projections, audit, and documentation

Map start/detail, pending request, historical event, artifact, checkpoint, response, and
operation status through explicit Web-owned DTOs. Public output excludes:

- `RequestJson`, `ResponseJson`, and raw event `PayloadJson`;
- checkpoint payload, native checkpoint IDs, payload references, and hashes;
- artifact storage paths;
- credentials, protected idempotency keys, unrestricted governed arguments, and internal
  authorization-policy material.

Safe output may include public IDs, kinds, versions, lifecycle state, timestamps, bounded
summary/message, operation state/outcome, and a safe next-pending-request projection.

The durable operation is the accepted-attempt audit record: actor, action derived from
protected payload, request/run IDs, payload and idempotency hashes, correlation,
timestamps/attempts, state/outcome, and safe diagnostic. Denied authorization and
validation attempts produce structured redacted diagnostics without raw response,
idempotency key, checkpoint, or secrets.

Move response/pending/status endpoint binding and DTO/result mapping into focused Web
types so `WorkflowsApi.cs` materially shrinks. Update OpenAPI metadata, route contract
listing, and `docs/api-control-plane.md`.

### 7. HTTP outcome map

| Condition | HTTP |
|---|---:|
| Completed, waiting again, denied, or stable terminal replay | 200 |
| Same operation accepted, claimed, or resuming | 202 |
| Invalid header/content type/body/JSON/schema/size/depth | 400 |
| Unauthenticated or missing stable subject | 401 |
| Wrong capability/profile/scope/assignment or self-approval | 403 |
| Missing request, run, or operation | 404 |
| Stale version, different-payload replay, competing operation, already answered, or run not waiting | 409 |
| Cancelled or superseded request | 410 |
| Legacy/missing/corrupt/incompatible checkpoint or topology/workflow-version mismatch | 422 |
| Retryable backend, store, or lease failure | 503 |
| Unexpected or unclassified terminal recovery failure | safe 500 |

Do not map deterministic local resume failure to 502.

### 8. UI composition freeze

No visual redesign is planned. The workflows page remains the primary surface; its
existing pending-response editor is orchestration only. There are no new stats, polling
panels, dialogs, or supporting cards. Preserve the existing first-viewport task and scroll
owner, and keep the existing textarea sizing unless its contract is already invalid. If
markup, layout, or styling changes become necessary, trigger IK-18 and add large-screen
normal/open-state browser proof before closure.

### 9. CP-WB3 entry architecture

- Neutral command/result/actor/authorizer/validator contracts live in
  Workflows.Abstractions or Models with no ASP.NET, EF, or MAF types.
- Pure validation/fingerprint policy stays in Workflows.Core.
- Orchestration stays in Workflows.Runtime and reuses SB04 stores/continuation.
- Current-profile, agent assignment/capability, and persistence resolution stay in
  Modules.AgentFramework.
- HTTP claims, request reading, DTOs, safe projection, and status mapping stay in Web.
- Web does not reference MAF or persistence entities; Core/Runtime do not reference Web,
  EF, or MafAdapter.
- No new project, second manager, second response API, handwritten production partial,
  nested extracted service, or service locator is allowed.
- Direct tests must instantiate the service, authorizer, validator, action/grant
  reconstruction, mapper, and redaction projection without `WebApplication` or
  `WorkflowRuntimeManager`.

CP-WB3 closure requires a fresh CodeAnalytics snapshot, no new cycle/reference leak, the
two named baseline non-project cycles unchanged, all three production callers on the
common service, production DI resolution, and a materially thinner `WorkflowsApi.cs`.

## Acceptance criteria

- the existing POST remains the single response command route;
- the focused operation-status GET is present and uses the same authorization boundary;
- typed JSON is accepted without double encoding and the old HTTP DTO is absent;
- actor/profile/scope come only from trusted server context;
- all three production callers use the common service and no raw manager bypass remains;
- unauthorized, wrong-scope, missing-policy, and self-approval submissions perform no
  operation/backend mutation;
- request kind/schema/version/size/depth/linkage are validated;
- same-key/same-payload returns a stable replay and changed payload/scope conflicts;
- completed, resuming, waiting-again, denied, cancelled, retryable failure, and terminal
  recovery failure remain distinguishable;
- start/detail/pending/event/artifact/checkpoint/operation projections contain no raw
  checkpoint JSON, protected payload, credentials, storage paths, or governed arguments;
- accepted attempts have durable actor/action/hash/correlation/timestamp/outcome audit and
  denied attempts have redacted diagnostics;
- recovery reconstructs and revalidates authorization from durable state;
- no SB05 relational migration or model-snapshot edit is introduced;
- OpenAPI and `docs/api-control-plane.md` match runtime;
- CP-WB3 and Governed proof pass before SB06 is unlocked.

## Proof tier

Governed

Create `proof/SB05/manifest.md` and `proof/SB05/semantic-invariants.md`. The manifest must
include changed-file before/after SHA-256 hashes, exact command transcripts, failing-first
and passing transcripts for the same invariants, production source assertions, safe
projection/audit assertions, anti-stub output, production composition proof, and a
downstream status/read smoke. No closure claim may cite an absent artifact.

The shallow-pass traps to defeat are:

- endpoint policy alone while tool/UI bypass raw manager submission;
- typed DTO at the edge while internal code still accepts double-encoded JSON;
- authorization checked once but bypassed by crash recovery;
- actor equality without database-profile and persisted-scope enforcement;
- status DTO that leaks raw domain/persistence fields;
- idempotency tests that never vary payload or actor scope;
- seeded/fake operation rows in place of production service/host/PostgreSQL behavior.

Required semantic positive proof is a real authenticated typed response through production
Web/service/PostgreSQL/MAF that completes or waits again and replays stably. Required
adversarial proof includes a wrong-profile/scope or autonomous self-approval request that
creates no operation, a changed-payload replay conflict, and recovery with missing/corrupt
authorization scope that fails closed.

## Focused validation

### Frozen entry baseline

The exact Unit filter below discovered and passed 50 tests:

```text
FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.WorkflowAgentRuntimeToolProviderTests.StatusCancellationAndResponseToolsPreserveTypedRuntimeOutcomes
|FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.WorkflowAgentRuntimeToolProviderTests.AttachedToolReauthorizesActorAndCatalogAtInvocationTime
|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.WorkflowExternalResponseSubmissionCoordinatorTests
|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.WorkflowExternalResponseContinuationTests
|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.WorkflowExternalResponseValidationTests
|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.WorkflowExternalResponseFingerprintFactoryTests
|FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.WorkflowRuntimeLifecycleRedGateTests.InProcessExternalResponseRemainsWaitingWhenResumeUnsupported
|FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.WorkflowRuntimeLifecycleRedGateTests.ResumeCapableBackendAcceptsExternalResponseExactlyOnce
|FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.WorkflowRuntimeLifecycleRedGateTests.ExternalResponseContractRequiresTypedOutcomeAndResumeCapableBackendPort
```

Entry composition was 2 exact agent-tool tests, all 6 coordinator tests, all 22
continuation tests, all 8 validation tests, all 9 fingerprint tests, and 3 exact lifecycle
tests: 50 discovered / 50 passed.

The exact Integration filter below discovered 7 tests:

```text
FullyQualifiedName=CanDoItAll.Tests.Integration.AgentFramework.WorkflowCallerLaunchBoundaryIntegrationTests.WorkflowApi_StartsExactAndLatestActiveWithAuthenticatedServerOriginAndRejectsSpoofedLineage
|FullyQualifiedName=CanDoItAll.Tests.Integration.AgentFramework.WorkflowCallerLaunchBoundaryIntegrationTests.WorkflowApi_MapsTypedCancellationAndExternalResponseOutcomesToHttpSemantics
|FullyQualifiedName=CanDoItAll.Tests.Integration.AgentFramework.WorkflowApiIntegrationTests.Workflow_contract_lists_control_and_validation_routes
|FullyQualifiedName=CanDoItAll.Tests.Integration.AgentFramework.WorkflowApiIntegrationTests.Openapi_exposes_workflow_routes
|FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiAccessAuthorizationIntegrationTests
```

Entry composition was 2 exact launch/response tests, 2 exact route/OpenAPI tests, and all
3 API access-authorization tests: 7 discovered. The first sandbox execution failed only
because the runner could not write the user-local generation lock. The identical
permission-enabled execution passed 7/7. This is an environment exception, not a product
failure.

### Failing-first and final selectors

Before production changes, add and capture failing-first cases for:

- common service order and no mutation after authorization/validation failure;
- authenticated actor/profile/scope/capability and autonomous self-approval negatives;
- approval/HumanInput schema, expected version, size, depth, duplicate, and linkage;
- authorization reconstruction on retry/recovery and derived action;
- all three callers using the common service with no raw-manager submission;
- every HTTP status and safe projection/redaction outcome;
- operation status authorization and next-pending projection;
- production DI and real host/PostgreSQL/MAF behavior.

Use stable class/topic selectors for new
`WorkflowExternalResponseServiceTests`,
`WorkflowExternalRequestAuthorizerTests`,
`WorkflowExternalResponseValidatorTests`,
approval-authorization/recovery tests, caller-boundary source guards, HTTP mapper/redaction
tests, and `WorkflowAuthorizedHitlApiIntegrationTests`. Retain affected continuation,
fingerprint, lifecycle, tool-provider, route/OpenAPI, access-authorization,
persistence/composition, and caller-launch coverage.

The prepared matrix had 16 bullets; splitting legacy, missing checkpoint, and topology
mismatch into separate executable cases produces 18 required HTTP cases:

1. approve success;
2. deny success;
3. HumanInput success;
4. consecutive wait;
5. anonymous;
6. wrong profile/scope;
7. autonomous self-approval;
8. invalid schema/body;
9. stale request version;
10. idempotent replay;
11. conflicting replay;
12. active same operation;
13. cancelled/superseded request;
14. missing request/operation;
15. legacy non-resumable;
16. missing/corrupt checkpoint;
17. topology/workflow-version mismatch;
18. retryable backend/store unavailable.

Final SB05 focused discovery must be greater than 75 tests across the frozen Unit and
Integration selectors, with every required HTTP case present and passed. Record list-only
discovery separately from execution, then run the identical filters with TRX. The 50+7
entry tests are the retention baseline. If removing the obsolete coordinator removes a
test class, map every displaced case to named common-service tests in the manifest; do not
silently lower the behavioral floor. Zero or unexpected discovery is failure.

Build the affected production and test projects in Release before `--no-build` execution.
Record exact commands, working directory, configuration, dependency roots, start time,
exit code, discovered/passed/failed/skipped counts, and sanitized output under
`proof/SB05/transcripts`.

## Invalidation keys

IK-13, IK-14, IK-16, IK-17, and IK-18.

Focused evidence is invalidated by any semantic change to:

- response command/result/actor/scope/authorization contracts;
- launch-origin or request-boundary JSON serialization;
- operation fingerprint/create/replay/recovery behavior;
- approval/HumanInput validation or executor authorization;
- any of the three production callers or runtime/module/Web DI;
- HTTP reader, DTO, mapper, route metadata, safe projection, or API policy;
- selected tests, filters, fixtures, PostgreSQL schema baseline, or dependency roots.

Documentation, proof manifests, checksums, and non-semantic bundle wording do not by
themselves invalidate built/tested binaries. If an affected source/test input changes,
rerun only its owning focused selector and downstream composition check, then refreeze.

## Broad-gate decision

Do not run FG-01 in SB05. The named broad trigger is the new public response-service/API
serialization contract and changed runtime/module/Web composition consumed across
otherwise independent packages. After all affected focused checks pass, declare
**CP-WB3 / post-SB05 source-schema-API freeze** with exact git diff state, project graph,
dependency roots, public route/OpenAPI surface, JSON persistence format, tests/filters,
and documentation.

SB06 runs the authorized broad stable gate once against that frozen state. Any later
semantic source, persistence format, API, DI, or test-fixture edit reopens its owning
subbundle, invalidates CP-WB3 and any FG-01 result, and requires a new freeze. SB05
introduced no relational migration; discovery that one is required reopens this decision
and the frozen state before FG-01.

## Closure record

Executed on 2026-08-21. SB05 is Proven and CP-WB3 is Pass.

- Common boundary: one neutral `IWorkflowExternalResponseService` authorizes, validates,
  creates/replays the durable operation, continues through the SB04 recovery path, and
  maps safe typed results. The focused authorizer, validator, authorization-grant factory,
  result mapper, recovery coordinator, and startup worker are directly testable top-level
  collaborators.
- Callers and bypass: exactly the Web POST, `WorkflowAgentRuntimeToolProvider`, and
  `WorkflowsPage.razor.cs` mutate responses. All three call the common service; production
  raw manager/coordinator response submission is absent.
- Wire contract: the existing POST accepts a bounded typed `JsonElement` response plus a
  positive expected request version and one `Idempotency-Key`. The obsolete
  double-encoded `ResponseJson` HTTP body is removed.
- Trusted authorization: HTTP claims, agent governance, and the server-owned local UI
  actor create closed actor contexts. Profile/scope/policy is server-owned and persists in
  existing `OriginJson` and `AuthorizationPolicyJson`; initial continuation and recovery
  reconstruct and revalidate the same durable authorization evidence and derive action
  only from the validated protected payload.
- Schema: no SB05 table, column, entity, model-snapshot edit, or EF migration was created.
  Migration `20260821021747_AddWorkflowHitlRecovery` remains the latest HITL relational
  migration, and the pending-model check passes.
- Idempotency and status: identical key/scope/request/version/payload replays the same
  operation; changed payload or scope conflicts. The focused operation-status GET uses the
  same linked request/run/profile/scope authorization boundary and returns a
  replay-independent safe projection.
- Projections and audit: explicit Web DTO allow-lists cover run/detail, pending request,
  event/SSE, artifact, checkpoint, response, and operation status. Raw payloads,
  checkpoint/native identifiers, hashes, storage paths, credentials, governed arguments,
  protected keys, and policy material are excluded. Accepted operations retain durable
  actor/action/hash/correlation/timestamp/outcome audit; rejection diagnostics are
  structured and redacted.
- HTTP matrix: all 18 required cases pass, including approve/deny/HumanInput, consecutive
  wait, authentication/authorization negatives, schema/version conflicts, stable and
  conflicting replay, cancellation, missing and incompatible state, retryable failure,
  and safe terminal failure. No deterministic response outcome maps to 502.
- Real production path: authenticated typed Web -> common service -> PostgreSQL -> MAF
  completes and replays with the same operation and no new events. Real adversarial proof
  denies an insufficient API scope before operation creation, returns 409 for changed
  payload under the same key without a second operation or new events, and returns 410 for
  a late response after real cancellation without creating an operation or response event.
- OpenAPI and documentation: route metadata, typed schemas, status codes, safe projections,
  and `docs/api-control-plane.md` match the runtime contract.
- Final focused proof: the exact 22-class Unit selector discovered and passed 297/297; the
  exact 11-class Integration selector discovered and passed 137/137; neither skipped a
  test. Combined governed execution is 434 and exceeds the entry floor.
- Build and architecture: all affected Release project builds passed with zero warnings
  and zero errors. Final snapshot `snap-20260821072204-bf844210` has zero project cycles,
  unchanged project references, and exactly the two baseline non-project cycles from
  `snap-20260821044013-44e660f5`.
- Governed evidence: `proof/SB05` records semantic invariants, exact selectors, raw TRX,
  source/no-bypass/safe-projection/no-migration assertions, production composition,
  progression failures, passing proof, and the CP-WB3 review.
- Honest progression: the first frozen Unit run is retained at 296/297 before a test-only
  size-cap correction. Integration retains the 75/137 sandbox profile-lock failure and the
  136/137 stale `MetadataOnly` assertion before the corrected 137/137 run. These artifacts
  are not relabeled as passing proof.
- Blockers/deviations: none remain for SB05. SB06 is dependency-ready and alone owns the
  restartable E2E matrix, final input/documentation audit, and single FG-01 broad gate.
