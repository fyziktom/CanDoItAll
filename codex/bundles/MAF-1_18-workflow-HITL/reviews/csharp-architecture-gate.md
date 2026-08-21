# C# Architecture Gate Review

## Review state

- Review: Wave B final implementation and frozen closeout
- Status: **CP-WB1, CP-WB2, CP-WB3, and CP-WB4 Pass**
- Evidence date: 2026-08-21
- CodeAnalytics snapshots: re-anchor `snap-20260820203442-90bdd166`; focused architecture `snap-20260820220112-5cb38069`; SB03 final `snap-20260821002934-bf844210`; SB04 final `snap-20260821044013-44e660f5`; SB05 final `snap-20260821072204-bf844210`; SB06 focused final `snap-20260821092959-44e660f5`
- Applicable subbundles: SB03, SB04, SB05, and final SB06 verification

## Gate findings

### Boundary and dependency direction — Pass

The implementation uses existing project boundaries, adds no production project, places
neutral records/ports below implementations, and keeps MAF, EF/Npgsql, and ASP.NET
dependencies at their proper edges. Final focused snapshot
`snap-20260821092959-44e660f5` has zero project cycles, unchanged project references, and
exactly the same two baseline non-project cycles. Subsequent narrow broad-diagnostic repairs
add no project reference. Forbidden references and cycle remediation remain explicit.

### Responsibility extraction — Pass through CP-WB4

The implemented compiler binding collaborator, checkpoint adapter, correlator, native start
driver, response driver, rehydration verifier, turn-result mapper, response submission and
continuation coordinators, lease heartbeat, focused PostgreSQL stores, and executor dedup
decorator own focused testable responsibilities. `MafWorkflowCompiler` and
`MafInProcessWorkflowExecutionBackend` remain thin, non-partial facades.
`WorkflowRuntimeManager` is 738 lines versus the 748-line baseline; public entry points are
thin factory delegates and the compatibility construction path is test-only. SB05 adds one
neutral response-service facade plus focused top-level authorizer, validator,
authorization-grant factory, result mapper, recovery coordinator, and worker. Focused Web
types own HTTP binding/projection, while all three mutation callers delegate to the common
service. No old owner retains a competing response decision path.

### Pattern justification — Pass

Every selected Adapter, Builder, State, Decorator, Strategy, and Facade has a concrete force, rejected alternatives, named types, direct test seam, and required proof. No pattern is selected merely for naming or indirection.

### Testability — Pass through CP-WB4

Direct construction tests cover the checkpoint adapter, binding compiler/factory, correlator,
turn-result mapper, verifier, drivers, operation transitions, continuation, lease/heartbeat,
fingerprint/result mapping, and dedup decorator. The immutable SB04 Unit selector passes
419/419. The exact three-class Integration selector passes 16/16, with 15 facts using the
real PostgreSQL test database and one production-composition fact. Migration
`20260821021747_AddWorkflowHitlRecovery` has no pending model changes. The exact SB05
selectors pass 297/297 Unit and 137/137 Integration with zero skipped tests. The Integration
selector includes real authenticated Web/service/PostgreSQL/MAF completion/replay and real
scope, changed-payload, and cancellation adversarial cases. Direct service, mapper,
authorizer, validator, grant-reconstruction, recovery, caller, and safe-projection seams are
covered without requiring only the old facade.

### Partial-class policy — Pass

No handwritten production partial is allowed. Generated EF migration/designer partials are the sole exception. Nested services, forwarding helpers, service location, and tests only through old facades are explicit blockers.

### CP-WB3 closure review — Pass

| Gate finding | Closure evidence |
|---|---|
| Three production callers previously bypassed a governed common boundary | The Web POST, `WorkflowsPage.razor.cs`, and `WorkflowAgentRuntimeToolProvider` are exactly the three production mutation callers; all call one `IWorkflowExternalResponseService`, and source assertions find no raw manager/coordinator response path |
| Background recovery cannot depend on an HTTP principal | Initial and startup/lease recovery use the same durable authorization-grant reconstruction over operation and request-boundary evidence; incomplete, expired, corrupt, or mismatched evidence fails before backend/executor delivery |
| Existing security data does not establish a general per-user project ACL | The authorizer enforces canonical profile, persisted target scope, capabilities, assignment, and self-approval policy; agents require exact admitted scope, while same-profile organization-scoped human/API authority covers only server-verified narrower targets. No fabricated per-user ACL is claimed |
| Public projections could expose domain/persistence data | Explicit Web DTO allow-lists and serialization tests cover run, event/SSE, pending request, artifact, checkpoint, response, and operation status; raw/protected payload, native checkpoint data, hashes, storage paths, credentials, governed arguments, and policy material are excluded |
| An additive authorization migration would duplicate durable data | Scope/policy use existing `OriginJson`/`AuthorizationPolicyJson`; actor/time/fingerprint/protected payload/outcome use the SB04 operation. EF pending-model validation passes and no SB05 migration or model-snapshot change exists |

The implemented dependency direction is Web/Module adapters -> neutral facade -> neutral
ports, with persisted-scope authorization and persistence composition in
Modules.AgentFramework. Core/Runtime remain free of ASP.NET, EF/Npgsql, and MAF types.
Production DI, safe projections, recovery behavior, exact three-caller ownership, real
Web/PostgreSQL/MAF execution, and governed proof have passed CP-WB3.

### CP-WB4 closure review — Pass

| Gate finding | Closure evidence |
|---|---|
| Implicit plugin assembly discovery could export private nested fixture executors | Discovery requires a visible, concrete, closed `IWorkflowExecutor`; explicit type registration remains unchanged; the focused Integration fact passes 1/1 |
| In-memory native resume could persist protected response content in the source request | A typed internal redacted-acceptance capability persists blank source `ResponseJson`; the protected operation retains the payload; unsupported native compatibility fails during construction; lifecycle selector passes 14/14 |
| The in-memory operation store exceeded its architecture budget | Cancellation planning moved to the cohesive top-level `InMemoryWorkflowExternalResponseCancellation`; the budget was not raised and combined runtime repair proof passes 9/9 |
| Safe DTO shape alone could still transport unsafe event message content | The normalizer emits only resolved public node text or bounded event type; the unresolved native identifier stays in internal payload metadata; direct 4/4 and restart E2E 12/12 pass |
| UI and process-host broad failures could be mistaken for product regressions | The shared bUnit harness composes the production UI boundary and authorization context; process-tree tests serialize and wait for an explicit readiness signal. Components pass 1,078/1,078 and the process-host selector passes 2/2 |
| Later findings invalidated frozen prerequisite claims | Append-only SB03/SB04/SB05 supplements preserve narrow reproof for event redaction and PostgreSQL precision/lease fencing; frozen parent ledgers/TRXs are not rewritten |

The final CodeAnalytics snapshot covers 9 projects and 499 documents with no blocking
error or project cycle and only the two unchanged baseline non-project cycles. Subsequent
narrow plugin/runtime/test repairs add no project reference. Independent strict review
approved the typed seam and construction-time fence. The accepted frozen FG-01 builds both
solution graphs at 0W/0E and passes 8,471/8,471 tests with zero failed or skipped.

## Existing debt disposition

- Exception-as-pause and metadata-only checkpoints are legacy compatibility only and may not serve new native runs.
- The 3,133-line persistent-store cluster must not receive the new durable responsibilities.
- The runtime manager remains a compatibility/start-cancel facade and delegates response-operation lifecycle ownership to focused collaborators.
- Two pre-existing CodeAnalytics module/type cycles are outside scope and must not increase.

## Implementation entry decision

SB03 entered through CP-WB0. Final snapshot `snap-20260821002934-bf844210`, governed
proof under `proof/SB03`, the exact 203/203 selector, and the independent strict review
establish CP-WB1 Pass. Final snapshot `snap-20260821044013-44e660f5`, governed proof
under `proof/SB04`, the 419/419 Unit and 16/16 Integration selectors, ten affected Release
builds plus the final post-fix Unit build, migration/no-pending-model proof, and strict
re-review establish CP-WB2 Pass. Final snapshot `snap-20260821072204-bf844210`,
governed proof under `proof/SB05`, the 297/297 Unit and 137/137 Integration selectors,
all affected Release builds at 0W/0E, no-bypass/no-migration/source/API validation, real
production-path proof, and strict re-review establish CP-WB3 Pass. SB06 then completed the
declared reopen/reproof loop and passed CP-WB4.

CP-WB3 implements one common facade, the authorizer Strategy, one validator, a
reconstructable authorization grant, and focused Web adapters/mappers. It introduces no
new project or relational migration, makes no fabricated per-user project ACL claim, and
does not manufacture a background compatibility actor.

The in-memory proof implementations are process-local, non-durable, and
non-snapshot-isolated. Production correctness relies on authoritative PostgreSQL CAS,
constraints, and transactions. The guarantee is exactly-once response acceptance and
deduplicated participating governed effects, not arbitrary external exactly-once behavior.

## Closure review requirements

Before each architecture subbundle closes, rerun the strict architecture review against actual code and proof. Closure is blocked by a new partial/nested extraction, old owner retaining decisions, inward project references, tests that cannot instantiate extracted behavior, missing pattern proof, production DI bypass, or an extension that still requires editing the old monolith.

## Reviewer decision log

| Checkpoint | Decision | Evidence |
|---|---|---|
| CP-WB0 | Prepared / SB03 entry-approved | architecture artifacts, source inventory, dependency map, pattern records, testability plan, snapshots `snap-20260820203442-90bdd166` and `snap-20260820220112-5cb38069` |
| CP-WB1 | Pass / SB03 Proven | `proof/SB03`; exact 203/203 selector; seven Release builds including unit; Debug unit build; source/anti-stub and production-composition proof; snapshot `snap-20260821002934-bf844210` |
| CP-WB2 | Pass / SB04 Proven | `proof/SB04`; Unit 419/419; Integration 16/16; ten affected Release builds plus final post-fix Unit build at 0W/0E; migration `20260821021747_AddWorkflowHitlRecovery` with no pending model changes; scanner/docs/bundle/diff gates; snapshot `snap-20260821044013-44e660f5`; independent strict re-review |
| CP-WB3 | Pass / SB05 Proven | `proof/SB05`; Unit 297/297; Integration 137/137; all affected Release builds at 0W/0E; no SB05 migration/model-snapshot change; exact three-caller/no-bypass and safe-projection gates; real Web/service/PostgreSQL/MAF positive and adversarial proof; snapshot `snap-20260821072204-bf844210`; independent strict re-review |
| CP-WB4 | Pass / SB06 and parent Proven | 17-row E2E matrix; restart E2E 12/12; focused Unit 7/7 and Integration 14/14; append-only SB03/SB04/SB05 reproof; snapshot `snap-20260821092959-44e660f5`; strict typed-seam review; Components 1,078/1,078; valid frozen FG-01 builds 0W/0E and tests 8,471/8,471; final documentation, traceability, and input audit |
