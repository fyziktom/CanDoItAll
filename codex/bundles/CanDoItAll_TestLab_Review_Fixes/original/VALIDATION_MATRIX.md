# TestLab validation and acceptance matrix

These are behavior obligations, not a requirement for one class/test per row. No C# or application proof in this document was executed during handoff preparation. Derive actual test counts from the current source and confirm discovery before execution. Old Collaboration counts are not quotas for this task.

## A. Deterministic boundaries and real forms

| ID | Arrange / trigger | Required proof |
|---|---|---|
| S0.1 | First shell contributor awaits a controlled task and ignores cancellation; dispose host; release it | No second contributor starts, no disposed token-source access, no dead-host render/notification, no unobserved task failure; disposal does not wait indefinitely |
| S0.2 | Cooperative cancellation, genuine live failure, and queued Changed event crossing disposal | Expected shutdown is distinguished from a real failure; real diagnostic remains; queued callback rechecks its lifetime; normal catalog/focused-window behavior stays valid |
| R1 | Reapply the same query tuple and change tabs with a dirty form | Same draft, `EditContext`, raw validation and user text; no unnecessary owner reload |
| R2 | Controlled plan reads A -> B -> A, late success/error/cancellation and Reset | Only newest eligible result applies; no stale finally unlocks new request, no stale error replaces current data |
| R3 | Select project A -> B -> A while party list and saved-party lookup complete out of order | Options belong to the active editor/project request; old results cannot clear/relabel its responsible party |
| R4 | Initial load failure; successful empty; filter-empty; requested missing plan; refresh failure | Distinct visible states; same-scope stale accepted data can remain with warning; no fake new editor or another scope's cached values |
| R5 | Saved party not in current list, resolves separately; then unresolvable or lookup failure | Stored ID preserved; resolved fallback or explicit unavailable state; failed read not treated as empty success |
| A1 | Load current project, then retire/recreate same ID before save | Original profile/project/lifetime admission is returned unchanged and refused; no silent recapture, write or success receipt |
| A2 | Global plan, invalid Guid.Empty/mismatched admission, historical plan and explicit rebind | Null/null global remains valid; invalid pair refused; historical reference retained; explicit new project selection follows existing owner rebind behavior |
| A3 | Old-lifetime plan and new project share public ID | Current-project filter/label does not bind old plan to new lifetime; history remains accessible |
| W1 | Save and Enter/double dispatch on a new editor before completion | Exactly one owner call and one new persisted aggregate; retired origin does not block an independent successor |
| W2 | Snapshot submitted, then mutate live scalar/child values before owner accepts | Owner sees original stable submission, not mutable live state |
| W3 | Successful save, hold read-back; type title/notes through real input before blur; then accept normalized values | Newer text and form/validation identity retained; untouched fields adopt accepted values; IDs retained; tab/filter changes survive |
| W4 | Nested row added, removed, reordered or replaced with an equal-looking row while save waits | Returned IDs attach only to captured eligible original rows; no matching by text/current position; new rows stay local; no wrong-row reset |
| W5 | Existing stored child removed in fixture, owner assigns replacement ID; secondary fault; explicit resave | Actual replacement ID preserved, resave does not recreate it again; unaffected children keep stable IDs |
| W6 | Change editor or project during pending save, including A -> B -> A and Reset/create | Old commit can be reported with its own identity but cannot mark new target saved, patch its parent/children or reset its draft |
| W7 | Owner refuses title/project admission before commit | Editable draft retained with correct refusal; no saved state, postcommit-only warning or accidental unknown-outcome lock |
| W8 | Owner commits, then Search or Activity throws; repeat for refresh failure/missing read-back | Preserve confirmed parent/child IDs and truthful warning; no automatic write replay; explicit later edit/save remains possible |
| W9 | Genuinely unknown write outcome | Preserve submission and prevent blind replay; recovery explains uncertainty; a similarly named row is not proof of exactly-once commit |
| W10 | Dispose/retire page while read or dispatched write is pending | Owned reads/handlers cleaned up; write outcome not inferred from cancellation; no stale callback or disposed-service use in a replacement |
| F1 | Invalid real form/input value and section switch, then valid correction | Same validation context/raw input retained; invalid submit not disguised as success; real form dispatch and disabled semantics verified |
| F2 | All status choices and existing defaults; recorded dates/timestamps across save/reopen | No enum/schema/value drift, no accidental loss of stored timestamp precision/time information due to UI move |

Use `TaskCompletionSource` with asynchronous continuations or the existing controlled test-probe helpers, not sleep-based race orchestration. A before-blur proof must use real `input` events; a model property assignment is not equivalent. Find and dispatch DOM events together on the renderer dispatcher, await event tasks and do not execute blocking owner reads inside `WaitForAssertion`.

For S0, use the current `DisposeRenderedComponentsAsync` helper when disposing rendered roots within a reused bUnit context. The existing immediate-completion shell tests do not constitute delayed-disposal proof.

## B. Sandbox parity

Each of the four real TestLab sections must be accessible in the standalone sandbox, with real controls, styles, forms and active state. A single static card is not enough. Cover the scenarios defined in the prompt, and use the same semantic assertions where production/session and scenario layers overlap.

| ID | Scenario transition | Required proof |
|---|---|---|
| SB1 | Admitted fake save on A; select B; complete save; reopen A | Original fake aggregate contains exactly one committed result; B's active editor is unaffected |
| SB2 | A -> B -> A with a second pending editor operation | Old completion cannot release/update/notify for the newer draft; separate stored writes remain distinguishable |
| SB3 | Delayed reference/plan read superseded, scenario switched or disposed | Pending waits terminate safely; no callback mutates the replacement scenario |
| SB4 | Committed-with-warning and Retry refresh | Fake store really contains the accepted record/child IDs; retry only reads; fake UI does not manufacture a second commit |
| SB5 | All tabs, large realistic data, filtered empty, missing references, invalid editor and raw-input race | Same components and actual bindings as production, faithful lifetime/validation semantics, no backend registration |

A scenario test proves scenario behavior. It cannot replace the actual owner commit/persistence, schema, profile or production browser proof below.

## C. Existing production and owner baselines

Inspect the current full files and actual references; source names below are discovery seeds, not an exhaustive exact filter. Do not remove old tests because the UI code moved.

| Current baseline | Preserve / execute |
|---|---|
| `tests/Components/CanDoItAll.Tests.Components/ConversationShellHostTests.cs` | Existing catalog/actions and focused window behavior, plus S0 lifecycle regression |
| `tests/Components/CanDoItAll.Tests.Components/OwnerPostcommitPageTests.cs` | Every affected TestPlan theory row and TestLab-specific fact: search/activity committed warnings, same-editor IDs, read-back failures, late selection, retained/replaced/missing children, retired project refusal |
| `tests/Integration/CanDoItAll.Tests.Integration/TestLabOwnerPersistenceTests.cs` | Exact owned model/complete-schema parity, no foreign query, historical restart and all parent/child IDs, search route, profile isolation |
| Project-admission and project-lifecycle tests | Actual references touching TestPlan saves, global/history/current lifetime filtering and rebind; derive bounded filter through Code Analytics/source |
| CRM/HR, Workbench projections, Project Structure and automatic placement consumers | Build actual moved-type consumers; run affected owner/bridge tests, including relevant portions of `CrmHrCrossModuleIntegrationTests`, `WorkbenchOwnerProjectionIntegrationTests`, `ProjectStructureAgentIntegrationTests` and `ProjectStructureAutomaticPlacementIntegrationTests` as discovered |
| Existing Collaboration source/real-form regression | Preserve accepted fixes; expand rerun if shared inputs/lifetime/component contracts are changed |
| `CollaborationBrowserTests` | Run after S0 against the real owned host to close the shared-shell issue; retain actual notification/escalation and accepted mark-read assertions |

The mixed `OwnerPostcommitPageTests` also contains Resources-only scenarios. Select TestPlan data rows accurately if the runner supports it, or run the bounded whole class; do not accidentally discover zero cases with a method-name filter that omits parameterized rows. Changing shared test infrastructure may make Resources rows affected as well.

Do not force production-only fixtures into the lightweight renderer project. Have a genuinely light renderer/scenario test lane and separate real host/owner proof, using the repository's existing test solutions and CI sharding conventions.

## D. Actual browser journeys

Use the actual Web route and a separate backend-free sandbox host at a supported large desktop viewport; the current Collaboration proof uses **1600 x 1000**. Capture screenshots and inspect them, not only generate them.

**P1 — end-to-end aggregate:** open `/test-lab` with owned fixture projects and responsible parties; create a named plan through actual controls, enter Overview values, add and edit a case, evidence record and recorded run through all three tabs, then save. Wait for a completion observation tied to this operation/plan, not button disabled or pre-existing Ready. Read the owner outside the renderer dispatcher to confirm project admission, responsible party, all values and all child IDs. Reopen/deep-link, edit and save again; parent and unchanged child IDs must remain stable and no duplicate aggregate exists.

**P2 — navigation and selection:** cover `?planId=...`, project-based creation context, global plan, text/project/phase/result filters, no-match/reset behavior, section changes with dirty input, selection/Reset and requested missing plan. A background list/filter update must not silently select another editor. Exercise a saved historical/unavailable reference via isolated fixture data without downgrading the owner admission checks.

**P3 — asynchronous form correctness:** use approved controlled test seams for a delayed read/save/reconciliation and same-target typing before blur. Cover project/party A/B/A and duplicate submit at deterministic lower layers; at least one real browser path must exercise the actual unsent-input/reconciliation behavior. Test-only delays/faults belong in fixture dependencies, never production URL switches or publicly exposed mutation endpoints.

**P4 — known postcommit failure:** inject a one-shot Search or Activity fault in the owned test host, save through the real form, verify saved-with-warning and owner IDs. A Retry refresh or an explicit subsequent edit must not create a second plan/child set. Existing real-host component tests remain mandatory even if a browser fault probe needs separate wiring.

**P5 — shared shell and shutdown:** navigate away/reopen/dispose with the real shell present. Preserve normal agent/chat catalog behavior and review server logs alongside page/console/asset failures. Do not stub/remove the shell from production proof to avoid S0. The deterministic S0 tests establish the actual rare ordering; a clean repeated browser run complements them, not replaces them.

**P6 — standalone parity:** browse every real sandbox section, representative and failure scenarios, saved-but-unavailable references and controlled completions. Verify styles/icons/fonts/scripts and that the host launches without a database or production DI. Keep fake storage and active view effects separate as in SB1–SB5.

Owner-level state is required when the UI action changes durable data. A toast, count, screenshot or a seed that already contains the desired record is not proof that a form performed the write.

## E. Build, discovery and runtime safety

Build every changed production project directly: shell for S0; actual TestLab.Contracts, TestLab.UI, TestLab module; affected consumer/Composition/Web projects; and the standalone sandbox. Then build the owning test projects for the same final source/configuration. Do not use `--no-build` with stale assemblies.

For each selected filter, record expected and actual **case** counts and exit codes before execution. This is a command pattern, not ready-made paths/counts:

```powershell
# Use the actual project, supported configuration and verified bounded filter.
dotnet test $project --configuration $configuration --list-tests --filter $filter /m:1
if ($LASTEXITCODE -ne 0) { throw 'Test discovery failed.' }
# Compare discovered data-driven cases with the source-derived expectation before proceeding.
dotnet test $project --configuration $configuration --no-build --no-restore --filter $filter --logger "trx;LogFileName=$lane.trx" --results-directory $results /m:1
if ($LASTEXITCODE -ne 0) { throw 'The selected validation lane failed.' }
```

The current testing guide has separate Unit, Components, Integration, Memory, Playwright and Stable solutions. Confirm new lightweight test projects are included in their owning solutions and the actual CI shards, not the product solution. Source-mode builds use the required sibling checkouts/revisions. Broader stable/cross-platform/live/provider gates follow named current invalidation triggers, not completion of every small phase.

Use only the explicitly isolated PostgreSQL 18 endpoint through `CANDOITALL_TESTS_POSTGRES_CONNECTION`. Record `server_version_num`, a sanitized endpoint and owned resource identifiers. Do not log passwords. Leave `CANDOITALL_PLAYWRIGHT_BASEURL` unset for the normal owned fixture. The user's port-5032 app, ordinary retained data, containers and unrelated processes are off limits. Task-specific project configuration such as `TestLabProof` may avoid locked outputs if current tooling supports it; solutions accept only their declared configurations, and browser child configuration must match the built output.

## F. Graph, assets, static gates and receipts

Follow [DEV_LOOP.md](DEV_LOOP.md). Prove evaluated source-mode isolation plus runtime/asset startup; static reference rules must reject forbidden transitives and unresolved references rather than silently skipping them. Verify Tailwind/static web assets and actual real BaseLib controls. Measure actual startup/edit loops with provenance; no historical Collaboration number is a new TestLab benchmark.

Run required portability-static after final source/merge changes, review added/stale findings and repair actual defects. Intentional baseline changes require review and a final clean enforcement run **without** baseline-write mode. Include relevant documentation checks, local links and `git diff --check`. Do not merely add suppressions until a script passes.

Receipt fields: start/final branch/HEAD, sibling revisions, SDK/OS/mode, exact changed files and kept ownership, S0 outcome, all built projects, each exact discovery/execution command with expected/discovered/pass/fail/skip counts, evidence paths, measured graph/watch/latency data, failed or unavailable lanes, cleanup and signed commits. Failed, skipped, quarantined, unavailable and historical observations remain separate from current success.
