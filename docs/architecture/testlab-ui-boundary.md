# TestLab UI boundary

Status: implemented. Validation receipts and measurement limits are recorded below.

## Decision and ownership

The existing `/test-lab` page combines rendering, reference reads and save reconciliation.
Its four sections form one workspace, so a cohesive workspace view is the appropriate
seam. The module retains the route and a per-page session. A narrow owner port adapts
TestLabService, the Projects admission query and the party bridge; the renderer receives
only bounded models and origin-bound intents. This is a real substitution boundary for
controlled tests, not a service bag.

Stable TestLab DTOs and status values move to TestLab.Contracts, preserving namespaces,
defaults and serialization. Projects.Contracts remains the authority for the exact
profile/project/lifetime admission. EF models, transaction and child synchronization,
search/activity effects, projections and transfer ownership remain in the TestLab module.
The UI and standalone sandbox depend on light contracts and BaseLib, without backend
implementations. Keeping DTOs in the mixed EF assembly would preserve the expensive edge;
an HTTP rewrite or general state framework would add unrelated complexity.

Draft and EditContext share one editing lifetime. Repeated route intent, section changes
and list filters preserve that lifetime. Explicit selection, New and Reset retire it.
Changing project advances target intent even for A/B/A. Read lanes fence every completion
and own cancellation resources until they unwind. Writes use immutable deep submissions
and per-origin admission. Reconciliation pairs original row objects with submitted rows,
then durable returned IDs, and compares fields with submitted values. Raw input and newer
edits survive. Known commits retain IDs before any secondary reads; recovery never replays
the write. Unknown outcomes retain the submission and require explicit review/reset
before a new submission. A project change cannot unlock it, and an earlier successful
receipt cannot resolve a later refused or uncertain write. A refresh already in flight
also checks that its commit receipt is still current after every suspension.

S0 is a bounded repair to ConversationShellHost: observe initialization, capture its token
while alive, stop the loop after retirement, check queued callbacks inside the dispatcher,
and release resources after outstanding initialization without blocking disposal on a
noncooperative contributor. No contributor runtime is redesigned.

## Proof plan and provenance

Starting branch: `components-decoupling`; HEAD:
`4a42bd2a51d4e219bbf78bb3af5e695fc215d0b8`; clean worktree.
Source-mode sibling dependencies and isolated `TestLabProof` project outputs are used.
Code Analytics and Components MCP tools are unavailable; source, evaluated metadata and
actual discovery provide the local evidence. Siblings remain read-only.

Before extraction, capture the Web graph/watch and real edit-to-visible baseline. First
reproduce S0 with controlled contributors and run existing shell and production
Collaboration journeys. Then prove TestLab state races, real forms and sandbox scenarios
independently, retain real owner/postcommit/admission/cross-module baselines, and exercise
production and sandbox in Chromium at 1600 × 1000 with isolated PostgreSQL 18.

Build changed projects and affected consumers directly. Every new filter requires a
source-derived case count and matching discovery. Finish with evaluated graph and asset
proof, measured development loops, reviewed portability enforcement without write mode,
documentation checks and whitespace validation. Adding the two feature projects to the
product solution is membership-only; no root build policy, shared persistence or
cross-cutting DI behavior changes are intended. Reassess broader gates if that changes.

See [UI component seams](ui-component-seams.md) and [Testing](../testing.md) for canonical
rules. Current proof artifacts belong in ignored `artifacts/testlab-ui`, not this record.

## Implemented boundary

| Location | Responsibility |
| --- | --- |
| `CanDoItAll.Modules.TestLab.Contracts` | Original DTO namespace/defaults/status values and JSON shape; public minimal child identity; exact Projects.Contracts admission |
| `CanDoItAll.TestLab.UI` | Complete BaseLib renderer, typed workspace view/state, one draft/EditContext, raw input validation and pure submission/row reconciliation |
| TestLab module `Workspace` | Per-page route/read/write orchestration, independent generation-fenced read lanes, production adapter and explicit write outcomes |
| TestLab module owner/persistence | Existing transaction, project locks/admission, four-table synchronization, committed ID updates, search/activity, projections and transfer |
| `CanDoItAll.TestLab.UiSandbox` | Real renderer with independent fake storage, controlled operation completion and scenario disposal |
| Owning test projects | Separate light renderer tests, production session tests, real owner/host tests and browser proof |

The stored-binding exception is now a typed `InvalidOperationException` subtype so the
adapter can distinguish that known precommit refusal without parsing exception text.
Other owner persistence behavior is unchanged. No schema, migration, transfer format,
durable idempotency protocol or API redesign was introduced. `ITestPlanChildEditor` became
a public three-line identity contract because both the light editors and backend generic
synchronizer require it; no backend helper moved into the contracts assembly.

The editor remains editable during a pending save while its submit action is disabled.
Rendered callbacks carry the draft and target version. Submitted rows are paired with
their original objects and returned durable IDs, never with the current collection's
positions or display text. Accepted values replace only unchanged submitted fields;
new rows, removed/replaced rows, newer text, raw invalid timestamps, filters and tabs are
retained. Known commits keep IDs before secondary reads and allow a later explicit save.
Unknown writes remain locked through refresh and project changes until explicit draft
retirement. No similarly named list row is treated as proof of a successful create.

Project/party options contain only identity, display name and (for projects) the original
immutable admission. A stored party absent from the current list gets a fenced fallback
lookup or an explicit unavailable choice. Historical project bindings remain visible;
the current-project filter requires the same lifetime. Rebinding uses an explicit user
selection, including the **Use current project** action for a recreated public ID.

Input events update draft values before blur. The timestamp input preserves raw text
through tab changes, validates an explicit round-trip ISO timestamp and normalizes its
instant to UTC. PostgreSQL's existing microsecond precision remains unchanged. The
prerendered form is disabled until interactive readiness, avoiding edits lost during
the initial server-to-interactive handoff.

## Validation receipt — 2026-09-28

The branch remains `components-decoupling`. S0 is signed commit `7446c940e` (signature
verified by Git); the TestLab implementation is the signed commit containing this record.
No remote operation or next-module work is part of this closure.

Environment: Windows 10.0.26200, .NET SDK 10.0.303, MSBuild 18.6.14, runtime 10.0.12;
Intel Core i9-13900H (14 cores/20 logical processors), approximately 64 GiB RAM.
Chromium through Playwright 1.55, 1600 × 1000. All project commands used `TestLabProof`,
default source dependency mode and restored local caches. Sibling revisions were:

- Components: `f258ab6a959a97fa16c01d0858e7dc122728a11a`.
- FileTools: `3a080ecd31068a77c1e1bd639f7a78e21c93db85`.
- SharedInfo: `83e21e23bcf43d92b061a6d367ac385241d13cd3`.

PostgreSQL was the owned disposable `postgres:18.6-alpine` container
`cda-testlab-proof-40d0086d`, loopback endpoint `127.0.0.1:58025`,
`server_version_num=180006`. Fixture databases used the existing isolated leasing and
`WAL_LOG` creation strategy. External browser base URLs were unset. The user's app,
ordinary database, sibling source and unrelated processes were not changed.

Direct builds used `dotnet build <project> --configuration TestLabProof /m:1` for the
shell, TestLab.Contracts, TestLab.UI, TestLab module, TestLab sandbox, Workbench module,
Composition and Web. All eight completed with zero warnings/errors. S0 also separately
built its AgentFramework and SimpleChats consumers. Owning test projects were rebuilt
by discovery before execution; existing warnings in the broad test graph are separate
from the clean direct production builds.

Every row below used source-derived expected case counts and matching successful
discovery before execution. Commands, raw discovery output and TRX files are retained
under `artifacts/testlab-ui` with the indicated prefix:

```powershell
dotnet test <project> --configuration TestLabProof --list-tests --filter '<filter>' /m:1
dotnet test <project> --configuration TestLabProof --no-build --no-restore --filter '<filter>' --logger 'trx;LogFileName=<prefix>.trx' --results-directory artifacts/testlab-ui /m:1
```

| Project under `tests` | Filter | Expected / discovered / passed | Failed / skipped | Prefix |
| --- | --- | --- | --- | --- |
| `Unit/CanDoItAll.TestLab.Tests` | `FullyQualifiedName~TestLabSessionTests` | 28 / 28 / 28 | 0 / 0 | `closure-session` |
| `Components/CanDoItAll.TestLab.UI.Tests` | `FullyQualifiedName~CanDoItAll.Tests.Components.TestLab` | 20 / 20 / 20 | 0 / 0 | `closure-ui` |
| `Components/CanDoItAll.Tests.Components` | `FullyQualifiedName~TestLabReconciliationTests\|FullyQualifiedName~OwnerPostcommitPageTests\|FullyQualifiedName~ConversationShellHostTests\|FullyQualifiedName~FloatingAgentChatHostLifecycleTests` | 28 / 28 / 28 | 0 / 0 | `closure-host` |
| `Integration/CanDoItAll.Tests.Integration` | Owner/consumer filter below | 35 / 35 / 35 | 0 / 0 | `testlab-owners` |
| `Playwright/CanDoItAll.Tests.Playwright` | `FullyQualifiedName~TestLabBrowserTests\|FullyQualifiedName~CollaborationBrowserTests` | 4 / 4 / 4 | 0 / 0 | `closure-browser` |

Each project directory in that table contains its same-named `.csproj`. The exact bounded
Integration filter was:

```text
FullyQualifiedName~TestLabOwnerPersistenceTests|FullyQualifiedName~ResourceTestLabAdmissionIntegrationTests|FullyQualifiedName~OwnerPostcommitPersistenceTests|FullyQualifiedName~Responsible_party_links_round_trip_for_resources_and_test_lab|FullyQualifiedName~Owner_projections_preserve_fields_order_bindings_layout_and_plan_timestamps|FullyQualifiedName~Owner_scope_facts_deny_foreign_projects_and_preserve_missing_and_canonical_precedence|FullyQualifiedName~Assembly_mutation_reads_preserve_the_outer_serializable_snapshot_and_transaction_ownership|FullyQualifiedName~Static_test_plan_projection_precedes_process_child_composition|FullyQualifiedName~RuntimeGateway_CreateAssetAsync_replays_duplicate_idempotency_key_without_duplicate_node
```

That lane covers owner schema/restart/profile parity, actual project retirement/admission,
CRM/HR references, Workbench projections and serializable mutation reads, automatic TestLab
placement and a bounded Project Structure runtime-gateway consumer. The current large
ProjectStructureAgent test file otherwise refers to TestPlan only in its generic card-size
helper; its unrelated provider/process/live cases were not claimed as TestLab proof.

The 115 distinct cases cover the supplied matrix through controlled session requests,
real EditForm/input events, fake-store identity, actual PostgreSQL writes and the real Web
route. Browser proof creates and reopens all four sections, checks parent/child identities
and owner values, filters/global/history/missing navigation, retains unblurred input across
a held read-back, and recovers a real postcommit Activity failure without a second write.
The test-only Kestrel fixture retains the real shell and production registrations; only
the existing owner test probes add controlled delays/failures. It exposes no HTTP control
endpoints. Sandbox proof loads the shipped controls, styles, icon font and scripts without
database configuration. Browser/server/asset errors and owned-host shutdown are checked.
Screenshots were captured and inspected in `output/playwright/testlab-ui`.

Failing-first artifacts are retained separately: the old TestLab page failed all three
reconciliation/duplicate-submit cases; S0 reproduced disposed-token access and queued
dispatch after retirement. During construction, incorrect test selectors were corrected;
browser proof also exposed and led to repairs for non-UTC input persistence and interactive
readiness. Final review also reproduced four controlled cases where an older in-flight
refresh could mislabel a later refused/unknown save; refresh now checks its specific
commit receipt and the final session lane passes all four. Those failed attempts are not
counted as passing proof. Final counts above are
the closure runs. No Linux/macOS, full Stable, live provider or release gate was claimed:
the solution edit is additive membership, the new CI entries select the light lane, and
there is no root build-policy, shared persistence or cross-cutting DI change.

## Evaluated graph and measured development loop

Evaluated restore metadata (`project.assets.json` and NuGet dgspec project-reference
graphs), complete assembly traversal and actual host startup agree on the light closure.
The sandbox contains itself, TestLab.UI, TestLab.Contracts, Projects.Contracts,
SharedKernel and BaseLib/Common. Its only package is the SDK static-assets package;
there are no native runtime assets. Both graphs have no unresolved edge or cycle.

| Observation | Original Web | Changed Web | TestLab sandbox |
| --- | ---: | ---: | ---: |
| Evaluated projects including root | 135 | 137 | 7 |
| Transitive packages | 140 | 140 | 1 |
| Native runtime assets | 30 | 30 | 0 |
| `dotnet watch --list` paths | 4,395 | 4,406 | 285 |
| Startup to first form render, one sample (s) | 48.541 | 51.485 | 8.020 |
| Razor edit-to-visible median (s) | 2.924 | 5.946 | 1.904 |
| C# expression edit-to-visible median (s) | 0.889 | 3.909 | 1.893 |
| Application CSS edit-to-visible median (s) | Not captured | 0.151 | 0.117 |

Individual edit samples in milliseconds, in execution order:

| Kind | Original Web | Changed Web | Sandbox |
| --- | --- | --- | --- |
| Razor | 3922.634, 2915.274, 2923.976 | 5971.025, 5946.232, 4921.362 | 5926.042, 1903.185, 1903.607 |
| C# expression | 1908.995, 888.725, 887.628 | 3909.282, 3924.747, 1884.469 | 1891.531, 1893.249, 1907.316 |
| Application CSS | Not captured | 224.271, 150.865, 115.616 | 231.541, 98.831, 116.574 |

The C# sample changes the editor-title expression in the Razor source. The CSS sample
changes the actual input border through the linked application stylesheet, waits for
computed browser style and restores the original bytes. Logs show managed hot reload
for the source edits and static-asset refresh for CSS; no required watcher was disabled.
The raw sandbox `--list` output lists source and BaseLib assets; the linked Web CSS is
also watched by the static-asset runtime, confirmed by its actual file-update/refresh
logs and browser observations. No backend source appears in the sandbox watch inventory.

Inventory command: `dotnet watch --list --project <host> --configuration TestLabProof`.
Measured launch: `dotnet watch --non-interactive --project <host> --configuration TestLabProof --no-launch-profile -- --urls <owned-loopback-url>`.
The hosts were `src/App/CanDoItAll.Web` and
`src/Sandboxes/CanDoItAll.TestLab.UiSandbox`. Ignored `*-transitive-graph.json`,
`*-watch.txt`, `*-watch-runtime.log` and `*-loop.json` retain the samples and provenance.
All measurement-only source/CSS changes were restored and owned watch processes stopped.

These are local workflow comparisons with restored caches, one startup per host and three
edit samples per measured kind. They are not cold-start benchmarks or universal speedup
claims. The full Web graph remains broad and its measured edits were slower in this run;
the extraction's benefit is the real lightweight sandbox. No feature-owned JavaScript or
scoped CSS exists to measure separately. Original-Web CSS latency was not captured.

## Static gates and cleanup

The portability scanner/enforcer and artifact-secret scanner self-tests passed. A full
scan reviewed the removed page findings and the new case-insensitive UI text filters and
README command fences. S0 fixed the pre-existing baseline metadata count from 15,149 to
15,119: all existing entries already matched that scan, so no allowance was hidden.
The final full scan covered 6,687 files and found 31,815 total observations. Review accepted
10 additions (six README command-fence matches and four text-filter matches) and removed
seven stale page-filter allowances. No filesystem comparison or platform assumption was
introduced. The inspected baseline now has 15,122 allowances, and final enforcement
without `--write-baseline` passed. Documentation evidence checks passed all nine cases;
the maintained Markdown validator passed 252 files. `git diff --check` also passed.

All owned browser/watch processes and leased databases are disposed. Final cleanup found
zero matching test/watch processes and zero remaining fixture databases. The PostgreSQL
container's ID, name and task label were verified before stopping it; its removal was
confirmed afterward. Raw runtime logs, TRX and temporary measurement helpers remain in
ignored evidence directories; maintained documentation and source are committed locally.
