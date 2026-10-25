# Concurrent mutations: C# testability and validation plan

Status: proposed proof, not executed. Reviewed 2026-10-16 against
`ead988abeb4cfb3e12341e618362bfa3dde5df29`. The preserved architect catalogue supplies
requirement IDs; those IDs are not discovered .NET tests. Runtime pass counts must be
recorded only after actual discovery/execution against the final binaries.

## Proof boundaries and seams

| Responsibility | Independent proof seam | Real composition proof |
|---|---|---|
| Content equality, original condition and child identity validation | Small owner policy with typed inputs and immutable snapshots; no Web host or service locator | Projects owner over isolated PostgreSQL |
| Draft acknowledgement and conflict state | Existing `ProjectEditorDraft` with real renderer; frozen submission, field revisions, original row objects | Projects page with actual owner and held native effects |
| Operation fingerprint/dedupe and outcomes | Canonical typed request identity and explicit result states | Receipt/aggregate transaction, reconnect/restart and unauthorized lookup |
| Agent input/publication policy | Provider-neutral pure policy, bounded replan state and trusted required facts | Actual registered tool and process/workflow admission to owner |
| Transaction/release behavior | Narrow supported test interceptor/adapter around commit/cleanup | Independent contexts and two OS processes using one leased database |
| HTTP contract | Raw JSON and generated schema assertions, typed error mapping | Actual HTTP host, owner reload and conflicting second client |
| Renderer dependencies | Existing compiled graph and public-signature guards | Backend-free sandbox hosting actual production renderer |

Do not make a "testable" interface implemented only by an unchanged broad manager. Put
policy and protocol mapping in cohesive types that can be exercised independently, while
leaving persistence and transaction ownership with the module. No globally enabled
production race switches. Use scoped interceptors, existing adapters or a narrowly
bounded test hook at the real commit boundary.

## Baseline evidence to preserve

The Projects source/contracts snapshot `snap-20261016142513-0f60f349` contains three scoped
projects and 205 documents. It excludes the renderer and sandbox. The infrastructure
snapshot `snap-20261016142537-4d8bda80` contains four scoped projects and 224 documents.
Snapshots carry diagnostics, including partial factory-DI interpretation and restore
warnings; they are navigation evidence, not a successful build or complete evaluated UI
dependency closure. Use actual project references and `ProjectsBoundaryTests` for the
UI boundary, and refresh any affected evaluated graph before closure.

Relevant existing suites and exact source seeds:

- `tests/Components/CanDoItAll.Projects.UI.Tests/ProjectEditorSurfaceTests.cs`: submission
  capture before held browser validation, raw date/context survival, both Save actions,
  row ID reconciliation after reorder, typing back to the submitted value and foreign
  lifetime acknowledgement rejection.
- `tests/Components/CanDoItAll.Projects.UI.Tests/ProjectsBoundaryTests.cs`: compiled
  renderer/sandbox transitive closure, forbidden-edge controls, public signatures and DI.
- `tests/Components/CanDoItAll.Tests.Components/ProjectsEditorMutationTests.cs`: held
  committed save, pending/unknown seed, failed refresh, native IDs, retired saves and
  same-ID recreation. Preserve actual PostgreSQL effects behind its timing adapters.
- `tests/Components/CanDoItAll.Tests.Components/ProjectsPageTests.cs`: surrounding host
  portfolio, Files, package and route behavior.
- `tests/Integration/CanDoItAll.Tests.Integration/ProjectsServiceIntegrationTests.cs` and
  `ProjectsOwnerPersistenceTests.cs`: owner persistence, UTC normalization, reservation,
  hierarchy and postcommit projection failure controls.
- `tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureStaleEditHttpTests.cs`:
  existing Workbench expected-state/graph phantom behavior. Extend selectively; do not
  weaken these in order to route everything through one global revision.
- `tests/Integration/CanDoItAll.Tests.Integration/ProjectAdmissionSnapshotRaceTests.cs`:
  snapshot established while waiting for retirement/recreation, including batch reads.
- `tests/Integration/CanDoItAll.Tests.Integration/ProjectCrossModuleMutationScopeReleaseIntegrationTests.cs`:
  real CRM followup after structure scopes are released.
- `tests/Playwright/CanDoItAll.Tests.Playwright/ProjectsPortfolioBrowserTests.cs` and
  `ProjectsPortfolioUiJourney.cs`: real five-step editor, native identity and route/focus
  behavior at the supported desktop viewport.

## Deterministic Projects owner and protocol cases

The following are proposed cases, not a forecast of discovery count; data-driven rows
and final class grouping are chosen when implementation makes the surface concrete.

| Case | Arrangement and required result | Architect IDs |
|---|---|---|
| Original stale edit | Two independent reads/contexts; first saves; second's scalar/list replacement is rejected; first state remains exact | T004, T019 |
| Fresh positive control | Current revision and valid admission save successfully and return the accepted revision | T045 |
| Missing wire revision | Raw JSON missing/null/empty/malformed revision refuses before any root or child mutation | T020, T021 |
| Root-child atomicity | Child-only edit advances definition revision; stale root replacement cannot erase it; injected failure rolls back both | T005, T007, T029 |
| Coherent read | Barrier between root and child read overlaps another committed save; returned model belongs to one revision | T006 |
| Child identities | Duplicate ID, foreign ID and unknown nonempty ID each reject; new rows get stable accepted IDs | T035, T050 |
| Two hosts | Two independent processes read the same revision and race; at most one conflicting proposal commits regardless of local semaphores | T013 |
| Disjoint positive control | Different projects both make progress; a hot project cannot require a global write lock | T013, T088 |
| Source/lifetime race | Hold final admission while another connection retires/recreates; original admission refuses even after a wait | T012, T093 |
| Request dedupe | Same operation/same canonical command returns exact original result; changed command with same ID refuses | T024, T025, T026 |
| Late replay | Retry after unrelated newer save returns original accepted result, not latest read-back | T025, T092 |
| Lost acknowledgement | Commit then interrupt response; lookup resolves original operation without a second write | T008, T024 |
| Receipt access | Other principal/profile or revoked read permission cannot obtain sensitive accepted content | T028, T096 |
| Transaction fault | Fail after row changes/before commit; neither success receipt nor partial children remain | T029 |
| Cleanup fault | Commit acknowledged internally, disposal/finalization fails; preserve known outcome and release every owned gate | T010, T011, T017 |
| Projection order | Hold old projection, commit new save, release old work; canonical current content remains indexed | T036, T037 |
| Projection lifetime | Delayed old projection cannot revive a deleted/recreated project | T038 |
| Restart/import | Ordinary restart preserves lookup; import generates new live tokens and cannot activate source approvals/receipts | T039, T041 |
| No-op | Declared normalization/no-op semantics still validate original basis/authority; equal values do not bypass stale approval | T051, T094 |

Use `TaskCompletionSource` with asynchronous continuations and bounded awaits at observed
protocol points. Await every operation and surface background exceptions. `Thread.Sleep`
or probabilistic stress cannot be the only proof of an ordering assertion. Keep the SQL
interleaving proof separate from throughput runs.

## Editor, renderer and browser cases

| Case | Required observation | Architect IDs |
|---|---|---|
| Conflict preserves editor | Same draft and `EditContext`; invalid raw date/text and selection remain; latest data presented separately | T030 |
| Typing during save | Submitted values commit; later typing remains unsaved; untouched fields adopt normalized accepted values and revision | T031 |
| Edit back to value | Field revision records post-dispatch edits even when value equals submission again | T031 |
| Reorder/remove/add pending | IDs map to original submission rows; removed row stays removed; new row receives no foreign ID | T035 |
| Save and Enter | Exactly one owner command; duplicate callbacks cannot create a second operation | T033 |
| A to B to A | Late read/success/refusal/error cannot mutate A2 or navigate/reopen dismissed editor | T032 |
| Known commit/read failure | Visible saved warning, identity retained, read retry issues zero repeated writes | T034 |
| Current read newer than receipt | Exact accepted revision and subsequent change shown distinctly; newer snapshot not passed off as original commit | T092 |
| Conflict read unavailable | Draft stays usable; no token advancement, no false "ready" acknowledgement | T030 |
| Third writer | Deliberate resolution with reviewed revision conflicts again if owner changed | T047 |
| Independent panels | Portfolio/agent completion or hierarchy refresh does not replace acquired dirty draft | T030, T091 |
| Dispose/repeated opens | Cancel owned reads and detach effects/subscriptions; no retained mutable sessions | T032, T044 |
| Two browsers | Human A succeeds, human B sees conflict and retains text; deliberate recovery uses new conditional command; DB oracle agrees | T043 |
| Actual agent vs human | Hold real tool publication, human saves relevant content, agent gets structured refusal and zero effects; valid fresh control succeeds | T064 |
| Backend-free scenarios | Real production renderer covers conflict, stale current read, unknown result and recovery with no module persistence/MAF/Web dependency | T044 |

Use bUnit `ClickAsync` and query/dispatch together under `cut.InvokeAsync` when renders
can race. Wait for rendered conflict controls as well as state. Use the repository's
`DisposeRenderedComponentsAsync` helper for reused contexts. Never block a renderer
dispatcher by awaiting an owner operation inside `WaitForAssertion`.

Use `PlaywrightAppFixture`/owned host infrastructure with two explicitly owned independent
browser contexts at 1920 by 1080. `LiveUiHost.NewPageAsync` stores one current context;
do not assume calling it twice establishes explicit ownership of both contexts. Wait for
`projects-workspace[data-interactive=true]`, capture relevant conflict screenshots and
native persisted values, and retain all cleanup failures. A scripted provider may drive
the actual registered agent tool without paid inference; that is tool-contract proof,
not a claim about a live model's reasoning.

## Wider adoption invariants

Each owner wave must retain its specific semantics rather than copy a universal parent
token: Workbench graph membership and expected task state; CRM personal/rate privacy,
assignment revisions and capacity predicates; prompt mutable draft versus immutable
version; chat definition versus append ordering; collaboration status versus reply
dedupe; resources metadata versus bytes; security revocation versus editable settings;
workflow/process definition pinning versus current publication authority. Keep each wave's
full caller inventory and positive controls with its owner.

Required runtime cases include material input changed during inference, irrelevant input
unchanged, omitted mandatory basis, membership/absence phantom, stale approved proposal
with a substituted token, ignored cancellation from retired attempt, claim transfer,
bounded replans/deadline escalation and resumed legacy checkpoint requiring explicit
readmission. Snapshot planning never waives target CAS or current authority.

## Fixture and execution procedure

Read current `docs/testing.md`, `.github/workflows/ci.yml` and `ui-component-seams.md`
again if those files change during implementation. Use PostgreSQL 18 configured privately
through `CANDOITALL_TESTS_POSTGRES_CONNECTION`. `PostgresTestAvailability` refuses missing,
unreachable or wrong-major endpoints. `CanDoItAllTestEnvironment` and
`PostgresTestDatabaseLease` own named databases, clear only their pools, and use separate
60-second maintenance/15-second ordinary SQL budgets. Reuse the isolated server across
lanes; never use the ordinary app/database on 5032 or start development Compose as fallback.

Before provisioning, record ownership of temporary ports/processes/containers/volumes and
resolved directories. For two-process proof, both processes use the same explicitly owned
lease; each has a separate service provider, context and physical connection. Release
hosts before disposing the lease. Verify owned resources are removed after the last lane.
Do not delete the entire `.artifacts` tree or globally prune Docker.

For each implementation slice:

1. Build each changed production project directly in standard Release (or the deliberately
   selected standard Debug) and serialize processes that write the same `bin`/`obj`.
2. Identify actual changed ranges with CodeAnalytics impacted-tests using every relevant
   test workspace, or document scoped symbol/reference analysis when unavailable.
3. Write the exact filter and expected count from actual case declarations, including
   data rows. Run `--list-tests` against refreshed binaries and compare actual count.
4. Run that exact filter. Zero/unexpected discovery, skipped required cases or unavailable
   fixtures invalidate proof. No claim of success based only on static selection.
5. Run portability-static on the complete final proposed protected tree, include untracked
   new files, review added/stale findings, repair genuine defects, refresh intentional
   deltas and enforce again without `--write-baseline`.
6. Update relevant rendering register entries and run its check after final UI edits.

Owning entry points are the Unit, Components, Integration and Playwright solutions under
`tests/Solutions`, plus lightweight `CanDoItAll.Projects.UI.Tests`. The product solution
does not contain tests. Preserve existing filtered Projects native tests alongside the
new focused cases. Shared persistence/migration/public-contract/composition changes are
named triggers for one frozen broad Stable checkpoint; do not rerun the full suite merely
because a phase number changed. Browser, live-process, Docker and quarantined lanes remain
separate and are reported honestly.

## Measurement plan

The package's performance thresholds are proposals, not existing service levels. Measure
matched baseline/candidate paths: normal 5-phase/7-option editor; bounded large collections;
disjoint writers at 1/8/32/64; deliberately colliding local stripes; hot target and two
processes; basis cardinalities 1/8/32/128 within the chosen bound; delayed/failing projection;
receipt growth; repeated editor lifetime and refresh bursts.

Record source/sibling revisions, SDK/runtime/OS/hardware, PostgreSQL version/topology,
warmup/sample counts, SQL commands/rows, payload bytes, allocations/CPU, p50/p95/p99,
successful throughput, rejection rate, pool/lock waits and lock hold/transaction time.
For agent policy record useful completions, basis age, invalidating category, semantic
replans and discarded model work. Separate correct rejection from useful throughput.

Hard gates are no external IO/human/model waits under locks, no N+1 input validation,
bounded complete basis validation, no global portfolio scan for a narrow edit, linear
identity matching, no silent lost required projection and no infinite retries. Investigate
more than +15% save p95 or -10% disjoint throughput with absolute noise/variance context;
record any accepted cost of stronger guarantees explicitly. Do not remove correctness
checks to meet a synthetic relative threshold.

## Evidence record and remaining implementation decisions

For every executed lane record command/filter, expected/actual discovery, pass/fail/skip,
source identities, sanitized endpoint/version, receipt/projection outcomes, retained proof
paths and cleanup result. Keep planned, unavailable and failed proof distinct from passed.

The chosen first conflict flow is explicit inspect/reload/manual reconciliation, with no
force or automatic merge. Typed body edit stamps are the initial public mechanism.
Resolve rollout/cutoff, durable operation/retention identity, delete policy and
collection/basis limits before dependent code. The
separate SPA's current generated serializers drop lifetime/revision data; its mutation
compatibility must be coordinated separately or refused by the protected server. No
passing engine test can be reported as a passing unmodified SPA journey.
