# Execution Report

**State:** Proven

**Repository:** `C:\repositories\CanDoItAll`

**Branch/HEAD:** `maf-update-and-hil` / `af425ac371b251447f9858b15476092531c686da`
**Executor:** Codex GPT-5.6 xhigh
**Started:** 2026-08-20
**Completed:** 2026-08-21T13:59:43.2785414Z

## Wave A result

Passed. SB01 is proven: stable MAF is `1.18.0`, A2A/Hosting preview is
`1.18.0-preview.260818.1`, the resolved product and test graphs are coherent, affected
projects build, and 73/73 compile-sensitive MAF/hosting/A2A tests pass. No source-level
breaking adaptation was required. SB02 explicitly disables concurrent invocation, proves
serial A/B/C execution in streaming and non-streaming modes with a meaningful concurrent
negative probe, and passes 84/84 approval/session/usage/streaming/runtime tests. Wave A
contains no workflow HITL implementation.

## Wave B result

Passed. SB03–SB06 are Proven and CP-WB1–CP-WB4 are Pass. Native HumanInput and approval
boundaries use public MAF request ports, and exact-version continuation uses focused
PostgreSQL checkpoint/session, request-boundary, response-operation, and
executor-invocation stores. Operation CAS, lease/heartbeat recovery, cancellation/failure
races, four crash windows, consecutive waits, legacy fail-closed behavior, and
participating governed-effect deduplication are proven.

SB05 routes exactly three production mutation callers through one authorized
`IWorkflowExternalResponseService`. Typed JSON, trusted actor/profile/scope, validation
before mutation, scope-bound idempotency, durable authorization reconstruction, safe
status/read projections, OpenAPI, and redacted audit are proven. A real authenticated Web
request completes through the common service, PostgreSQL, and MAF; identical replay returns
the same operation without new events. Real adversarial proof denies insufficient scope
before operation creation, conflicts changed payload without duplicate state/events, and
rejects a late response after real cancellation without creating an operation. The precise
guarantee remains exactly-once response acceptance and deduplicated participating governed
effects, never arbitrary external exactly-once execution. Governed proof is under
`proof/SB03`, `proof/SB04`, and `proof/SB05`.

The in-memory stores used by direct proof and compatibility construction are proof-only,
process-local, non-durable, and non-snapshot-isolated. They do not establish host-restart
or multi-host correctness. PostgreSQL conditional writes, constraints, and transactions
are authoritative for production CAS, checkpoint ordinal allocation, response-operation
claims, atomic resume-boundary commits, and executor-invocation deduplication. The
in-process backend still advertises `IsDurable = false` because persisted checkpoints do
not create a durable orchestration host.

Final SB06 CodeAnalytics snapshot `snap-20260821092959-44e660f5` has zero project cycles,
unchanged project references, and exactly the two baseline non-project cycles retained by
SB04 and SB05. The common service and focused mapper/recovery collaborators preserve the
neutral Runtime boundary; Web owns HTTP binding and safe projections,
Modules.AgentFramework owns trusted current-profile authorization and persistence
composition, and MafAdapter owns native continuation.

All 17 required E2E rows are Proven. The reconstructed-host cases use real Web/JWT,
service, PostgreSQL, protected checkpoint, and MAF boundaries with deterministic
participating executors. Direct coordinator recovery is proven on a reconstructed host;
automatic hosted-worker startup remains separate focused composition/worker evidence and
is not overstated by the E2E fixture.

## Validation summary

Executed through the final frozen SB06 state. The accepted FG-01 checkpoint ran once after
focused/E2E proof and every diagnostic repair passed.

| Gate | Command/filter | Expected discovery | Actual | Result | Evidence |
|---|---|---:|---:|---|---|
| SB00 baseline | bundle validation; solution restore; three-class unit filter; `WorkflowApiIntegrationTests` | 12 + 4 + 13 unit; 16 integration | 29 unit; 16 integration | Pass | SB00 closure record; CodeAnalytics `snap-20260820203442-90bdd166` |
| SB01 affected build | upgraded scanner; three product builds; unit build; seven-class focused filter | 52 + 4 + 3 + 3 + 2 + 5 + 4 | 73 | Pass | SB01 closure record; resolved `project.assets.json` inspection |
| SB02 behavior | upgraded scanner; seven-class focused filter; MAF Release build; docs validator | 52 + 12 + 3 + 2 + 9 + 2 + 4 | 84 | Pass | SB02 closure record; failing-first factory assertion |
| SB03 native HITL | Debug unit build; exact thirteen-class selector; six affected Release production builds plus Release unit build; scanner/docs/validator/source/diff gates | 203 | 203 | Pass | `proof/SB03`; snapshot `snap-20260821002934-bf844210`; CP-WB1 Pass |
| SB04 persistence/recovery | immutable 26-clause Unit selector; exact three-class Integration selector; ten affected Release builds; final post-fix Unit build; migration pending-model check; scanner/docs/bundle/diff/source/architecture gates | 419 Unit; 16 Integration | 419 Unit; 16 Integration | Pass | `proof/SB04`; migration `20260821021747_AddWorkflowHitlRecovery`; snapshot `snap-20260821044013-44e660f5`; CP-WB2 Pass |
| SB05 API | exact 22-class Unit selector; exact 11-class Integration selector; all affected Release builds; source/schema/API/architecture gates | 297 Unit; 137 Integration | 297 Unit; 137 Integration; 0 skipped | Pass | `proof/SB05`; snapshot `snap-20260821072204-bf844210`; CP-WB3 Pass |
| SB06 restart and closure | 17-scenario E2E map; retained Unit/Integration selectors; normalizer, PostgreSQL precision/lease, Components, plugin, API projection, runtime, and process-host focused repairs | E2E 12; Unit 7; Integration 14; targeted counts as recorded | E2E 12/12; Unit 7/7; Integration 14/14; every targeted repair green | Pass | `proof/SB06/TestResults`; append-only SB03/SB04/SB05 reopen supplements; snapshot `snap-20260821092959-44e660f5` |
| FG-01 broad | Five authoritative commands from `docs/testing.md`: product restore/build, Stable restore/build, exact filtered Stable test | Components 1,078; Integration 923; AgentFramework.Memory 22; Memory 196; Unit 6,252 | 8,471/8,471; 0 failed/skipped; both builds 0W/0E | Pass | Valid freeze `2026-08-21T12:52:49.8229732Z`–`2026-08-21T13:59:43.2785414Z`; HEAD and sibling pins recorded below |

## Deviations

- The execution branch differs from the prepared target branch, but its only post-baseline
  commit adds the bundle; product source and tests are identical to the preparation baseline.
- The first Release test-project build was blocked by an existing web host locking Release
  binaries. No process was stopped; focused test execution and restore passed, and later
  validation will use isolated output/configuration where necessary.
- `dotnet restore CanDoItAll.slnx` intentionally did not refresh test assets because the
  product solution contains no tests. The first unit build exposed the stale 1.17 graph;
  an explicit unit-project restore produced a coherent 1.18 graph and a clean rebuild.
- The upgrade scanner repeatedly traversed ignored build trees and reread every source file
  for each rule. Its inventory now uses Git's tracked-plus-untracked index with a pruned
  filesystem fallback and evaluates source rules in one pass; the prescribed gate passes.
- `CanDoItAll.slnx` contains no test projects. FG-01 must combine the product solution build
  with the documented `tests/Solutions/CanDoItAll.Tests.Stable.slnx` build/test gate instead
  of relying on `dotnet test CanDoItAll.slnx`.
- Exact restored 1.18 inspection showed `RequestInfoExecutor` is internal and wrapped request
  mode does not preserve original business input. SB03 uses public request-port bindings and
  a server-owned continuation created from the restored checkpoint request; this is the
  fail-closed resolution of IK-07, not an API-response argument fallback.
- MAF allocates neither checkpoint ID nor commit ordinal at the checkpoint-store call. The
  application checkpoint port now owns their atomic allocation and explicit oldest-to-newest
  index ordering.
- Exact verifier source validates the complete persisted version/topology/session/request/port
  identity. Dedicated mutation negatives cover session, request, port, topology, missing, and
  corrupt state; SB03 does not claim one dedicated mutation test for every validated field.
- The first sandboxed SB03 Release unit build could not write six sibling Components
  generated-asset caches. The authorized identical rerun passed with zero warnings/errors;
  the environmental failure and passing rerun are both retained in `proof/SB03`.
- The SB04 Integration project retained stale 1.17 test assets after the product-only
  solution restore. An explicit Integration-project restore refreshed the graph to stable
  1.18 and preview `1.18.0-preview.260818.1`; no source package version was changed.
- The first sandboxed SB04 Module build could not write sibling Components generated-asset
  caches. The authorized identical rerun passed. Ten affected Release project builds and
  the final post-fix Release Unit build completed with zero warnings and zero errors; both
  the environmental failure and passing rerun are retained in `proof/SB04`.
- The production-composition test uses the application bootstrap with an explicit in-memory
  test profile and an inert DbContext factory to prove registration shape without querying
  a database. It is not durability evidence; the other 15 Integration facts use the real
  PostgreSQL test database and prove persistence/CAS/transaction behavior.
- The first frozen SB05 Unit selector is retained at 296/297: a test-only 1 KiB payload cap
  caused the over-bound approval-message fact to observe `PayloadTooLarge` before its
  intended schema branch. Raising only that fixture cap preserved production behavior; the
  identical selector then passed 297/297.
- The first SB05 Integration selector is retained at 75/137 because the sandbox denied the
  user-local database-profile generation lock. The permission-enabled retry reached 136/137
  and exposed one stale assertion expecting `MetadataOnly`; production correctly returned
  resumable `TrustedRuntimeState`. Correcting only that expectation produced the distinct
  final 137/137 artifact. None of the red artifacts is relabeled green.
- SB06 PostgreSQL diagnostics reopened two narrow SB04 executor-deduplication clauses. The
  completion-precision case moved from 0/1 to 1/1 after one UTC-microsecond canonicalization
  point; the exact-expiry lease class moved from 4/6 to 6/6 after every mutation adopted the
  same strict expiry fence. The append-only SB04 supplement cites both red and green TRXs;
  the frozen SB04 parent ledgers remain unchanged.
- An SB06 redaction E2E exposed an unresolved native backend-port identifier in otherwise
  allow-listed public event text. The normalizer now emits only a resolved public node or
  bounded event type while retaining the native identifier in internal payload metadata.
  The direct selector passed 4/4 and the restart E2E passed 12/12. Append-only SB03 and SB05
  supplements preserve the reopened current-source claims without rewriting frozen parent
  proof.
- The first permission-enabled Stable attempt exposed that the shared Components harness
  did not install authorization state or the production AgentFramework UI registration.
  The fixture-only composition repair passed the affected 40/40 selector and then the full
  Components assembly at 1,078/1,078.
- The next complete exact Stable diagnostic returned 8,463/8,470: Components 1,078/1,078;
  Integration 921/923; AgentFramework.Memory 22/22; Memory 196/196; Unit 6,246/6,251.
  Because the seven failures required edits, this was a diagnostic pre-freeze invocation,
  not the accepted FG-01 checkpoint. It exposed a non-public implicit plugin export, a stale
  safe-API hash assertion, in-memory response-redaction/composition and file-budget defects,
  and process-host child-readiness races.
- The plugin repair limits implicit assembly discovery to visible concrete closed executor
  types while preserving explicit type registration. The API contract now asserts absence
  of `idempotencyKeyHash`. The in-memory native-resume path uses a typed internal redacted
  acceptance capability, persists blank source `ResponseJson`, fails during construction
  when native compatibility lacks the capability, and preserves legacy no-checkpoint
  compatibility. Cancellation transition logic was extracted cohesively rather than
  weakening the architecture line budget. Process-host tests serialize their process-tree
  fixture, allow the documented readiness window, and fail explicitly if the child never
  publishes its PID. Targeted proof passed 1/1 plugin, 1/1 API, 14/14 runtime lifecycle,
  9/9 runtime repair, and 2/2 process-host.
- A sandbox-only denial during the final restore was retried with the identical command
  under the required permission and passed. This was an environmental access result and did
  not mutate source or count as a product-gate failure.
- The final valid frozen checkpoint remained at HEAD
  `af425ac371b251447f9858b15476092531c686da`, Components
  `8372c1d55f21b349f8e859470b02eeb4421e96ca`, and FileTools
  `c95dd07208a6d48724443317cdc6cfe67a13020a`. Product build passed 0W/0E in
  51.15s, Stable build passed 0W/0E in 70.13s, and the exact filtered test passed
  8,471/8,471 with zero failed or skipped tests.

## Remaining risks

No required implementation or validation work remains. The declared product boundaries
remain intentional rather than hidden residual risk: the in-process backend is non-durable;
in-memory stores are process-local, non-durable, and non-snapshot-isolated; direct
reconstructed-host coordinator recovery is distinct from hosted-worker startup proof; and
the supported guarantee is exactly-once response acceptance plus deduplicated participating
governed effects, not arbitrary external exactly-once execution.

## Original input closure

| Original request note | Closure | Implementation and proof |
|---|---|---|
| Review MAF 1.18 against the repository's prior 1.17 usage | Solved | `evidence/MAF-1.18-DELTA.md`, resolved assets, upgraded scanner, affected builds, and FG-01 establish the actual package/API delta. |
| Update to MAF 1.18 and repair breaking changes | Solved | Stable `1.18.0` and preview `1.18.0-preview.260818.1` are the only active MAF versions; package consumers and both authoritative solution graphs build at 0W/0E. |
| Treat parallel tool calls cautiously because order matters | Solved | Application-owned invocation remains explicitly serial; streaming/non-streaming A/B/C order and a meaningful opt-in overlap negative are proven. No public concurrency toggle or declaration-only storage experiment was added. |
| Complete workflow Human-in-the-Loop after the small update | Solved | Native MAF request ports, protected checkpoints, exact-version/topology rehydration, consecutive waits, approval/denial, recovery, cancellation, legacy/corruption fail-closed behavior, and participating-effect deduplication are implemented and proven. |
| Complete the HITL API, not only the runtime | Solved | The existing response route now uses typed bounded JSON, trusted actor/scope authorization, idempotency/conflict handling, audit/redaction, safe status/read projections, OpenAPI, and one shared service used by all three production callers. |
| Provide a detailed Codex 5.6 xhigh bundle/archive | Solved | The user identified this delivered repository bundle as the execution input. Its seven-subbundle shape preserves requirements, dependencies, architecture, proof tiers, traceability, governed artifacts, reopen history, execution report, and final closure; the current in-place execution supersedes archive transport. |

RQ-001 through RQ-045 are Proven. No raw input is Partially solved or Not solved.
