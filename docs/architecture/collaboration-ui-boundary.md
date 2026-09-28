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
Quick-create is independent. Each admitted form disables its fields and reset actions;
the owner-side gate also rejects duplicate or retired callbacks. Submissions capture
immutable values before dispatch. Local replies retain `MarkAsUnread: false`.

Successful writes retain their identity even if reconciliation fails. Retry performs only
a read. Unexpected exceptions from a dispatched command leave its submission locked with
an unknown-outcome warning; the operator must inspect persisted state before explicitly
starting a new draft. Observer exceptions after a save are logged and isolated individually.

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

## Completion evidence — 2026-09-28

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
