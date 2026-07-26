# C# Architecture Gate

## Preparation Decision

Status: `Pass for SB01 entry`

- Responsibility boundaries: explicit.
- Dependency direction: explicit and cycle-safe on the preparation snapshot.
- Patterns: selected only for archive modes, transport adaptation, durable idempotency,
  readiness projection, and compatibility facades.
- Test seams: direct unit and HTTP integration proof defined.
- Partial-class policy: no new partial file is accepted as a final boundary.

## Closure Checklist

- [x] before/after scoped CodeAnalytics snapshot and dependency/cycle result
- [x] no new unjustified partial or nested implementation type
- [x] focused services instantiated directly by unit tests
- [x] old broad classes shrink or contain delegation only for new responsibilities
- [x] composition smoke passes
- [x] downstream unlock decision recorded after each completed critical subbundle

## SB01 C# Architecture Gate Result

Status: `Pass`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | Multipart, policy, and archive responsibilities have distinct owners. | `AgentPackageImportApi`, `AgentPackageImportService`, and `ZipAgentPackageService`; source assertion and tests. | None. |
| Follow-up | SB02 must turn the imported external key into the same durable identity model used by provisioning. | SB01 receipt/ledger retains the key but SB02 owns normalized binding uniqueness. | Reopen SB01 if the binding cannot be added atomically. |

### Dependency direction

- Scoped snapshot `snap-20260725233309-b2a91453`: Models has no project references; Core
  references Models; Persistence references Core/Models; no matching dependency cycles.
- No `.csproj` changed.

### Partial-class policy

- No partial file was added for package policy. Existing workspace and current-profile
  facades only delegate/synchronize.

### Testability proof

- Six direct archive tests, three direct `AgentPackageImportService` policy tests with
  in-memory fakes, and two HTTP integration tests all passed.

### Closure decision

- SB01 may close and SB02 may proceed. The external-identity follow-up is an explicit SB02
  dependency and SB01 reopen trigger.

## SB02 C# Architecture Gate Result

Status: `Pass`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | One focused Core service owns external identity, fingerprint, precondition, ledger, and atomic mutation policy. | Direct service tests plus Web/facade source assertions. | None. |
| None | Package import now uses the same durable binding model. | Package import unit assertion and HTTP lookup through the external-key route. | SB01 remains closed. |

### Dependency direction

- Scoped snapshot `snap-20260726002038-b2a91453` analyzed 1,898 dependency edges with
  zero cycles and no blocking errors.
- Models remains dependency-free; Core owns policy; Persistence owns storage; Web owns
  transport. No `.csproj` changed.

### Partial-class policy

- No new partial file owns provisioning policy. Existing workspace and current-profile
  partial/facade surfaces contain delegation and projection synchronization only.

### Testability proof

- The internal provisioning service is directly instantiated with an in-memory store.
- The production route is exercised against the real file store and cross-process lock,
  including parallel identical requests and stale/conflicting negatives.

### Closure decision

- Critical SB02 may close. Package import remains trusted, and SB03 may proceed.

## SB03 C# Architecture Gate Result

Status: `Pass`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | The public execution request uses a portable, versioned JSON Schema contract and does not expose the trusted in-process `AgentStructuredOutputContract` or runtime `Type`. | Focused OpenAPI assertions and scoped HTTP execution proof. | None. |
| None | Validation, provider adaptation, transport, and durable evidence remain separately owned. | Models owns the DTO/evidence; Core owns bounded preparation and local validation; MAF adapts the exact schema to provider response format; Web owns request mapping. | None. |
| Observation | `AgentJsonSchemaOutputContractProcessor.cs` is a large cohesive policy implementation. | CodeAnalytics complexity warning; all entry points belong to one bounded schema preparation/validation responsibility and are directly tested. | Reassess extraction if later schema vocabulary support adds a second responsibility. |

### Dependency direction

- Scoped snapshot `snap-20260726012650-f8ae8df6` analyzed 4 projects and 293
  documents with zero dependency cycles and no blocking errors.
- Models remains dependency-free; Core references Models; MAF references Core/Models; Web
  owns transport composition. No `.csproj` changed.

### Partial-class policy

- The new validator is a top-level focused type. Existing execution partials retain
  orchestration and delegate portable-schema policy to it.

### Testability proof

- Eleven direct processor tests cover deterministic hashing, schema limits, strictness,
  unsupported vocabulary, valid output, provider refusal, malformed JSON, and
  schema-invalid output.
- A 102-test compatibility slice proves the existing typed-contract and reflection
  surfaces remain intact.
- The deterministic fake-provider integration proves provider options, durable
  success/failure evidence, the scoped HTTP request, and generated OpenAPI without
  runtime type leakage. The existing workflow OpenAPI characterization also passes.

### Closure decision

- Critical SB03 may close. Its durable evidence contract remains available to SB06/SB07,
  and SB04 may proceed.

## SB04 C# Architecture Gate Result

Status: `Pass`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | Stable lookup targets the canonical catalog abstraction instead of an in-memory side store. | `WorkflowStableIdentityLookupService` depends on `IWorkflowCatalogService`; final-host tests seed and query `PersistentWorkflowCatalogService`. | None. |
| None | Persistence and transport responsibilities remain separately owned. | Workflows Core owns normalization/resolution; AgentFramework persistence owns durable projections and uniqueness; the focused `WorkflowStableIdentityApi` owns HTTP mapping. | None. |
| None | External uniqueness is enforced at both policy and database boundaries. | Normalized preflight conflict check plus filtered unique head index in `20260726022532_AddWorkflowStableExternalIdentity`. | None. |

### Dependency direction

- Final scoped snapshot `snap-20260726023746-5a6a0c3e` analyzed 5 projects and 265
  documents with no blocking errors.
- Its only module and type cycles use the exact node ids recorded in the preparation
  snapshot, so SB04 introduced no dependency cycle.
- Models remains dependency-free; Abstractions owns the query contract; Core owns policy;
  AgentFramework owns persistence/seeding; Web owns transport. No `.csproj` changed.

### Partial-class policy

- The stable lookup service and HTTP mapper are focused top-level types. No new partial
  class owns stable-identity policy.

### Testability proof

- Eight direct unit cases cover normalized exact lookup, missing and ambiguous results,
  suspended/archived staleness, active-version pinning, duplicate rejection, and
  provenance projection.
- Four persistent-host integration cases cover all public routes and filters, OpenAPI
  parameters/schemas, authentication, and workspace isolation.
- The migration project builds and EF reports no pending model changes.

### Closure decision

- SB04 may close. Its explicit workflow id and runnable version id are stable inputs for
  the SB05 idempotency fingerprint, so critical SB05 may proceed.

## SB05 C# Architecture Gate Result

Status: `Pass`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | The existing launch service and durable ledger remain the only claim/concurrency owner. | Both HTTP routes construct caller-supplied idempotency and call `IWorkflowLaunchService`; no endpoint lock or cache was added. | None. |
| None | Workspace API-key uniqueness is enforced durably without weakening internal caller scopes. | API-origin lookup/claim uses the filtered unique key index; non-API claims retain the full existing scope. | None. |
| None | Persistence, query policy, and transport remain separately owned. | AgentFramework owns the entity/store; Workflows Core owns canonical hashing and safe evidence projection; the focused `WorkflowRunIdempotencyApi` owns lookup mapping. | None. |

### Dependency direction

- Final scoped snapshot `snap-20260726032132-5a6a0c3e` analyzed 5 projects and 267
  documents with no blocking errors or matching architecture findings.
- Its module and type cycles use the exact node ids recorded for SB04:
  `efe376421f64`/`f602d7c77eb2` and `5374bd3c4751`/`a9e2e15d6c60`.
  SB05 therefore introduced no dependency cycle.
- Models remains dependency-free; Abstractions owns store/query contracts; Core owns
  canonicalization and evidence projection; AgentFramework owns persistence; Web owns
  HTTP binding. No `.csproj` changed.

### Partial-class policy

- The query service and HTTP lookup mapper are focused top-level types. No partial or
  nested type was added to own idempotency policy.

### Testability proof

- Twenty-five focused unit tests pass, including the existing launch-service suite and
  direct canonical JSON/fingerprint equivalence tests.
- Five persistent integration tests pass, including existing atomic claim/crash recovery,
  eight concurrent HTTP submissions collapsing to one run, replay/conflict semantics,
  safe terminal-state lookup, authorization, and OpenAPI header/response metadata.
- The migration project builds and EF reports no pending model changes.

### Closure decision

- Critical SB05 may close. Its stable original run id and retry-safe public launch contract
  unlock typed recruiting evidence in SB06.

## SB06 C# Architecture Gate Result

Status: `Pass`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | The agent domain is the canonical owner of interview evidence; CRM-HR remains untouched and optional. | Models, Core service/projector, and profile-scoped file store live in AgentFramework; no CRM-HR source changed. | None. |
| None | Runtime dependencies are represented by exactly one primitive typed target and resolved only in Web composition. | `AgentRecruitingExecutionTarget` plus `WorkspaceAgentRecruitingTargetResolver`; Core has no workflow/process implementation reference. | None. |
| None | Automated evaluation, qualifying human authorization, readiness projection, and activation are distinct. | Direct projector tests and full-host API assertions; readiness always reports `ActivatesAgent=false`. | None. |

### Dependency direction

- Final scoped snapshot `snap-20260726043515-7a05e048` analyzed 5 projects and 379
  documents with no blocking errors.
- Its only module and type cycles retain the exact pre-existing suffixes
  `efe376421f64`/`f602d7c77eb2` and `5374bd3c4751`/`a9e2e15d6c60`.
- Models owns portable evidence records; Core owns policy and projection; Persistence owns
  durable append-only storage; Web owns workflow/process/agent target adaptation and
  transport. No `.csproj` changed.

### Partial-class policy

- Recruiting policy is implemented by focused top-level service, validator, projector,
  resolver, persistence, and endpoint types. No new partial class owns the policy.
- The service and validator have no CodeAnalytics findings or open questions.

### Testability proof

- Thirteen direct service/projector tests pass.
- Five full-host API/OpenAPI tests pass, including authorization, workspace isolation,
  concurrent append-only persistence, all target discriminators, evidence negatives, and
  the human-gated/no-activation invariant.
- Twenty-four direct concrete-resolver tests pass across every agent/workflow/process run
  state, missing-target semantics, identity projection, and isolated backing stores.

### Closure decision

- Critical SB06 closes. Its typed response contracts and full behavior proof unlock SB07.

## SB07 C# Architecture Gate Result

Status: `Pass`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | Transport metadata and authorization error serialization remain Web-owned. | `ApiEndpointMetadata`, JWT event mapping, and route-family metadata live in `CanDoItAll.Web`; no domain or persistence project changed for SB07. | None. |
| None | Runtime and OpenAPI use one shared error shape for owned routes. | Stable-identity metadata and workflow-start validation were normalized to `ApiErrorResponse`; representative runtime parity passes. | None. |
| Baseline | `AgentsApi.cs` and `WorkflowsApi.cs` remain pre-existing large route-family files. | CodeAnalytics baseline complexity findings predate SB07; changes are metadata and narrow result mapping only. | Reassess only if route-family responsibility expands. |

### Dependency direction

- Final scoped snapshot `snap-20260726050323-7a05e048` analyzed 5 projects and 379
  documents with no blocking errors.
- Its only module and type cycles retain the exact pre-existing suffixes
  `efe376421f64`/`f602d7c77eb2` and `5374bd3c4751`/`a9e2e15d6c60`.
- No project file, domain ownership, or persistence boundary changed.

### Partial-class policy

- No partial or nested policy type was introduced. The shared endpoint metadata helper is
  a focused top-level Web type.

### Testability proof

- Four focused host-generated OpenAPI/runtime tests cover 22 unique operations, schema
  details, portable type isolation, real payload parity, and the actual 401 envelope.
- The final Web host builds with zero errors.

### Closure decision

- SB07 closes. Its host contract is the sole source for SB08 canonical capture and
  SharedInfo/installed-skill synchronization.

## SB08 And Final Architecture Gate Result

Status: `Pass`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | SharedInfo publication did not introduce a product-to-SharedInfo filesystem dependency. | Product host generated the contract; SharedInfo owns only copied generated assets, reusable guidance, and its validator. | None. |
| None | Published operation ownership matches the final product boundaries. | Separate Agents, Agent Recruiting, Workflows, and Processes manifest operation sets and route appendices all validate. | None. |
| Limitation | Product working tree was not committed before publication. | Manifest and support README record baseline HEAD, `workingTreeClean: false`, and the limitation. | Refresh provenance after a product commit when commit-clean publication is required. |

### Dependency direction

- No C# source or project file changed during SB08.
- Final product evidence remains snapshot `snap-20260726050323-7a05e048`: no blocking
  errors and only the exact pre-existing AgentFramework module/type cycle pair.
- SharedInfo has no runtime/product project reference.

### Testability proof

- Generated OpenAPI, manifest operation sets, route appendices, repository structure,
  skill frontmatter, and active package copies all have deterministic validators or hash
  comparisons.

### Final closure decision

- SB08 and the initiative architecture gate pass. All product and publication
  subbundles may remain closed.
