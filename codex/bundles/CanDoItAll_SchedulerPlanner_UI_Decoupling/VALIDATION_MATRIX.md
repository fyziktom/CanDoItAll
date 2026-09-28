# Validation matrix — Plugins S0 and SchedulerPlanner

Implementer must turn these obligations into current discovered/executed cases and record
exact commands/results. Rows are **requirements, not tests already run by this reviewer**.
Use current repository instructions and the [execution prompt](prompt.md); source rationale
is in [Plugins review](PLUGINS_REVIEW.md) and [Scheduler review](SCHEDULER_REVIEW_NOTES.md).

## Entry and proof ledger

Record start/end main SHA, branch/dirty state, sibling revisions/source mode, SDK/runtime,
configuration and test resources. Locate actual owning test projects from current references
and Code Analytics/local search. Build/discover every changed assembly before execution.
For theories distinguish discovered method/data entries from executed expanded cases.
A zero match, incomplete TRX, cancellation or skipped required case is not a pass.

Use a ledger with requirement ID, source baseline, test name, owner project, expected and
actual discovery, execution counts, artifact path, status and limitation. Capture failing-first
proof for findings, then green proof on the corrected implementation. Tests may cover several
rows; do not invent one class per row or add timing-sensitive duplication.

## S0 — Plugins fixes

| ID | Required case | Strong assertion / minimum proof |
| --- | --- | --- |
| V-PL-01 | A/B catalog, selected A, successful refresh B only | Real top-level UI still lists/selects B; missing A detail explained; no page reload or synthetic mutation |
| V-PL-02 | Missing selection with dirty/pending/unknown A | A origin and raw draft/receipt retained; B selection cannot adopt A's identity or enable blind replay |
| V-PL-03 | Empty catalog then repopulate; stale catalog response | Recovery remains usable; earlier success/failure/finally cannot replace current explicit selection |
| V-PL-04 | Replacement starts, primary filesystem failure, cleanup also fails | Actual stage and package/plugin identity survive; primary failure is not erased; not clean Refused or invented Installed |
| V-PL-05 | PL-R2 result through real owner adapter and workspace | Partial/unknown receipt locks replay; read-only refresh does not extract/install again |
| V-PL-06 | Invalid pre-effect upload and known later-stage failures | Existing invalid/oversize/read, Extracted, Installed, RestartRecorded outcomes preserved; task-owned cleanup remains attempted |

Suggested existing owning locations: `tests/Components/CanDoItAll.Plugins.UI.Tests`,
`tests/Components/CanDoItAll.Tests.Components`,
`tests/Integration/CanDoItAll.Tests.Integration` and the actual Playwright project. Inspect
current `PluginsWorkspaceTests`, `PluginsEffectTests`, `PluginsBoundaryTests`,
`PluginsUiOwnerReceiptTests` and existing Plugins browser tests before selecting exact filters.
At least V-PL-01/02 need the actual renderer, and V-PL-04/05 cannot close with only a fake
receipt. Use a browser for the missing-selection recovery and keep production compile/consumer
proof for the owner correction. No blanket re-run of unchanged TestLab extraction is required.

## Scheduler rendered scope and pure behavior

| ID | Behavior | Expected proof |
| --- | --- | --- |
| V-SC-01 | Complete route, four tabs and overlays | Production uses extracted renderer; sandbox exposes Calendar/Schedules/New/History plus picker/edit/delete, not a subset |
| V-SC-02 | Empty, loading, failure, stale and missing states | Failures are not fabricated empty success; remaining independent information stays useful |
| V-SC-03 | Filters, tags, paging and history status/route/result | Existing controls remain functional; no owner calls merely from render; query bounds persist |
| V-SC-04 | Target kind/ID/version, same name and missing version | Exact identity preserved; no silent newest-version fallback or first-option reassignment |
| V-SC-05 | New draft capture before validation/authority await | Submitted values fixed; newer raw typing cannot change actual owner command or authority target |
| V-SC-06 | Existing draft capture and in-flight edits | Save uses original full payload; name/description/CRON/zone/JSON/enabled later edits survive |
| V-SC-07 | Committed create before held/read-failed refresh | Real saved ID adopted before refresh; subsequent explicit save updates that ID; no duplicate create |
| V-SC-08 | Known committed update plus readback failure | Existing ID and latest user edits retained; warning differs from refusal/unknown |
| V-SC-09 | True unknown versus refused | Unknown blocks blind replay; exact operator recovery is explicit; Refused never claims saved state |
| V-SC-10 | Direct duplicate saves and same-plan conflicts | Handler-side admission; save/pause/resume/delete have explicit consistent target conflict policy |
| V-SC-11 | Independent plans | A pending operation does not needlessly block unrelated safe B operations or share its status |
| V-SC-12 | Dialog dismissed/reopened during validation/authority/save/readback | No null dereference, successor close/reset, stale notification or forced tab; old admitted write observed |
| V-SC-13 | New/reset/default-load race and selection A/B/A | Late default/schema/editor response never replaces a newer draft; no abandoned loading state |
| V-SC-14 | Invalid JSON/non-object raw payload | Raw text and error remain; no conversion into empty/default object or accidental owner write |
| V-SC-15 | Typed/raw JSON normalization | Unknown properties and supported types preserved; defaults/optional values and current version stay consistent |
| V-SC-16 | New input during validation / options load | Old normalization/options cannot patch the new revision; partial numbers and unblurred text remain editable |
| V-SC-17 | Dependent option changes | Only relevant dependent values invalidated; missing/unavailable/empty distinguished; stale results fenced |
| V-SC-18 | Option request budgets | Plain typing and repeated blur do not reload every independent source; cache/coalescing bounded and context-specific |
| V-SC-19 | Disposal of every async lane | Retire callbacks and subscriptions; CTS lifetime until unwind; no successor state mutation or unobserved task |
| V-SC-20 | Hidden persisted fields | Start/end bounds and existing nonvisible settings survive update; default form does not erase them |

Use controlled completion sources/test clocks instead of arbitrary sleeps. Keep raw input
checks through actual `input` events, not only helper methods. Pure/service tests and real
component events are complementary. Where a broad existing page test is slow because its
fixture hosts the application, move pure new cases to the light owning assembly while keeping
real-host integration coverage for composition and owner authority.

## Scheduler owner / PostgreSQL / projection / authority

| ID | Boundary | Required assertions |
| --- | --- | --- |
| V-OW-01 | Save actual PostgreSQL commit then projection failure | Stored exact ID/payload/version exists once; honest committed-with-warning receipt; retry read causes no second save/fire |
| V-OW-02 | Actual commit then reload/log failure | Receipt retains committed identity even without refreshed projection fields; no invented current NextFire value |
| V-OW-03 | Pre-commit validation/refusal and uncertain commit | Known refusal remains no-save; uncertain commit remains Unknown, not inferred by name or broad exception class |
| V-OW-04 | Pause/resume commit then sync failure | Saved flag retained; do not claim projection complete; conflicting UI commands do not duplicate unintended work |
| V-OW-05 | Delete commit then sync failure | Exact deletion retained; UI cannot say no mutation; current trigger/revocation policy remains authoritative |
| V-OW-06 | Display history cascade and retained FireAdmissions | Real configured cascade confirmed; delete copy accurate; exact retained admissions survive delete/recreate semantics |
| V-OW-07 | Projection repair, if introduced | Separate explicit owner operation, current exact plan state and authority; never a read refresh or historical redispatch |
| V-OW-08 | Exact Workflow/default version semantics | Supported defaults characterized; explicitly saved version is never silently replaced; Process production target remains unavailable |
| V-OW-09 | Local operator versus managed Agent authority | Submitted target/profile is exact; no renderer-supplied privilege; original Agent lease/governance ceiling preserved |
| V-OW-10 | Source lease and transaction ordering | Existing lease-before-SQL and commit checks remain; no provider execution while holding UI/persistence locks |
| V-OW-11 | Fire idempotency / accepted observation | Affected contract moves preserve immutable snapshot, original caller/run/correlation identity and read-only observation |
| V-OW-12 | Timezone/CRON/misfire/bounds | Existing installed owner computes supported expressions/timezones and boundary cases; UTC/display distinctions and enums preserved |

Use current repository-supported isolated PostgreSQL (18-compatible at review) and test-owned
fixtures. Respect its pinned image/leases/configuration; do not copy an unrelated old test
database or change defaults to force results. Fault-control only the precise projection,
reload/log or external boundary; a fake Save returning “SavedWithWarning” does not prove
V-OW-01/02. Preserve real owner validation and source policy. No live mailbox/provider/network
account is needed for a harmless versioned Workflow fixture.

Investigate actual consumers before adding receipts: managed Scheduler Agent tool handling,
current APIs if present, workflow/authority contracts, integration tests and transfer/migration
readers. Preserve expected success/exception compatibility or adapt those exact callers and
test them. Broader fire/lease tests are selected by actual contract/owner changes, not skipped
merely because the screen was the motivating change.

## Browser / assets / integration

| ID | Journey | Required actual observation |
| --- | --- | --- |
| V-BR-01 | Production `/scheduler` and standalone sandbox | Complete interactive desktop UI with real controls; no console/page/asset errors; no hidden DB dependency in sandbox |
| V-BR-02 | New schedule, edit, pause/resume, delete | Exact persisted fixture identity and values; correct confirmation text and known save behavior |
| V-BR-03 | Unblurred name and malformed/valid raw JSON | `input` updates captured; visible errors retain text; later edits survive held validation/commit/readback |
| V-BR-04 | Dismiss/reopen successor while old operation waits | No stale close, focus transfer, forced tab or successor receipt; old admitted write is not replayed |
| V-BR-05 | Real CanvasCalendar selection and mouse double-click | Correct planned event opens exact plan once; history/empty canvas does not edit unrelated plan |
| V-BR-06 | Two calendar hosts, tab removal/remount, late JS import | Per-host callback/disposal; one host cannot steal/detach the other; no leaked reference/listener |
| V-BR-07 | Real CSS/assets and published Production host | Scoped/deep styles actually match; canvas/theme/fonts/scripts present; source mode and published URLs work |
| V-BR-08 | Actual Scheduler Agent hook/context | Exact managed identity, source/view/selected plan/target/overlay and readiness/completion behavior retained |
| V-BR-09 | Bounded large/history/status scenario | Calendar and lists remain usable with real component paths; no fake live status or unbounded query |

Use actual canvas interaction when closing V-BR-05; invoking a .NET method directly is only
unit-level evidence. Record screenshots at a consistent desktop viewport (for example
1600 × 1000), inspect them, and include all four tabs, dialogs, held-newer-input and error
states. A screenshot alone does not prove persistence; pair it with exact stored identifiers.
An external fixture may replace an Agent execution/provider effect, but the real page's
context composition and authorization path must remain under test.

## Graph / build / packaging / closure

Evaluate direct and transitive project references, package/runtime/static-asset closure,
public types and startup services. Reject forbidden concrete runtime/EF/provider edges and
unexplained unresolved dependencies. Do not limit scanning to `.razor @inject`. The sandbox
must neither reference the production Web project nor start Quartz/Workflow/Agent/DB services.
Prove all modules still resolve in actual Web composition after pure contracts move.

Directly build the new UI/contract/presentation-if-any, sandbox, owner module, Web and affected
public consumers in the supported isolated configuration. Register new tests in actual
component/Stable solutions and every applicable CI shard/selection. Do not put tests into
the product solution. Read pipeline conditions rather than assuming an assembly with the
right folder name is executed.

Run [DEV_LOOP.md](DEV_LOOP.md) measurements with original Web, extracted Web and real sandbox.
Preserve asset provenance, required watch inputs and exact source-mode sibling revisions.
Document limitations separately from correctness. Test/graph performance numbers must not
be copied from the prior Plugins record as if they measured this module.

Run current required documentation/evidence, portability/tool self-tests, reviewed baseline
reconciliation, secret-artifact checks and whitespace gates. Prefer fixing genuinely new
portable-code issues; review additions/removals individually if the baseline format requires
fingerprint updates. Never weaken patterns or mark the entire proposed tree allowed.

Cleanup: record exact owned PIDs/container IDs/names/labels/ports and fixture roots, restore
measurement files byte for byte, verify no leaked fixture databases/hosts/watchers, and stop
only owned resources. Do not access the ordinary app/database on port 5032 or modify sibling
repos. Signing stays enabled for user-repository commits; no remote writes.

## Scope-sensitive test escalation

The entire broad Stable/live/platform suite is not an automatic requirement after each step.
Current repository policy and actual changes decide. Shared lifecycle, build policy,
serialization, cross-module owner/admission or migration changes reopen broader proof;
ordinary renderer relocation still requires direct impacted production/browser/owner proof.
Write the decision and rationale, exact exclusions and unresolved blockers. Continue independent
work in this bundle, but never count an unrun required lane as green or declare the whole
slice complete while a required regression is failing.
