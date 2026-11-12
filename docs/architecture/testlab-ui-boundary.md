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

## Extraction validation receipt — 2026-09-28 (historical)

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

## Corrective validation receipt — 2026-09-28 (R1, R2, C1)

The review-fixes package was executed on `components-decoupling`, starting from clean
HEAD `e5654544c34a5463579031aa16193a97fd2d0be1`. The final branch is unchanged; the final
local signed commit is the commit containing this corrective receipt. Its hash and Git
signature status are reported at delivery. No remote operation or next module is included.
The 24 reviewed source blobs matched the package's reviewed implementation without drift.
All 98 manifest entries passed checkout-normalized hash validation, and all 33 inherited
foundation files matched the original package. The historical assignment was not rerun.

R1 was reproduced through the real EditForm and responsible-party selector: retiring all
reads left the exact committed submission pending forever. Sandbox reference requests now
have an independent generation and typed wait ownership. A party edit retains the save
read-back until its own completion; the same draft, EditContext, active section, newer
unblurred text and party, and parent/child identities survive. Retry only reads; a later
explicit save writes the newer values once. Retired reference successes/errors, reset,
disposal, selection A/B/A and project A/B/A cannot settle a successor's operation.

R2 was reproduced by choosing Project None in the real form: a known saved party became
unavailable. The bounded fake catalog now separates reference existence from project
membership and resolves only an exact known saved ID. Global and omitted-option fallback,
unknown IDs, deliberately missing references, failed lookup and delayed stale lookup are
distinct. `DelayedPartyLookup` and `ReferenceFailure` expose those controlled scenarios.
The production session's existing global fallback is covered by a new positive owner-port
test; its implementation is unchanged.

C1 now gives Unknown the title **Test plan save outcome unknown** at the real page's
notification switch, preserving Error severity, recovery detail and replay lock. The
four-outcome page/session test uses a narrow controlled owner, observes the actual
notification service and confirms that refresh/project changes cannot invent certainty
or cause another write. Real PostgreSQL behavior remains separately proved below.

Implementation changes are confined to sandbox `TestLabScenarioWorkspace.cs` and
`TestLabScenarioStore.cs`, plus the three-line Unknown branch in module
`Pages/TestLabPage.razor`. Test changes are `TestLabSandboxReviewTests.cs`,
`TestLabSurfaceTests.cs`, `TestLabSessionTests.cs`, `TestLabNotificationTests.cs` and
`TestLabBrowserTests.cs`. Documentation changes are this record, `docs/testing.md` and
the sandbox README. The renderer, draft/submission contracts, production session/owner,
persistence, DI, project references, build settings and S0 shell are source-equivalent
to the start HEAD. The UI README therefore needs no correction.

Current environment: SDK 10.0.303, net10.0, xUnit/VSTest, Playwright 1.55 Chromium at
1600 × 1000, `TestLabProof` outputs and default source dependency mode. The clean,
read-only sibling HEADs are unchanged from the extraction receipt: Components
`f258ab6a959a97fa16c01d0858e7dc122728a11a`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`, SharedInfo
`83e21e23bcf43d92b061a6d367ac385241d13cd3`. Code Analytics and Components MCP tools were
unavailable; local source, actual discovery and the existing transitive boundary tests
provide the impact evidence. No shared component behavior changed.

Each of these direct builds passed with zero warnings/errors using
`dotnet build <project> --configuration TestLabProof /m:1`:

- `src/Sandboxes/CanDoItAll.TestLab.UiSandbox/CanDoItAll.TestLab.UiSandbox.csproj`
- `src/UI/CanDoItAll.TestLab.UI/CanDoItAll.TestLab.UI.csproj`
- `src/Modules/CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj`
- `src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`

Fresh failing-first evidence is `artifacts/testlab-review-fixes/red-ui.trx` (expected and
discovered 2; passed 0, failed 2, skipped 0) and `red-c1.trx` (expected and discovered 4;
passed 3, failed 1, skipped 0). R1 failed its pending-settlement assertion, R2 its saved-party
options assertion, and C1 its uncertainty title assertion. The reviewer's uncompiled seeds
were guidance, not an executed test result. The two light reproductions used filter
`FullyQualifiedName~Party_change_during_real_form_readback_preserves_draft_and_settles_without_replay|FullyQualifiedName~Global_project_control_keeps_the_known_saved_party_named`;
C1 used `FullyQualifiedName~TestLabNotificationTests` in the corresponding projects below.

All current green lanes rebuilt/discovered the owning assembly before execution. Each
project directory below contains its same-named `.csproj`; these are the exact commands
with the row's project, filter and prefix substituted:

```powershell
dotnet test <project> --configuration TestLabProof --list-tests --filter '<filter>' /m:1
dotnet test <project> --configuration TestLabProof --no-build --no-restore --filter '<filter>' --logger 'trx;LogFileName=<prefix>.trx' --results-directory artifacts/testlab-review-fixes /m:1
```

| Project under `tests` | Filter | Expected / discovered / passed | Failed / skipped | Prefix |
| --- | --- | --- | --- | --- |
| `Components/CanDoItAll.TestLab.UI.Tests` | `FullyQualifiedName~CanDoItAll.Tests.Components.TestLab` | 38 / 38 / 38 | 0 / 0 | `light` |
| `Unit/CanDoItAll.TestLab.Tests` | `FullyQualifiedName~TestLabSessionTests` | 29 / 29 / 29 | 0 / 0 | `session` |
| `Components/CanDoItAll.Tests.Components` | `FullyQualifiedName~TestLabNotificationTests\|FullyQualifiedName~TestLabReconciliationTests` | 7 / 7 / 7 | 0 / 0 | `host` |
| `Playwright/CanDoItAll.Tests.Playwright` | `FullyQualifiedName~TestLabBrowserTests` | 3 / 3 / 3 | 0 / 0 | `browser` |

These are 77 distinct current cases. The new files are discovered in their existing owning
projects; the light project remains selected by Components/Stable and all current component
CI shards. There is no new assembly, solution or CI membership change. Existing warnings
in broad test dependencies are separate from the clean direct production builds.

Browser proof exercised the actual selector during the exact committed read-back, observed
Pending then Saved for that plan identity, retained the focused newer title and party,
enabled submit and exactly one fake commit. It also checked the global party name and
intentional missing/failure states. Both existing production journeys ran with the real
shell, aggregate identity, held read-back and postcommit Activity failure/read-only recovery.
Fresh desktop captures were inspected in `output/playwright/testlab-ui`, including
`sandbox-party-readback.png`, `sandbox-global-party.png`, `sandbox-missing-party.png`,
`production-retained-input.png` and `production-committed-warning.png`. Browser console,
page and asset checks passed; the only server failure was the intentionally injected
postcommit Activity failure, with no shell disposal exception.

The backend-free sandbox started with its existing Parity stylesheet, fonts and scripts.
All three light dependency/contract boundary tests passed. References and watch inputs
are unchanged, so the earlier 137/7 project-graph inventory and comparative measurements
remain historical rather than being presented as new measurements. A fresh
`dotnet watch --non-interactive --project src/Sandboxes/CanDoItAll.TestLab.UiSandbox --configuration TestLabProof --no-launch-profile -- --urls <owned-loopback-url>`
Razor edit-to-visible smoke passed in 2,899.665 ms (one local sample, restored caches;
not a benchmark or Web speedup claim). The source was restored byte for byte and the
owned watch process stopped. `watch-smoke.json`, its log and screenshot retain this proof.

The owned `postgres:18.6-alpine` container `cda-testlab-review-3c068a79` used
`127.0.0.1:53685`, server version 180006, and existing isolated `WAL_LOG` database leasing.
External browser base URLs were unset and child-host/test configurations matched.
Cleanup verified zero remaining fixture databases and proof host/watch processes, checked
the exact container ID/name/task label before stopping it, and confirmed automatic removal.
The ordinary app/database on port 5032 and unrelated processes were untouched.

Portability tooling self-tests passed 6/6 and artifact-secret tooling self-tests 4/4.
The complete scan covered 6,732 files and 31,863 observations. Final enforcement without
`--write-baseline` passed with all 15,122 reviewed executable-source findings unchanged;
no baseline refresh was needed. Documentation evidence and canonical Markdown checks,
run with `./tools/Validation/Test-DocumentationEvidence.ps1` and
`./tools/Validation/Test-Documentation.ps1`, passed 9 cases and 252 maintained files.
`git diff --cached --check` passed. Raw discovery/build/run logs, TRX, package/entry checks
and cleanup provenance remain in ignored `artifacts/testlab-review-fixes`.

The Behavioral entry and closure review found no unresolved R1/R2/C1 requirement or new
dependency/composition boundary. The earlier 115-case extraction/S0 receipt is historical.
Full Stable, cross-platform, owner/admission integration and shell/Collaboration reruns were
not claimed for this correction: their production contracts, session/persistence, shared
composition and S0 implementation did not change, so their expansion triggers did not apply.
