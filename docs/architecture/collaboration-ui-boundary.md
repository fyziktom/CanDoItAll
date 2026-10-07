# Collaboration UI boundary

## Decision and scope

The Collaboration workspace keeps its routed host and application owner in
`CanDoItAll.Modules.Collaboration`. `CanDoItAll.Collaboration.UI` owns the complete
workspace rendering over a cohesive view contract. The host owns a per-page session,
not a circuit-scoped workspace service. The sandbox implements the same contract with
deterministic scenario state and renders the same components.

Plain requests, snapshots, annotated editor models and the six existing enums move to
`CanDoItAll.Modules.Collaboration.Contracts`, retaining their CLR namespace, member
names and enum values. Persistence records, mapping configurations, the bounded context,
transfer participant and activity mirroring remain in the module. No schema migration,
HTTP control plane or optimistic concurrency protocol is introduced.

The view contract is justified by the two live forms and their shared selection. A
presentation copy per render would duplicate mutable drafts; a generic command bus or
the CRM / HR mutation framework would add unnecessary machinery. A narrow module-owned
owner port enables scripted lifetime tests while production resolves the existing service.

## Lifetime and recovery

Desired selection distinguishes initial default, explicit identity and deliberately empty
filtered selection. Request generations fence success, errors and cleanup, including
A → B → A and disposal. Each read cancels its predecessor and disposes its own source
after unwinding. A failed refresh preserves only the same selection's accepted snapshot.

The host owns draft instances and their `EditContext`. Same-target refreshes and section
changes preserve both. Switching targets explicitly discards the old reply draft.
Automatic selection retains a reply with text, modified fields or an admitted/unknown
submission on its original target, even when that target leaves the active list. A visible
status explains the retained detail. Reply input updates its draft on each input event,
including before blur. Explicit thread selection still discards the reply; Clear preserves
the chosen message kind, clears modified state, and permits the next Refresh to realign.
A clean empty filter retains a genuinely empty selection. No text is rebound to another
thread and no owner unread semantics change.
Quick-create is independent. Each admitted form disables its fields and reset actions;
the owner-side gate also rejects duplicate or retired callbacks. Submissions capture
immutable values before dispatch. Local replies retain `MarkAsUnread: false`.

Successful writes retain their identity even if reconciliation fails. Retry performs only
a read. Unexpected exceptions from a dispatched command leave its submission locked with
an unknown-outcome warning; the operator must inspect persisted state before explicitly
starting a new draft. Observer exceptions after a save are logged and isolated individually.

The sandbox separates its stored results from the accepted view. Admitted writes survive
target retirement; only current lifetimes may reset editors or publish completion.
Effective tab/filter changes retire create-navigation effects. Delayed operations complete
one at a time, and delayed reconciliation exposes the same successor-edit interval as
production. Scenario disposal abandons that isolated store, releases all owned waits and
suppresses further writes/callbacks. These are local scenario semantics, not a production
cancellation or durable idempotency protocol.

## Validation contract

Proof covers scripted read/command races, actual forms and validation, the real routed host,
PostgreSQL owner writes and shell badge updates, and production and sandbox browser journeys.
The dependency guard traverses relevant references without ignoring unresolved edges and
checks public types. Evaluated project/package and watch graphs complement assembly guards.
The closure evidence below distinguishes measured proof from unavailable inputs.

The primary surface remains the list/detail workspace; compact stats and filters support
it. Quick-create stays inline for compatibility. The workspace scaffold owns scrolling;
the sandbox's compact scenario toolbar remains visible. No feature
CSS or JavaScript existed at the starting revision; Parity uses production CSS plus actual
BaseLib assets. No Fast mode is claimed.

## Starting evidence

Started on `components-decoupling`, HEAD `714e42706904e796e5e628f30457ca40370c7d4c`,
with no pre-existing changes. SDK: 10.0.303, Windows x64, source dependency mode.
Components: `f258ab6a959a97fa16c01d0858e7dc122728a11a`; FileTools:
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`; SharedInfo:
`83e21e23bcf43d92b061a6d367ac385241d13cd3`. Collaboration and the relevant shell
sources have no drift from the handoff's provenance revision. The original Web watch
list contains 4,384 entries. Code Analytics and Components MCP were unavailable; source
inspection and evaluated metadata are the fallback evidence, not successful MCP proof.

## Original extraction evidence — 2026-09-28 (historical)

This section records the original execution before the user committed the extraction as
`e55a780b36e75db39a05b7400408d8267d0f32d5`. The review-fix receipt below supersedes its
current-status statements, without changing the provenance of these earlier runs.

The final checkout remains on `components-decoupling` at
`714e42706904e796e5e628f30457ca40370c7d4c`, with the implementation uncommitted. No signed
commits, pushes, PRs, package publications or releases were created. Only Collaboration
and its necessary shell, solution, test and documentation consumers changed.

Intentional corrections cover stale read/command completions, duplicate submissions,
empty unread selection, draft validation lifetime, failed reads versus empty data,
post-save observer failures, shell refresh faults and unsafe context navigation. Target
changes discard the reply draft; same-target reads and section changes preserve it.
An empty filter retains an existing stale-data warning. An old reply reconciliation cannot
relabel a newer saved reply. User and automation unread semantics remain owner-defined.

The moved contracts retain their source namespace, annotations, member shapes, enum names
and numeric values. The assembly identity changes; no binary-only consumer requiring type
forwarding was identified. Composition still discovers the owning module's route and the
complete schema still uses the original four mapping configurations. No HTTP contract,
database migration, token enforcement or API snapshot changed. Ambient transactions retain
their existing semantics; these UI calls do not introduce one.

### Builds and test commands

All commands ran from the repository root using SDK 10.0.303, runtime 10.0.12, Windows x64
10.0.26200, source dependency mode and Parity assets. The machine has an Intel i9-13900H
(14 cores / 20 logical processors) and approximately 63.7 GiB RAM. PostgreSQL was an owned,
ephemeral PostgreSQL 18.6 container at loopback port 63131, using the documented
`CANDOITALL_TESTS_POSTGRES_CONNECTION` and `WAL_LOG` create strategy. Tests created their own
databases. No production database, live provider, process run or scheduler job was used.

A running developer app locked ordinary Release output. The initial Release integration
discovery therefore failed before tests ran. The final project commands use the separate
`CollaborationProof` configuration; the app and its sessions were left running. No new
configuration was persisted in repository build settings.

Each of these direct builds passed with zero warnings and errors:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Collaboration.Contracts/CanDoItAll.Modules.Collaboration.Contracts.csproj --configuration CollaborationProof --no-restore /m:1
dotnet build src/UI/CanDoItAll.Collaboration.UI/CanDoItAll.Collaboration.UI.csproj --configuration CollaborationProof --no-restore /m:1
dotnet build src/Modules/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj --configuration CollaborationProof --no-restore /m:1
dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration CollaborationProof --no-restore /m:1
dotnet build src/Sandboxes/CanDoItAll.Collaboration.UiSandbox/CanDoItAll.Collaboration.UiSandbox.csproj --configuration CollaborationProof --no-restore /m:1
```

Every row used the following discovery/execution pair, substituting the exact project and
filter below. Source-derived expected cases were checked against discovery before execution.
The browser child hosts used `CANDOITALL_TEST_CONFIGURATION=CollaborationProof`; an external
browser base URL was unset so the fixture owned its application and port.

```powershell
dotnet test $project --configuration CollaborationProof --list-tests --filter $filter /m:1
dotnet test $project --configuration CollaborationProof --no-build --no-restore --filter $filter --logger "trx;LogFileName=$name.trx" --results-directory artifacts/collaboration-ui /m:1
```

| Project | Exact filter | Expected / discovered / passed |
|---|---|---|
| `tests/Unit/CanDoItAll.Collaboration.Tests/CanDoItAll.Collaboration.Tests.csproj` | `FullyQualifiedName~CollaborationWorkspaceSessionTests` | 29 / 29 / 29 |
| `tests/Components/CanDoItAll.Collaboration.UI.Tests/CanDoItAll.Collaboration.UI.Tests.csproj` | `FullyQualifiedName~Collaboration` | 13 / 13 / 13 |
| `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | `FullyQualifiedName~CollaborationDbContextTests` | 5 / 5 / 5 |
| `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` | `FullyQualifiedName~CollaborationHostTests\|FullyQualifiedName~MainLayoutCollaborationTests` | 3 / 3 / 3 |
| `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | `FullyQualifiedName~CollaborationIntegrationTests` | 7 / 7 / 7 |
| `tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj` | `FullyQualifiedName~CollaborationBrowserTests\|FullyQualifiedName~CollaborationSandboxBrowserTests` | 2 / 2 / 2 |

Total: 59 cases, none skipped. The observer regression failed at the real PostgreSQL owner
before the repair, then passed along with the remaining owner cases. Owner proof includes
schema equivalence, restart/read-back and profile isolation. Renderer proof uses actual
BaseLib children and actual form submission, including required/length validation and a
pending submit repeated through the same form. Scripted session proof covers A → B → A,
ignored cancellation, retired callbacks, disposal, saved/read-failed outcomes and unknown
write acknowledgements.

Production Playwright creates a thread, validates required fields, checks the shell badge,
marks it read, submits a typed local reply, checks PostgreSQL owner read-back, tests unread
and Threads behavior, opens a deep link, reloads, navigates to the linked scheduler route
and back, and verifies explicit not-found. Sandbox Playwright uses the real forms for
validation, delayed save, owner refusal, saved/read-failed recovery and delayed selection.
Both use 1600 × 1000 with actual assets and inspect browser/server errors. The sandbox also
checks all three sections, unread filtering, initial viewport geometry, scrolls to the last
of 60 messages and verifies that the scenario toolbar remains visible. Scenario selection
waits for the rendered scenario marker; rows have stable thread keys so list updates retain
their identity. Screenshot review found and corrected the initial
two-scaffold layout defect; final screenshots were inspected.

The narrow solution changes register the two new production projects and focused tests.
CI's explicit Components shard lists now include the lightweight renderer project, since
solution membership alone did not run it. The broader Stable suite, provider suites,
process suites and mobile geometry were not run; this is bounded Collaboration closure,
not a claim of release/merge or whole-repository validation.

### Graph, assets and development loop

`npm ci --prefix Tailwind` and `npm run tailwind:build` passed. Production's existing
`@source "../src"` includes the renderer and sandbox. Parity links the resulting production
CSS and actual BaseLib styles, Material Symbols fonts and browser resources. The sandbox
has an explicit missing-CSS build error and disables repository-template copying. It has
no Fast mode and no feature CSS/JS edit to measure.

Recursive restore metadata (`*.csproj.nuget.dgspec.json`) and resolved
`project.assets.json` were inspected after evaluated builds, including imported source-mode
rewrites. Every project edge resolved; native runtime assets were included in inspection.

| Host | Transitive projects including root | NuGet packages | Watch entries |
|---|---:|---:|---:|
| Original Web | not measured | not measured | 4,384 |
| Changed Web | 135 | 140 | 4,394 |
| Collaboration sandbox | 5 | 1 framework asset package | 252 |

The sandbox's complete project closure is sandbox → Collaboration.UI → Collaboration
contracts plus BaseLib → Common. Its only resolved package is
`Microsoft.AspNetCore.App.Internal.Assets/10.0.11`; it has no native runtime assets.
Assembly guards traverse all relevant feature/component edges, reject heavy or unresolved
edges and inspect public properties, method signatures and generic/element types. Framework
assemblies are the explicit traversal boundary. Full Web remains a broad watch graph.

Watch inventories used `dotnet watch --project <project> --list`. The live sandbox used:

```powershell
dotnet watch --project src/Sandboxes/CanDoItAll.Collaboration.UiSandbox --non-interactive --no-launch-profile --property:Configuration=CollaborationWatch --urls http://127.0.0.1:50441
```

In a warmed watch session, a local Playwright harness changed the actual renderer's Razor
title and C# list-description method, timing each file write until the new text appeared
without navigation. Three samples per edit type: Razor **596, 511, 549 ms** (median 549 ms);
C# **66, 66, 67 ms** (median 66 ms). Watch reported automatic hot reload for both. Browser
refresh/restart and cold-start timings were **not measured**. Probe bytes were restored in
`finally` and checked absent afterward. Preliminary timings distorted by tool scheduling
were discarded; only the local harness samples are reported.

Original and changed full-Web save-to-visible timings were **not measured**. The isolated
sandbox figures are not a measured comparative speedup. Initial Release locking prevented
the attempted baseline build, and the original source was not subsequently restored over
the working implementation to manufacture a comparison.

Local, ignored evidence is under `artifacts/collaboration-ui`: final build logs, discovery
logs, six final TRX files, graph JSON, watch inventories, edit-loop JSON and static-gate
review. Browser screenshots are under `output/playwright/collaboration`. These are local
artifacts, not a committed audit archive or reproducible machine-independent benchmark.

### Gates and remaining limitations

Portability-static scans the complete tree including new protected files. Review covered
50 added and 33 stale finding occurrences: moved enum/contracts/rendering terms, typed
escalation handling, README PowerShell examples and the intentional backslash rejection
in local-route validation. There were no new machine paths or platform-dependent runtime
operations. The baseline is refreshed only for these reviewed deltas. Scanner/enforcer
self-tests passed 6 cases and artifact-secret scanner self-tests passed 4. Documentation
evidence tests passed 9 cases; the documentation validator passed 245 maintained Markdown
files. The final complete scan covered 6,577 files with 31,631 findings and was untruncated.
After inspecting the baseline diff, final enforcement **without** `--write-baseline` passed:
**15,149 reviewed executable-source findings unchanged**, with no added or stale findings.
`git diff --check` and a separate whitespace check of all new files also passed.

```powershell
python tools/Validation/Portability/scan_portability.py --repo-root . --output artifacts/collaboration-ui/portability-scan.json
python tools/Validation/Portability/enforce_portability_baseline.py --scan artifacts/collaboration-ui/portability-scan.json --baseline tools/Validation/Portability/portability-risk-baseline.json
pwsh -NoProfile -File tools/Validation/Test-DocumentationEvidence.ps1
pwsh -NoProfile -File tools/Validation/Test-Documentation.ps1
```

The task-owned watch process, browser tab and disposable PostgreSQL container were stopped
after proof. Existing developer processes were not terminated.

The supplied standalone Collaboration assignment and repository shared v3 foundation were
read. The shared prompt, validation document and all four architecture documents were also
verified byte-for-byte against the supplied shared v3 ZIP. The referenced
`CanDoItAll_Collaboration_UI_Decoupling/prompt.md` directory,
`REVIEW_NOTES.md`, `VALIDATION_MATRIX.md` and `MODULE_SELECTION.md` were not available in the
checkout or supplied Downloads files. Their location was requested. The implementation
and proof follow the detailed standalone assignment and current repository rules, but
compliance with those missing module-specific documents is **unverified**. Code Analytics
and Components MCP proof, full-Web comparative timings and package-mode proof are also
not claimed. Existing lack of optimistic concurrency and durable operation idempotency
remains explicit owner behavior, not a feature of this extraction.

## C# Architecture Gate Result

Status: **Pass with follow-up**.

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Follow-up | The module-specific review/matrix inputs are unavailable. | Only the standalone assignment and shared v3 package were supplied. | Reconcile this implemented slice against those documents when available. |
| Limitation | Code Analytics and Components MCP were unavailable. | Source inspection, recursive evaluated graphs and real component proof replace unavailable tool results. | No fabricated MCP evidence; rerun tool-specific checks if required by later review. |

### Dependency direction

Contracts are implementation-free; UI references only contracts and the actual component
library. Production and the sandbox both consume the same renderer. Recursive graph
traversal found no project cycles or unresolved project references. The scoped owner port
aliases the actual service; production does not bypass the seam. No service locator,
nested service provider, application-wide workspace service or copied backend exists.

### Partial-class policy

The new Razor code-behind is the allowed cohesive UI use of partial classes: typed tab
mapping and pure render helpers. State/effects moved to the top-level per-page session,
not another partial file of the old page. The old routed page is now a thin host. No new
partial file extends the shell or owner service.

### Testability proof

The 29 session cases construct the session with a scripted narrow owner and no database or
Web host. The 13 renderer/boundary cases use the light graph. Real-host, PostgreSQL and
browser proof independently validate production wiring. Negative cases cover rejections,
unknown outcomes, forbidden/unresolved dependencies and stale lifetimes.

### Closure decision

The implementation and local proof support the Collaboration boundary. Review of the
missing module-specific documents and comparative full-Web timing remain explicitly open;
this record does not authorize merge, release or another module.

## Collaboration review fixes — 2026-09-28

### Checkout and scope

The complete committed review package, its original review/acceptance matrix, selection
record and shared v3 foundation were read. Execution started on `components-decoupling`
at `67413ee3a44c765cae7047576cf4be864b2ddb42`, with a clean worktree after the user's
bundle commit. Final HEAD remains that SHA; this follow-up is uncommitted, so there is no
new commit/signature to verify. No push, PR, merge, release or next-module work occurred.
The bundled input documents remain unchanged.

SDK 10.0.303, Windows x64, `UseLocalCanDoItAllLibraries=true`, Parity assets and the
`CollaborationProof` output configuration were used. Components, FileTools and SharedInfo
remain at the three sibling revisions recorded above. No source/package switch, project
reference, schema, persistence owner or scoped-owner alias changed. Both Code Analytics
and Components MCP remain unavailable; source, evaluated metadata and actual discovery
are the stated fallback, not successful MCP calls.

### Findings and red/green proof

| Finding | Correction | Current evidence |
|---|---|---|
| R1.1–R1.3 | `CollaborationReplyPolicy` retains text, modified context or locked submission on its original target during automatic realignment. The real textarea records input before blur. A status explains the retained detail. | Six new session cases failed before the fix (empty/B remaining, successor/already-dirty, Mark-read/manual refresh). All six now pass within 35 session cases. Four real-form cases exercise both `change` and `input` without blur; all pass with exact target, draft/context, text, navigation and owner-call assertions. |
| R1.4–R1.5 | Explicit navigation still resets the reply. Clear preserves kind and releases modified state; the next read can select B or remain genuinely empty. Existing refusal/unknown and read-failure semantics remain. | Session regressions for A→B→A, retired callbacks, validation, empty selection, same-route echoes, saved warnings and unknown results pass. Production browser retains a dirty reply through Mark-read, then verifies explicit discard without persisting it. |
| R2a | Admitted replies update the scenario store for captured A even after navigation; current-lifetime checks fence only projection/editor effects. Delayed operations release independently in FIFO order. | Both retired-write regressions failed before the fix; both now pass, including exactly one stored older reply and a still-pending successor with unchanged context/text/gate/status. Disposal releases all waits without replacement callbacks, including nested section realignment. |
| R2b | Effective section/filter changes advance selection intent; no-op setters do not. Retired create navigation preserves the newer view and returned identity; Refresh reveals the stored result. Read generations are separate from section intent. | Two effective-transition cases failed before the fix; two no-op controls passed. All four now pass. Delayed target/section and delayed reconciliation cases also pass in the 25-case lightweight lane. |
| R3 | The surface exposes accepted selected ID/unread state. Browser Mark-read requires that exact previously-unread ID to become read with `data-phase=ready`, before owner read-back. | The delayed real-form test shows the old disabled button and Ready marker already true during admission, while completion remains false through the pending write and read. It passes only after accepting the target snapshot. Production journey passes with the real owner. |
| C1 | Added positive production escalation creation, selection and durable reload, plus dirty-target navigation and stored-message checks. | Production browser creates both item kinds through real forms. Assertions use task-owned subjects/IDs, and verify escalation kind/body and absence of the discarded reply through the owner. |

The initial real-form red run selected five cases: two lost the changed draft, two had no
`input` handler, and one lacked the semantic accepted-state observation. The scenario red
run selected seven cases: four failed and three no-op/disposal controls passed. Red evidence
is `r1-red.trx`, `r2-red.trx` and `renderer-red-complete.trx` under the new ignored
`artifacts/collaboration-review` directory. These are current reproductions, not inferred
passes from the historical 59-case summary. An intermediate post-fix form assertion was
corrected to inspect the textarea's rendered value rather than its text-content node.

### Commands and final focused results

All five direct builds passed with zero warnings/errors. For each project listed in the
original build block above, the current command was:

```powershell
dotnet build $project --configuration CollaborationProof --no-restore /m:1
```

The projects were Collaboration.Contracts, Collaboration.UI, Modules.Collaboration,
Collaboration.UiSandbox and Web; current logs are `build-contracts.log`, `build-ui.log`,
`build-module.log`, `build-sandbox.log` and `build-web.log` in `artifacts/collaboration-review`.
Every test row used these exact discovery/execution commands, with the values below:

```powershell
dotnet test $project --configuration CollaborationProof --list-tests --filter $filter /m:1
dotnet test $project --configuration CollaborationProof --no-build --no-restore --filter $filter --logger "trx;LogFileName=$name.trx" --results-directory artifacts/collaboration-review /m:1
```

| Project | Exact filter | Name | Expected / discovered / passed / failed / skipped |
|---|---|---|---|
| `tests/Unit/CanDoItAll.Collaboration.Tests/CanDoItAll.Collaboration.Tests.csproj` | `FullyQualifiedName~CollaborationWorkspaceSessionTests` | `session-final` | 35 / 35 / 35 / 0 / 0 |
| `tests/Components/CanDoItAll.Collaboration.UI.Tests/CanDoItAll.Collaboration.UI.Tests.csproj` | `FullyQualifiedName~Collaboration` | `renderer-sandbox-verified` | 25 / 25 / 25 / 0 / 0 |
| `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` | `FullyQualifiedName~CollaborationHostTests\|FullyQualifiedName~MainLayoutCollaborationTests\|FullyQualifiedName~CollaborationReconciliationTests` | `host-final-verified` | 8 / 8 / 8 / 0 / 0 |
| `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | `FullyQualifiedName~CollaborationDbContextTests` | `context-final` | 5 / 5 / 5 / 0 / 0 |
| `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | `FullyQualifiedName~CollaborationIntegrationTests` | `owner-final` | 7 / 7 / 7 / 0 / 0 |
| `tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj` | `FullyQualifiedName~CollaborationBrowserTests\|FullyQualifiedName~CollaborationSandboxBrowserTests` | `browser-diagnostic-pair` | 2 / 2 / 2 / 0 / 0 |

Total final focused execution: **82 passed, zero failed/skipped**. Existing analyzer warnings
in unrelated Unit-project tests remain; they are not production-build warnings or failures.
New cases use existing test projects, already present in test solutions/CI shards; no new
project or CI wiring is needed.

The owned PostgreSQL container used loopback `127.0.0.1:54510`, server version `180006`,
the documented test connection setting and `WAL_LOG`. The browser child configuration was
`CANDOITALL_TEST_CONFIGURATION=CollaborationProof`, with external base URL unset and
headless runtime presentation enabled. Both browser hosts owned fresh ports/processes;
the sandbox removed the database setting. The ordinary application/database on 5032 and
existing watch/MCP sessions were not used or stopped.

### Original acceptance matrix reconciliation

| Original obligation | Status | Concrete proof / applicability |
|---|---|---|
| Actual checkout, dependencies and instructions | passed | Actual start/final SHA and sibling revisions above; clean start; full bundled foundation read; source and evaluated references inspected. MCP-specific execution is not run because tools are unavailable. |
| Full renderer closure | passed | Real BaseLib descendants in `CollaborationSurfaceTests`; both real browser hosts exercise lists, forms, detail, badges and three sections. No heavy child is replaced with a stub. |
| Route and selection | passed | Session route-echo, explicit-missing, initial-section and A→B→A tests; production notification/escalation deep links and reload. |
| Filter-empty selection | passed | Session clean-empty/null-fallback and six retention regressions; scenario empty/B cases; real-host empty selection and browser task-owned unread/all/Threads assertions. |
| Request lifetime | passed | Controlled ignored cancellation, old success/error/finally, retired commands and disposal in session tests; independent sandbox writes/read disposal. The unrelated shell diagnostic below remains separate. |
| Read failure versus absence | passed | Renderer loading/empty/missing/failed/stale cases; session failure retention; sandbox browser now explicitly visits Loading, FailedLoad and StaleRefresh and recovers by reading. |
| Draft/form lifetime and edit during save | passed | Actual form validation/mounted create context; admitted fieldset test; four successor-input cases; production dirty Mark-read and explicit navigation; no replay/rebinding. |
| Mutation admission | passed | Session duplicate/stale callbacks, captured values and independent target gates; real form repeated submission; sandbox independent older/successor operations and exact stored counts. |
| Owner semantics | passed | Real owner PostgreSQL tests for both item kinds, reply, missing/invalid targets, already-read no-op and automation; production create form/read-back for notification and escalation. |
| Save then refresh failure / unknown | passed | Session create identity, reply saved-warning, read-only retry and unknown-lock tests; sandbox SavedWithRefreshWarning and OwnerRefusal real-form/browser scenarios. No receipt/idempotency protocol invented. |
| Post-commit observer fault | passed | Real owner test retains committed writes and later observers; owner source logs each subscriber failure. Ambient transaction behavior is unchanged. |
| Shell and linked context | passed | Existing real PostgreSQL badge case exercises create, reply and Mark-read updates; MainLayout dispatch/error/generation/disposal paths inspected; browser Scheduler link/back and escaped content; safe-route unit theory. Separate shared-shell disposal limitation below. |
| Durable compatibility | passed | Seven integration cases include four-entity schema parity, restart/read-back and profile isolation; five context cases preserve GUID-token behavior. Enum names/values and mappings are unchanged by this follow-up. |
| Evaluated build/runtime boundary | passed | Positive/negative/unresolved-edge guards; fresh recursive graph: Web 135 projects / 140 packages / 30 native assets, sandbox 5 / 1 framework asset package / 0 native assets; no cycles or unresolved edges. |
| Parity assets and watch membership | passed | Actual CSS/JS/fonts in both browser hosts; fresh `dotnet watch --project <host> --list`: Web 4,394, sandbox 252, unchanged. No project, asset or production source membership change. |
| New watch timing / full-Web comparative speedup | not run | This bounded state repair does not change the development-loop boundary. Earlier three-sample sandbox measurements remain historical; no new timings or full-Web speedup are claimed. |
| Fast mode / feature CSS-JS edit timing | not applicable | No Fast mode or feature CSS/JS exists. Current Parity assets are reused; no new styling classes require generation. |
| API/schema/authorization/concurrency expansion | not applicable | No API contract, schema, permissions model, per-user unread or optimistic-concurrency change. No binary consumer/type-forwarding work is introduced. |
| Broad Stable/provider/LiveProcess/mobile proof | not run | No CI/release/merge checkpoint or named broader trigger for this bounded repair. No unrelated process, scheduler job or provider invocation was required. |

### Browser diagnostics and remaining limitation

Current screenshots under `output/playwright/collaboration-review` cover the retained reply,
production escalation deep link, ordinary creation, representative sandbox and long transcript
at 1600 × 1000. Changed-state screenshots were visually inspected. Both final journeys passed
page/console, stylesheet/script/font and server-error checks. The latest captured
`production-server.log` has no `fail:` or unhandled-exception entry.

There was an earlier **failed** production browser run (`browser-final.trx`): all interaction
and owner read-back assertions completed, but the final server check found an unhandled
rendering exception. That first assertion only retained a truncated message, so its exact
cause is not established. The test now saves the server snapshot for diagnosis. A subsequent
production-only diagnostic run passed its assertions but logged a separate shared-shell
`ObjectDisposedException`: `ConversationShellHost.InitializeContributorsAsync` reads
`lifetime.Token` after disposal while advancing to the `simple-chats` contributor. The
captured diagnostic is `artifacts/collaboration-review/server-diagnostic-production-only.log`.
The final paired rerun passed with a clean captured log; this does **not** prove the intermittent
shared-shell race fixed or establish it as the cause of the first rendering exception.

That pre-existing conversation-shell lifecycle issue is **unresolved**, outside the requested
Collaboration-only change. No shell error was filtered or swallowed, and no delay, forced click,
fake production DI or public test endpoint was added. Browser reliability therefore retains
this explicit limitation despite the final 82 passing cases. Remote CI was not triggered or
claimed; this receipt is local proof. Original historical TRX files were not reconstructed.

### Mandatory gates and architecture review

Scanner/enforcer self-tests passed 6 cases and secret-scanner self-tests passed 4.
The complete proposed-source portability scan includes untracked test files. Review found
one added and one stale executable-source fingerprint: the sandbox's old store-based
Escalations predicate became a typed accepted-snapshot projection. This introduces no
platform/process assumption. Only that reviewed replacement and baseline generation metadata
changed; no scanner pattern or exclusion changed. Final enforcement without the write flag
passed with **15,149 reviewed executable-source findings unchanged**.

```powershell
python tools/Validation/Portability/test_enforce_portability_baseline.py
python tools/Validation/Portability/test_scan_artifacts_for_secrets.py
python tools/Validation/Portability/scan_portability.py --repo-root . --output artifacts/collaboration-review/portability-final-scan.json
python tools/Validation/Portability/enforce_portability_baseline.py --scan artifacts/collaboration-review/portability-final-scan.json --baseline tools/Validation/Portability/portability-risk-baseline.json
pwsh -NoProfile -File tools/Validation/Test-DocumentationEvidence.ps1
pwsh -NoProfile -File tools/Validation/Test-Documentation.ps1
git diff --check
```

Documentation evidence tests passed **9** cases; the maintained documentation validator
passed **245** Markdown files. `git diff --check` and a separate whitespace check of both
new test files passed. The owned PostgreSQL container
`cda-collaboration-review-7c99b951ba` was stopped and auto-removed after verifying that no
test databases remained. No proof Web/sandbox process remains. Local logs, TRX, graphs,
watch inventories and screenshots stay in ignored artifact/output directories.

Architecture review: **Pass for the bounded Collaboration corrections**. The sole shared
policy is a reply-retention predicate; production still owns effects and the sandbox still
owns isolated scenario state. No new generic draft framework, service locator, partial-file
split, backend reference, copied component or second application owner was introduced.
Real production and scenario implementations both consume the same renderer/contract.
The original missing-input follow-up is reconciled above. The unrelated browser lifecycle
diagnostic and unavailable MCP-specific proof remain explicit limitations.
