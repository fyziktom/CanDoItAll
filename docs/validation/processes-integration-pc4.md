# Processes integration PC4 validation

Status: **IN PROGRESS — implementation and focused proof recorded; fresh Stable recovery running after artifact loss**.
Tested source: `ca195c794349d4e4eb653bacb7605ed83df86c4b`.
The [machine record](processes-integration-pc4.json) contains exact test identities,
commands, source candidates, raw failures and acceptance dispositions. The
[architecture record](../architecture/processes-integration-pc4.md) describes the corrections.

On October 10 the operator removed `.artifacts` after further crashes. The prior native
test server and runner are gone. The maintained machine record retains the reported
focused results and 28 completed component/UI project summaries (4,057 passes), but the
referenced raw TRXs, logs and recovery scripts are no longer available. The last observed
integration count was 1,462 passing console rows, with no complete integration report;
those partial rows do not close the gate. Browser files outside `.artifacts` still exist.
Raw-artifact availability is recorded separately from the outcomes observed before deletion.
The resumed Stable campaign reuses the existing PC4 Docker fixture after cleanup and
tool restoration; it does not provision another database container.

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
Windows free space remains about 34.8 GiB. Reclaimed Docker filesystem space is therefore
not reported as reclaimed Windows disk space. Offline administrator compaction, with its
service interruption, was not performed. Exact removal and protection receipts are under
`.artifacts/pc4-recovery-20261010` and summarized in the machine record.

The cleanup policy is in [Testing](../testing.md#bundle-resource-cleanup) and root agent
instructions, signed at `640fece824cfdf1f6c5985f6184c7eec199b7a9d`. Documentation validation
passed for 407 maintained Markdown files and portability enforcement passed with 15,380
reviewed executable-source findings unchanged for that policy change.

## Source and signatures

Entry was clean `components-decoupling` at `247e72a2f1caccc8b3cc720291eb4094fdb8e9ff`,
tree `9aef76a60d42631efd4c45575d48a40ba1b43740`. Work used the current checkout without
reset, rebase, stash, history rewrite, push, merge or deployment. Historical handoffs
and the existing shared bundle remain unchanged: **NO_CHANGE**.

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

Production last changed at `e9472752`; later checkpoints change tests or documentation. A report-only
commit references the tested source, avoiding a circular self-hash.

## Isolated resources and builds

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

## Focused proof and replacement sets

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

## Native behavior and rendered review

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

## Final gate and residuals

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
