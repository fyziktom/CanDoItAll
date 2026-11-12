# Processes integration PC4 validation

Status: **COMPLETE for implementation and required validation; prior tool-directory cleanup remains explicitly blocked**.
Tested executable source: `ca195c794349d4e4eb653bacb7605ed83df86c4b`.
The [machine record](processes-integration-pc4.json) contains commands, test identities,
source tuples, original failures and all acceptance dispositions. The
[architecture record](../architecture/processes-integration-pc4.md) describes the corrections.

## Current closure result

All **36 acceptance groups**, **123 carry-forward rows** and stages S0–S5 have current
evidence. HTTP enrichment/compilation share one captured authorized definition closure;
predecessor recovery reuses only proven origin and refuses an unproven retained candidate
without a replacement child; role deletion removes owned head material so Add/ImportRole/
ImportProcess preserve exact defaults and unrelated immutable data. The final proof also
covers original workflow, chat, file, cancellation, voice, scope and replay behavior.

The operator resumed after storage recovery at clean
`8eba867a69d1a9987ec1793760ac8c9a657e4bd8`. Product/test source still matches `ca195c794`;
subsequent protected-path changes are Markdown guidance. Fresh proof uses **Release**,
Windows, SDK **10.0.303**, clean Components
`f3745356182444656edfdef56aa827095632d3ea` and FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. FileTools' current CI pin
`498b36825bd5a5222429972af120b04becf4b3f6` has the same tree
`6bc360281b6ad13ddec5e813f0a00e26b5bc7d6d`; no repin was required.
SharedInfo is `30330978cf48d702382663a3d5231880fc034936`. This resume changes no product,
test, sibling or shared-bundle source. Standard outputs were reused and builds serialized.

| Fresh check | Outcome |
| --- | --- |
| Product Release build | PASS; 192.74 s |
| Stable Release build | PASS; 72.77 s |
| Playwright Release build | PASS; 60.62 s |
| Stable discovery | 17,346 cases, 34 canonical projects |
| Complete Stable execution | **17,401 passed**, zero failed/skipped; 361.15 min summed shard command time |
| Integration / Unit / Components | 3,460 / 9,588 / 2,975 passed |
| Remaining 31 Stable projects | 1,378 passed |
| Chromium native browser union | **18/18 passed**, 636.52 s |
| Portability static | PASS; 15,380 reviewed findings unchanged; final enforcement without baseline write |
| Canonical pending-model check | PASS; no pending changes and no database update |
| PC4/shared package integrity and helper checks | PASS |
| Documentation validation | PASS; 407 maintained Markdown files |

The broad gate is a **fresh complete union of 63 class shards across 34 projects**, using
the canonical repository shard runner and Stable filter from [Testing](../testing.md),
matching Release assembly hashes and a complete TRX for every shard. All shard indices
are included and run serially. Browser proof is separate. Reconciliation checks discovery
multiplicities, expanded theories, distinct execution IDs and exact outcomes. Each class
belongs to exactly one completed shard. Nonserializable theory rows can share a parent
test ID; those groups are explicitly reconciled with the discovered theory and separate
row execution IDs.
Seven nonserializable theory stubs expand into 55 additional execution rows:
16 in Memory, 34 in Unit and five in Integration. Every expansion is accounted for.
The longest local shard took 33.11 minutes.
Current CI gives each of three host shards 180 minutes and each of five Linux shards
90 minutes, including their own build/setup. Local serial execution and its finer
partition do not certify those remote budgets; no timeout or filter was changed.
Eight unit display labels are repeated because the adapter truncates long arguments;
their multiplicities are preserved rather than silently deduplicated. The full identity
manifest and SHA256 receipts are under `resumed_closure.reconciliation`.
Five Unit labels contain supplementary Unicode characters that TRX writes as literal
UTF-16 surrogate escape pairs. Their exact discovered counterparts are recorded alongside
the unchanged raw labels; no other display-name differences are ignored.
Earlier partial campaigns and overlapping focused runs contribute no additional total.

An application crash stopped the initial unsharded Integration process and its owned
database. Its last log write was at 18:49:59 UTC on 2026-10-10, retaining 1,667 passing
console rows but no complete TRX, so it contributes zero to the final total. The existing
database cluster recovered and was reused. Whole-class batches now preserve complete result files at
bounded checkpoints; this is a complete sharded union, not one uninterrupted invocation.
A second app crash then stopped Integration shard 9 before it produced a complete TRX.
All 51 completed shards (15,529 passes) survived and were checked before reuse. The same
native data directory recovered again, and only the unfinished shard was retried in a
new attempt directory. Its interrupted first attempt contributes zero to the final total.

A temporary discovery-runner aggregation error occurred after all 34 discovery commands
completed. Its corrected total was independently recomputed from the complete logs;
the original harness failure remains recorded. No product assertion was changed.
The restarted runner's initial process-identity guard compared a parsed timestamp with
a string; that preflight error was corrected before any tests started and is retained
separately from product outcomes.
Initial native fixture setup also required trimming the newline from its encrypted
credential input before initialization. This was a setup-script correction; no product
or database test failed in that setup step.
The EF CLI emitted its existing 10.0.3 versus runtime 10.0.4 warning.

## Browser, architecture and evidence review

The 18-case union covers role deletion/reimport, independent editors, lost-response and
read-back recovery, HTTP/Workbench publication agreement, native workflow/chat/files/
cancellation, both voice scopes, actual OS restart/exact prepared workflow, canvas/import
durability and known/unproven predecessor origin after same-ID project recreation.
Native read-back and stored plan/resource assertions supply behavioral proof.

The current index hashes **71 browser files**; six 1920×1080 screenshots
were visually inspected, including normal workflow state, maximized canvas, role dialogs,
Workbench launch and retained run detail. Fields and primary/footer actions are visible;
overlays and scroll ownership remain usable. The reviewed nine Processes renderers and
their active owners have no remaining forbidden rendering edge. Structural UI decoupling
is closed; no new renderer extraction or asset graph change was needed.

Captured traces contain no HTTP response status 400 or higher. Six restart traces retain
old pages while owned hosts stop, producing expected socket/negotiation console errors.
Both voice traces also record a fixture init-script TypeError on `about:blank` before
navigation, when `mediaDevices` is absent. The actual application-page assertions pass
with four requests, three track retirements, two starts and zero model requests per scope.
Deliberately unavailable projection reads and disposed-provider teardown logs are retained.
This is not a claim that all browser/server logs are empty. Deterministic media/provider
fixtures do not prove live speech or paid model services.

The restored CodeAnalytics tool produced scoped snapshot
`snap-20261010161841-a5c372cc`: one Application project, 102 documents, 307 types,
3,713 members, zero diagnostics and nine retained-recovery references. HTTP/Workbench/
Razor/test consumers are outside this index and were reviewed explicitly and executed.
Components MCP was absent from discovery. No repository-wide semantic-index completeness
claim is made. Existing Fast/Parity evidence applies only to the unchanged asset graph.

Current raw proof is in `.artifacts/pc4-closure-20261010` and `output/playwright`.
Exact fixture-credential scanning covers proof files and every uncompressed trace member;
heuristic findings were reviewed as public synthetic test data or executable expressions.
The current safety receipt reports zero exact credential occurrences and no unreadable or
oversized text omission. Historical missing raw files were not retroactively restored.

## Signatures and resource disposition

All 13 preceding PC4/maintenance commits were reverified using fingerprint
`96E836FAA8854EE98ABC10903C206549E1D7EAD6`. The new build/browser checkpoint is
`3ca1fe0c8e22c3cc806fc14733d73c413f847779`, with verified parent `8eba867a69d1a9987ec1793760ac8c9a657e4bd8`.
The final report-only commit references the tested source and is verified after creation;
its hash is not inserted into itself. No push, merge, deployment or history rewrite occurred.

Docker fixture setup stalled before a container existed. Its exact owned CLI and empty
volume were removed. One checksum-verified installer-pinned PostgreSQL 18.6 native cluster
on `127.0.0.1:55924` served browser and Stable lanes. After the last test its exact process
was stopped, the listener was released, and its runtime/data, archive and encrypted
credential were removed. All **40 preexisting containers** retain their IDs, states,
start times, mounts and ports, including the ordinary 5032 database. Standard Release
outputs and installed MCP tooling are preserved.

**Cleanup blocker:** automatic approval review rejected removal of two inactive, untracked
prior tool-recovery directories, reporting only `blocked by policy`:

- `.artifacts/mcp-installs/CanDoItAll.Mcp.CodeAnalytics/failed/failed-20261010-101825372` — 33,034,877 bytes.
- `.artifacts/pc4-recovery-20261010/codeanalytics-smoke` — 61,514 bytes.

They remain; their deletion is not claimed. The historical Windows 55904 listener belongs
to the shared Docker backend and was not terminated. Current PC4 fixture cleanup is
verified separately from those explicitly retained resources. No Docker/WSL restart,
global prune or offline VHD compaction was performed during this resume.

Authoring-aware portable transfer/export remains safely refused pending its separate
versioned-history/remapping design. Database paging for large catalogs is also separate
work. Local Windows results are not a dispatched multi-platform GitHub CI run.

## Earlier attempts and storage recovery (historical)

The following execution and cleanup history retains its original source/configuration
scope and incomplete outcomes. The current result above supersedes its open product gate;
it does not change a failed/interrupted run into a pass or restore deleted artifacts.

On October 10 the operator removed `.artifacts` after further crashes. The prior native
test server and runner are gone. The maintained machine record retains the reported
focused results and 28 completed component/UI project summaries (4,057 passes), but the
referenced raw TRXs, logs and recovery scripts are no longer available. The last observed
integration count was 1,462 passing console rows, with no complete integration report;
those partial rows do not close the gate. Browser files outside `.artifacts` still exist.
Raw-artifact availability is recorded separately from the outcomes observed before deletion.
The resumed Stable campaign reused the existing PC4 Docker fixture after cleanup and
tool restoration. At the operator's request it was stopped at 12:47 UTC to prioritize
storage cleanup. It had 2,597 passing integration console rows and zero observed failure
rows, but no completed TRX; this is incomplete validation, not accepted full coverage.
The owned runner/test processes are gone and its database fixture has now been removed.

CodeAnalytics was rebuilt with the repository installer after its configured executable
was deleted. Both staged and installed protocol checks passed against a disposable
one-project/one-test workspace. The initial smoke request lacked the protocol's `request`
wrapper; the corrected request resolved the expected caller with healthy workspace data.
This verifies the tool installation, not a new semantic analysis of the product changes.
Automatic approval review blocked removal of the failed install candidate (33,034,877
bytes) and smoke workspace (61,514 bytes), reporting only `blocked by policy`.
Those two minor directories remain. The operator prioritized
the large Docker storage instead of authorizing another deletion attempt. The restored
`mcp-installs/.../current` directory is an
installed tool and is excluded from temporary cleanup.

The Windows Docker endpoints timed out, but the existing Linux CLI could reach the daemon
through its local socket. Exact labels, mounts, ports and current consumers established
ownership before removing **40 obsolete bundle containers, 58 exclusive/orphan bundle
volumes, 159 empty bundle networks and seven unused bundle image tags**. This includes
the operator-named `53f7a5b0e8db490448aa41bbb55113f372d866077f64debbbe76e013114761e7`.
Two of the 40 containers and their anonymous volumes disappeared after the owned stop;
the initial removal attempt and subsequent verification are retained separately.

The operator separately authorized unused build-cache cleanup across the current builder.
Docker reported **257.7 GB reclaimed**; the remaining eight in-use cache records total
152.4 MB. Its filesystem used about 39.3 GiB afterward, versus approximately 325 GiB at
entry. All 41 retained containers kept their original state and start time. The cache
operation preserved their images and volumes, including `candoitall-postgres18-5032`,
the hotel demos, manual provider stacks and WB6 manual candidate. No engine/WSL restart,
ordinary database operation or blanket container/volume prune occurred.

An online filesystem trim succeeded, but the Windows backing file remains 356 GiB and
Windows free space was about 34.8 GiB immediately after trim. Reclaimed Docker filesystem
space is therefore not reported as reclaimed Windows disk space. Offline administrator compaction, with its
service interruption, was not performed. Exact removal and protection receipts are under
`.artifacts/pc4-recovery-20261010` and summarized in the machine record.

The cleanup policy is in [Testing](../testing.md#bundle-resource-cleanup) and root agent
instructions, signed at `640fece824cfdf1f6c5985f6184c7eec199b7a9d`. Documentation validation
passed for 407 maintained Markdown files and portability enforcement passed with 15,380
reviewed executable-source findings unchanged for that policy change.

The subsequent operator-directed stop released the current PC4 fixture. Its exact container
and exclusive volume were removed, bringing the totals to **41 bundle containers and 59
volumes**. All 40 retained containers kept their state and start time, including the 5032
database. Docker still reports zero reclaimable build cache. The original final cleanup
check remains `INCOMPLETE` because Docker's shared backend retains the Windows 55904
listener despite no container publishing it; no shared backend termination was attempted.

The focused storage inventory found **54.48 GiB in `output`** and **37.57 GiB in the Web
project's `bin`**. The largest output subfolders are `m122finalw` (24.32 GiB), `m122w`
(8.73 GiB), `maf-1.22` (8.64 GiB), and `ci-96391207179` (5.99 GiB). The current `.artifacts`
is only 0.17 GiB. Sizes are logical file bytes; parent and child totals overlap. The
machine record carries the measured paths and build-folder inventory. Additional test
build outputs include Playwright (28.03 GiB), Integration (25.63 GiB), Unit (23.85 GiB),
TestLabBrowserFixture (23.31 GiB), and PluginsBrowserFixture (22.70 GiB). These folders
were inventoried, not deleted. The operator's independent `.mcp-state` deletion (reported
as 25 GB) was excluded from the focused scan and from claimed cleanup savings.

### Source and signatures

The operator subsequently cleared `.mcp-state` and the previously listed large build
folders, then requested cleanup of the remaining main-repository `bin` directories and
repair of build/fixture instructions before further component work. The remaining
ownership-checked inventory measured 350 untracked, inactive build-output directories
at 147.028 GiB. All 350 directories are now verified absent. A fresh full inventory
within the same scope found zero remaining `bin` directories; no tracked files or active
consumers required deletion. Native PowerShell used a bounded maximum of eight workers
and repeated path/consumer checks. Installed MCP tools, sibling repositories and ordinary
5032 resources were excluded. The restored CodeAnalytics executable remains present.
The cleanup verification at `2026-10-10T15:01:33.9824736Z` records plan/receipt hashes and
Windows free space of 521.14 GiB. Logical output sizes are
not uniquely reclaimed physical bytes; the operator's earlier independent deletions are
not attributed to this remaining-bin cleanup. Deleted assemblies require a fresh standard
build and build-backed discovery before the next validation run; historical test results
are not new execution.

The current shared status is `MINIMAL_REPO_CHANGE` for this requested policy repair.
Root guidance, Testing, maintained UI/test READMEs, the existing shared companion and
PC4's future build instructions now use Debug/Release reuse and serialized conflicting
builds. UI iteration uses dotnetwatch when useful. Fixed project asset-mode/framework/
runtime layouts remain bounded and reusable. A new bundle/stage does not itself require
a new Docker fixture; dedicated fixtures need a concrete test reason, ownership and
last-use cleanup. Historical custom-configuration proof remains historical.

Canonical SharedInfo standards, templates and skills were updated in verified signed
commit `30330978cf48d702382663a3d5231880fc034936`. The four affected installed skill files
match their canonical sources. SharedInfo validation passed 47 skills, 411 Markdown files
and 12 PowerShell files with zero failures. The shared helper's Windows CRLF fixture was
corrected without weakening its assertions; all 18 helper tests and both package integrity
checks pass. Documentation validation passes all 407 maintained Markdown files. Fresh
full-tracked portability enforcement passes without a baseline write: 9,411 files and
15,380 reviewed executable-source findings unchanged. No product source/project change or new
native/UI execution is claimed by this maintenance.

Entry was clean `components-decoupling` at `247e72a2f1caccc8b3cc720291eb4094fdb8e9ff`,
tree `9aef76a60d42631efd4c45575d48a40ba1b43740`. Work used the current checkout without
reset, rebase, stash, history rewrite, push, merge or deployment. Historical handoffs
were unchanged at the product-proof checkpoint: **NO_CHANGE**. The subsequent requested
shared execution-policy repair is **MINIMAL_REPO_CHANGE**, as recorded above; historical
handoffs and product proof retain their original provenance.

Windows / SDK 10.0.303 used clean siblings Components `f3745356182444656edfdef56aa827095632d3ea`,
FileTools `3a080ecd31068a77c1e1bd639f7a78e21c93db85`, SharedInfo `6acab7a59325bdb9c5b36539d52403fa9b274cd6`.
FileTools CI pin `498b36825bd5a5222429972af120b04becf4b3f6` has the same tree
`6bc360281b6ad13ddec5e813f0a00e26b5bc7d6d`; no repin was warranted.
CodeAnalytics, Components MCP and dotnetwatch were unavailable in fresh discovery.
Source/caller/DI/serializer/Razor review, evaluated references, owning builds and
recursive/public-type tests provide the recorded fallback, not a semantic index claim.

Local PGP unlock was requested at entry. Every checkpoint passed `git verify-commit --raw`
and expected-parent checks using fingerprint `96E836FAA8854EE98ABC10903C206549E1D7EAD6`:

| Signed commit | Scope |
| --- | --- |
| `3b558ed8a4caaa84da316633a3d55432b4af7cc1` | Canonical captured HTTP executable content |
| `104800ab6d1c37fc886137ac34eb4ac8addad97d` | Role resource ownership |
| `284f0de20d1bdae568cafafee6d06c3d5c102ca9` | Origin-aware predecessor recovery |
| `cabefec67be46c15cd4108a14ddee47702db2863` | HTTP/browser regressions |
| `20b62598d1ea0a28d7a5c63dd21fe8289c182278` | Public JSON rejects executable snapshot injection |
| `e9472752ad68c7d1add689ac1364021cb310f5b1` | Canonical stopped-child identity and native proof |
| `77ccb70d6361fd65a4b92d1d68b783de89e52f1c` | Paired Workbench/HTTP publication proof |
| `ca195c794349d4e4eb653bacb7605ed83df86c4b` | Populated upgrade and native/OS restart proof |
| `640fece824cfdf1f6c5985f6184c7eec199b7a9d` | Verified temporary-resource cleanup policy |
| `86a7659b8e44014cdd66887ce20a1b66d745e364` | Standard build-output and isolated-fixture reuse policy; installed canonical skills synchronized |

Production last changed at `e9472752`; later checkpoints change tests or documentation. A report-only
commit references the tested source, avoiding a circular self-hash.

### Isolated resources and builds

The first fresh ProcIntegrationPC4 build exhausted disk; its owned process and incomplete
output were cleaned and the failure retained. Focused PC4 builds subsequently refreshed
isolated ProcAuthoringPC3 outputs. After space recovered, the full Web dependency graph
passed a **fresh ProcIntegrationPC4 build in 143.71s**, zero errors and one existing nullable
warning in StructureWorkspace.razor:71. Legacy OS browser tests use that clean Web output
with the fresh ProcAuthoringPC3 test assembly. Release product/Stable builds are separate.

During the earlier validation attempt, Docker metadata timed out. No shared engine restart or denied old-PC3-output cleanup was
performed. The repository installer-pinned PostgreSQL 18.6 Windows archive was downloaded,
checksum verified and extracted into task artifacts; the ordinary installer was not run.
The independent native cluster used loopback 55914, observed version 180006, SCRAM and
current-user DPAPI credentials. Every lane verified PID/executable/data identity before
connecting. No ordinary database, application port 5032 or paid inference was used.

The Codex crash interrupted the owned PostgreSQL process during Stable execution. The same
data directory recovered through PostgreSQL's normal WAL recovery at 05:23 UTC on October 10;
PID 70936 became 62276. No initialization, credential replacement or ordinary database change
occurred. That history survives in the maintained machine record; its raw artifacts were
subsequently removed by the operator as recorded above.

The previously unreachable owned Docker container `candoitall-pc4-20261009-pg18`, ID
`806dda3c2f8a3253715ae286203b6405f546571a4e1fb490184e3d5402b2e491`, port 55904, is now reachable
and is reused for the remaining Stable campaign. Its task label, exact identity, dedicated
volume and server version 180006 were verified. Cleanup follows its final required use.
No replacement PostgreSQL runtime or container was created. The earlier native archive
was removed after extraction; the extracted native runtime was later removed by the operator.

Fresh Release discovery still finds exactly 17,346 cases across the canonical 34 projects.
All 34 test assemblies identify source `ca195c7`, and executable source and sibling revisions
are unchanged. The new campaign records discovery, binary hashes and a complete TRX per
project. Its running or partial results are not a completed Stable gate.

### Focused proof and replacement sets

Each selection was discovered from a matching freshly built assembly. Counts are not
additive across intersecting runs; zero discovery never counts as PASS.

| Lane | Discovery / outcome | Boundary |
| --- | --- | --- |
| Current unit union | 64 / 64 passed | Producer, atomic, semantics, predecessor and resource ownership |
| Additional contracts | 125 / 125 passed | Resolver, scope, drivers and dependency guards |
| Strengthened public JSON | 13 / 13 passed | Replaces the same 13 PreparedLaunch cases in the 125-case selection |
| Native component hosts | Raw 212: 183 passed, 29 failed; repaired 29: all passed | Missing isolated PG caused all 29 failures; composite, no second full 212-case run |
| Processes renderer/sandbox | 21 / 21 passed | bUnit and recursive/public assembly guards |
| API documentation | 23 / 23 passed | In-memory documentation fixture; native HTTP proven separately |
| HTTP/predecessor/role first native run | Raw 24: 20 passed, 4 failed | HTTP 10 and predecessor 10 passed; role fixture omitted owner options |
| Repaired/expanded native role | 6 / 6 passed | Replaces four role cases and adds global/project Delete→AddRole |
| Populated predecessor upgrade | 1 / 1 passed | Canonical migration twice; exact original rows |
| Native preservation | 48 / 48 passed | Publication/store/scope/transfer/workspace/semantics/recovery/producer API |
| First browser union | 12 / 12 passed | Authoring 6, multisession 3, OS restart 2, Workbench 1 |
| Runtime/voice preservation | 4 / 4 passed | Global/project native 2 and voice 2 |
| Final browser selection | 11 / 11 passed | Replaces authoring 6, Workbench 1, native 2 and voice 2 after host restart strengthening |
| Legacy OS browser | 2 / 2 passed | Known/unproven origin, new Web PID, recreated project and unchanged rows |

The current browser proof has **18 distinct cases**: final 11, retained multisession 3 and
published-workflow/canvas OS restart 2, plus legacy OS 2. Prior overlapping runs are retained.
Four role unit cases failed before their fix; 9 of the first 15 predecessor cases failed, then passed and expanded to 30
predecessor cases in the 64-case union. An intermediate invalid-admission fixture was corrected
to assert the existing constructor guard; its raw failure remains.

The first legacy browser experiment seeded active old runs. The real dispatcher resumed
those runnable rows, invalidating a blanket unchanged-state assertion. Readback/restart
fixtures now explicitly seed blocked status while preserving authentic plan bytes; native
tests separately cover active/completed/failed/blocked cases. A later OS attempt clicked
before Blazor interactivity because its context had already dismissed startup. Independent
contexts using the actual startup flow fixed the test synchronization. Both failures remain.

### Native behavior and rendered review

Real HTTP routes cover captured publication/explicit-empty drivers, authored settings and
bindings, held enrichment plus republish/retirement, capability/caller checks, retry,
raw-input replay/conflict and supported key/id/profile normalization. Workbench browser
proof compares actual HTTP preparation with the UI producer on the same project/source,
then checks stored variables, assignments, replay, source links and real workflow output.

Authentic predecessor plans were generated by retained PC2 binaries whose type lacks
ExecutableDefinitions. Committed provenance records raw SHA/typed hashes. Full native
subprocess calls repeat in independent scopes and assert exact run/plan, assignments,
persisted child links and no replacement. The populated upgrade compares complete
plan/state/step/assignment rows. The browser opens the old run across different OS PIDs
and proves isolation after same-ID project recreation. Unknown/conflicting origin refuses
explicitly; public IDs/variables never become lifetime authority. The adjacent artifact
bridge was audited for accepted-tree IDs and no current-authority capture; existing native
child mapping tests provide its execution floor. Historical provider effects are not invented.

Role proof covers Add/ImportRole/ImportProcess after deletion, independent scopes, lost
post-COMMIT acknowledgement, operation replay, stale revision, and immutable publication,
receipt and preparation bytes. The retained native floor covers referenced-role refusal
and scope fences. Both browser import variants reopen exact defaults after disposing and
reconstructing the native host against the same database. This host restart is separate
from the new-OS-PID workflow and legacy tests.

Inspected Chromium screenshots at 1920×1080 include reimported/restarted role details,
Workbench launch review, legacy run details and maximized canvas. Fields and footer actions
are readable; overlays layer correctly; controls/minimap are unclipped. Workspace and
internal dialog scrolling remain usable. No production markup, style or asset graph changed.
Existing Fast/Parity asset evidence is retained for that unchanged graph, with fresh native
journeys for its consumers. No further renderer extraction is needed.

The hashed browser index covers 132 artifacts and 5,846 archive entries, with no exact fixture
credential occurrence. Trace review found no captured HTTP status ≥ 400. All 299 recorded
console error entries are connection-closed/refused or negotiation/fetch failures when
owned hosts are deliberately stopped with old pages still open; original failed attempts
are included. Fresh contexts assert no page errors. This is not a zero-console-error claim.

Bounded same-configuration Windows/native-PG/ProcIntegrationPC4 observations: known-origin
startup 28,082 ms, restart 12,057 ms, reentry 654/215 ms; unproven-origin startup 16,994 ms,
restart 11,113 ms, refusal 159/177 ms. These are samples, not a speedup or statistical benchmark.

### Final gate and residuals

Full tracked portability scan passes no-write enforcement with 15,380 reviewed executable
findings. Eight intentional PC4 allowances concern public definition/profile aliases;
stored identities/opaque variables stay ordinal. Canonical pending-model validation passes
without a database update. No schema or transfer guard changed. Helper/package and artifact
safety results are recorded in the machine ledger.

Shared launch/recovery and test-support changes trigger one frozen Broad Stable campaign
at `ca195c794`, using Release and the unchanged canonical filter. Product/test restore and
build passed; discovery found 17,346 cases. Relative to the earlier PC3 discovery there are
73 additions and no removals: 63 PC4 cases and 10 already present later PC3 repairs.

The original execution was interrupted by the Codex crash around 04:54 UTC on October 10.
Its 15 complete project reports contain 689 passing tests. The incomplete Components assembly
has no completed TRX, so its partial console passes contribute no accepted coverage. The
original log, completed reports and interruption manifest were retained at that point;
their raw files are now unavailable after the operator's later artifact deletion.

Recovery retained those 15 complete reports and ran the entire unfinished Components project
before continuing through unstarted Stable projects. All 19 recovery discovery lists matched their
original lists, totaling 16,657 cases. Source candidate, sibling revisions and Release binaries
were unchanged. Recovery used the same isolated PostgreSQL data directory, canonical filter,
Release configuration and serial execution. This was an explicitly composite campaign.
The recovery was also interrupted, and its raw artifacts were subsequently removed.
Its outcome remains incomplete; historical PC3 results are not current PASS.
Completion is not asserted while that gate is open.

Processes structural UI decoupling is closed for the reviewed nine renderers and active
consumers. Authoring-aware portable transfer/export and database paging for large catalogs
remain separately scoped product work. No replacement shared archive is required.
