# Resources UI boundary

The Workspace Settings Core follow-on closes RS-R1: catalog/reference readiness no longer
marks an exact Registry editor acquired. Held/failed reads block placeholder mutations;
failed same-ID selection and initial routes can retry. Acquired dirty editors retain their
state and allowed cleanup after optional-reference failure. See the
[current prerequisite proof](workspace-settings-core-ui-boundary.md) for the failing-first
cases, 70 light cases, 21 routed/owner cases and accepted Memory origin regression.

Resources renders its complete Registry and Browse workspace through the same lightweight
boundary in production and the standalone sandbox. Project admission, connector validation,
transactions, source resolution, actor authorization, storage and launch remain with the
existing in-process owners. This is UI/build-graph decoupling, with no new provider feature.

Execution entered `components-decoupling` at `b77fb24a3c4c3925f83f2b928634ae91085cbcc6`.
Review provenance `5f7f8329e3e3e30bd1451fc72757e04847579754` was inspected as history, not
checked out. The sealed Resources package/shared foundation remain unchanged. Current
instructions and actual source governed the work. Ignored proof is in `artifacts/resources-ui`
and `output/playwright/resources-ui`; no runtime log, binary, credential or screenshot is committed.

## S0: Memory prerequisite

Signed commit `9dac16414e38774eb7eb96d22cde7480bb31f9ea` closes ME-R1 before the Resources
baseline. Four failing-first cases reproduced visible/held results followed by provider
replacement or removal. A result retains provider revision and operation origin; replacement
retires it into labelled history. Failed reads retain stale evidence, identical revisions
preserve valid results, and manual query/feedback text survives. Refresh does not replay a
query. Scheduler corrections and shipped Memory capability refusals are unchanged.

Fresh discovery/execution passed 51 cases: 33 `MemoryUi`, 3 `MemoryBoundaryTests`,
12 `MemoryWorkspaceOwnerTests`, 3 `MemoryBrowserTests`, no skips. The real PostgreSQL snapshot
and browser proof, builds, initial static gate and failed attempts are recorded in the
[Memory receipt](memory-ui-boundary.md#me-r1-result-origin-correction-resources-prerequisite-2026-09-29).

## R1: inventory and baseline

The original tree comprised `/resources`, `ResourcesPage.razor/.cs`, `ResourceFileBrowsePane`,
`ResourceStorageObjectPromotionDialog`, Workspace's `ConnectorConfigFieldEditor`, BaseLib,
FileBrowser and read-only FileInteraction. Browse owned scoped CSS; browser file effects used
AppComponents' existing JS module. Web supplied theme/fonts/assets and the route/Agent shell.

`ResourceModels.cs` mixed value contracts with EF entities/services. Actual consumers include
connector plugins, pickers, Workbench enlisted projection reads, project lifetime admission,
Resource Memory snapshots and Workspace's configuration fallback. No persisted assembly-qualified
Resource value name or shipped external binary requiring a forwarder was identified. Source
consumers rebuild together; arbitrary external binary plugin compatibility is not claimed.

Before extraction, owned PostgreSQL and Web held a normal web-link resource, a historical
project-lifetime record and a harmless filesystem file. Registry, Browse, promotion, governed
reopen/read and back navigation were exercised and inspected at 1600x1000. The original Refresh
button wrapped vertically and configuration fields were narrow. Captures/measurements are
`original-web-*`. Baseline proof passed 30 main component cases and the real file integration case.

CodeAnalytics, Components MCP and dotnetwatch MCP were not callable. Local sources/consumer
searches, evaluated restore graphs, runtime traversal, CLI watch lists and real browser
interactions supplied the fallback evidence. No tool output was inferred.

## R2: dependency cut

| Project | Responsibility |
| --- | --- |
| Resources.Contracts | Existing enums/configuration/summary/editor values, source identity, reference-only options, typed Registry/Browse ports and scoped rendering leases |
| Configuration.UI | The actual shared field renderer/raw input draft over existing SharedKernel schema/state and BaseLib controls |
| Resources.UI | Complete Registry, filters/editor, Browse, FileBrowser, promotion, receipt history and read-only FileInteraction |
| Resources.Presentation | Shared Registry and Browse controllers: drafts, captured effects, reconciliation and independent read/lease lifetimes |
| Resources module | Existing owners and typed production adapters; thin route, canonical-navigation, notification and Agent context host |
| Resources.UiSandbox | Same renderer/controllers with bounded synthetic metadata, file and content owners |

Namespaces, enum numbers, serialized property names/defaults and the summary's ignored project
lifetime are preserved. Wire tests read an existing numeric enum payload and connector JSON.
EF entities/mappings, transactions, connector executors, source enumeration and actual file
authority remain in their original owners. No schema migration or new idempotency/concurrency
protocol was introduced. Contracts reuse Projects' exact `ProjectWriteAdmission`.

The shared field editor moved once. Workspace's real fallback caller projects secret IDs/names
and uses Configuration.UI. Raw incomplete numeric/JSON input survives; owner schema validation
still decides validity. This bounded child extraction neither extracts Workspace nor loads secrets.

The owner creates FileBrowserSession/read-only content leases and releases their grants.
Renderers receive typed workspaces; they do not inject production owners, service locators,
concrete coordinators or runtime registration. AppComponents is a genuine shared edge for
activation/action policy. Its Canvas, RecordBrowsing, Conversations and Markdig dependencies
are explicitly audited. The sandbox links generated theme CSS as content, with a missing-theme
build diagnostic, and has no Web reference.

The main solution includes four new production projects. The light test project belongs to
Components, Stable and all three actual CI component-shard lists. The sandbox follows the
existing direct-build pattern. Playwright builds it with `ReferenceOutputAssembly=false`.

## R3: Registry state and mutation outcomes

Registry owns a stable editor, EditContext and raw configuration draft. Immediate input events
capture text before blur. Refresh/reference/party reads and tab changes retain forms. Field
versions preserve edit-away-and-back; a mutation version prevents an older reconciliation from
overtaking a newer save. Read, editor/route and party generations are independent of mutations.

Each admitted Save/Delete captures the full editor, cloned configuration and exact project
admission before its first await. Handler admission blocks conflicting pending/unknown work
by aggregate or create origin while permitting independent targets. New drafts and explicit
project/connector changes have distinct lifetimes; no-op transitions preserve current work.

Confirmed IDs enter the original receipt and matching draft before secondary reads. Failed or
missing read-back cannot turn an existing target into a create. Refusal, committed success,
committed warning and unknown acknowledgement remain distinct. Read retry never repeats a write.
Unknown known-ID review is single-flight. Unknown-create recovery requires an explicit candidate
ID and records an observation without claiming the request created it. Opening the observed row
is a separate operator action. Target-labelled receipts survive navigation. Each history admits
at most 32 entries and does not evict pending, unknown or actively reviewed receipts for new work.

Unavailable connector/project/secret/party IDs stay visible. Missing connectors never select the
first manifest or discard stored configuration. Explicit schema/connector adoption retains
matching fields and removes fields outside that schema. Historical project admission stays
historical until deliberate current selection; combined routes require the exact lifetime,
while resource-only routes retain historical reads. Current project filters exclude unbound/old
lifetimes. Governed resources expose no normal edit form and the owner still refuses Save.

Location preview uses the existing cheap connector projection. Typing/filter/tab changes make
no DB/network/secret-value reads. Route load reads catalog/references, exact editor and party
options; explicit refresh repeats reads only. Promotion's parent refresh retains the Registry
draft and reports failure as a follow-up warning on the already confirmed promotion.

## R4: Browse, promotion and file authority

Catalog, source open, preview and promotion have independent origins. Leases detach before
asynchronous release. Late stale acquisitions dispose once, including after host retirement.
A failing-first held-cleanup case found a refresh marking a newer source failed after old
cleanup; the old state is now published before awaiting release. A-B-A, slow source/preview
cleanup and held/failing parent callbacks preserve successors and original results.

Promotion captures source key/scope/revision, exact item, project admission, name and sensitivity.
The adapter re-resolves current scope/revision/provider membership before promotion/download/local
launch. The existing chain then reactivates the exact item, authorizes the current actor,
validates stable occurrence and executes the lifetime-checked writer. Persisted configuration
contains the stable source/storage/provider/locator contract, never opaque grants, bearer URLs
or substituted absolute paths. Same-config/same-lifetime deduplication remains unchanged.

The promotion service retains ResourceId/Created as soon as the writer returns. Revision
publication/read, logger or revoke failure yields a typed committed observation; unavailable
revision remains null. Cleanup is attempted, and secondary failure types preserve the primary
failure. Save/Delete diagnostics protect their existing committed exceptions the same way.
A lost writer or real transaction acknowledgement remains unknown, never confirmed. PostgreSQL
tests verify exact row identity/count, explicit review and revoked-grant denial after faults.

Governed reopen rechecks current source/actor/file access and reads actual content. View mode
disables editing and mode switching. Supported files and keyboard invocation retain promotion;
unsupported pointer double-click uses preferred-app intent only with current local policy.
Downloads recheck cancellation/disposal after authorization/content open/JS import. An already
admitted external effect retains its original receipt rather than being claimed undone.

Preserved budgets: source512, page50; search32 containers/2,000 items/five seconds/one request/
200 matches/2 MiB retained data; preview16 MiB; disabled browser-session retention. Tests drive
the real search and bounded page, owner source overflow and fail-closed authority negatives.

## R5: sandbox, browser and published assets

Thirteen scenarios cover representative, empty, large, missing/retired references, missing
connector, invalid fields, failed reads, refused/unknown/committed-warning writes and denied/
unavailable effects. Hold lanes cover reads, mutations, promotion, preview, actions/downloads
and source/preview cleanup. Failed refresh, partial references, reset and missing selection are
interactive. Large data has 200 resource rows and 220 fixture files through real page50 browsing.

Writes update the original scenario store once. Reset retires controllers, releases all held
gates including cleanup, then mounts fresh owners. Retired stores remain inspectable, trimming
completed stores beyond eight. Current counters observe workspace changes. State is per host.

Production browser proof covers PostgreSQL create/edit/read-back/delete, task-owned file
promotion, governed reopen/read, streamed download, read-only presentation and exact cleanup.
Sandbox proof covers focus/caret during held refresh, pre-blur/raw numeric/JSON fields,
validation, unknown exact review, retired writes, held preview release and actual file children.

The published DLL runs in Production without database configuration. Registry/Browse,
promotion, actual FileInteraction, fixture download, loaded Material font, scoped 544px minimum
Browse geometry and zero page/HTTP errors are recorded in `published-proof.json`. Static CSS,
fonts, Blazor and shared FileTools/AppComponents scripts resolve through the published manifest.

Inspected desktop captures include production Registry/Browse/promotion/preview and source/
published sandbox states. Refresh uses an accessible icon; configuration controls fill their
columns. The Medium promotion dialog retains its visible action footer. Browse lists/details
own scrolling, receipt histories are bounded/scrollable, and no horizontal overflow occurs.
Some CRUD captures retain the shell scroll position; the measured journey also verifies the
full heading at its normal top position. Sandbox controls fit compact rows above the workspace.

FTP/IPFS are labelled synthetic. No live provider account, operator storage tree or local
application launch was used. Safe effect doubles prove exact preferred-app/folder intent;
production rejection and browser downloads separately prove authority/streaming. This does
not claim live transport or actual platform-launch coverage.

## Measured development loop

Windows, SDK 10.0.303, `ResourcesUiProof`, warm restored/build inputs, 1600x1000 Chromium and
source replacement mode were used. Components: `f258ab6a959a97fa16c01d0858e7dc122728a11a`;
FileTools: `3a080ecd31068a77c1e1bd639f7a78e21c93db85`;
SharedInfo: `83e21e23bcf43d92b061a6d367ac385241d13cd3`.

| Host | Projects | Packages | Native assets | Watch inputs |
| --- | ---: | ---: | ---: | ---: |
| Original Web after S0 | 146 | 140 | 30 | 4,467 |
| Extracted Web | 150 | 140 | 30 | 4,490 |
| Resources sandbox | 20 | 2 | 0 | 712 |

Graphs have zero unresolved/cyclic edges. Sandbox packages are Markdig 1.1.2 and
Microsoft.AspNetCore.App.Internal.Assets 10.0.11. Evaluated and runtime closure reject
implementations, infrastructure, production file composition and Web transitively, with
forbidden/unresolved negative controls. Final watch lists include the new mutation-history
stylesheet; earlier lists were one input smaller and remain available.

| Host | Startup, one sample (ms) | Razor samples (ms) | Executed C# samples (ms) | Scoped CSS samples (ms) |
| --- | ---: | --- | --- | --- |
| Original Web | 45,811 | 3,936 / 2,935 / 2,911 | 2,850 / 1,725 / 1,820 | 6,264 / 5,403 / 4,704 |
| Extracted Web | 47,670 | 4,932 / 3,926 / 3,935 | 3,580 / 1,384 / 1,457 | 6,058 / 5,843 / 4,973 |
| Sandbox | 12,003 | 4,926 / 2,903 / 2,884 | 2,511 / 349 / 363 | 1,040 / 1,480 / 817 |

Razor changes a real heading. C# changes the original executed editor title and the extracted
selected-project projection, observed through tab actions. CSS changes the real Browse
descendant's computed minimum height. Feature-owned JS is N/A; real shared module/download
callbacks and asset responses are separately proven. Supported edits needed no restart.
Every probe restores exact source bytes and stops its owned process tree. Web uses PostgreSQL/
files while sandbox uses synthetic owners; single startup observations are not a speedup ratio.

Actual launches use `dotnet watch --non-interactive --project <host> --configuration
ResourcesUiProof --no-launch-profile -- --urls <owned loopback URL>`. PIDs/ports and samples
are in `original-web-loop.json`, `extracted-web-loop.json`, `sandbox-loop.json` and owned-host
receipts. `dotnet watch --list` and evaluated graphs are captured per host.

## Validation selections and widening

Each selection derives expected discovery from facts/theory rows, freshly builds with
`--list-tests --filter`, then runs the same filter using `--no-build --no-restore`,
`ResourcesUiProof` and `/m:1`. Database tests use owned PostgreSQL 18.6. Below, `A | B` means
`FullyQualifiedName~A|FullyQualifiedName~B`. All successful executions have zero failures/skips.

| Owning project | Filter names | Discovered/executed | Artifact prefix |
| --- | --- | ---: | --- |
| Resources.UI.Tests | `CanDoItAll.Tests.Components.ResourcesUi` | 60/60 | `resources-light-delivery` |
| Tests.Components | `ResourceFileBrowsePaneTests`, `ResourcesPageAgentChatContextTests`, `OwnerPostcommitPageTests`, `ConnectorConfigFieldEditorTests`, `FileToolsHostActionsTests`, `ResourceCardPickerTests`, `ProjectStructureResourcePickerTests`, `ProjectStructureTaskResourcePickerTests` | 71/71 | `resources-components-consumers` |
| Tests.Unit | `ResourceFileSourceCatalogTests`, `ResourceRouteContextSelectionTests`, `ResourceStorageObjectPromotionTests`, `StorageObjectResourceConnectorTests`, `CrmHrResourceSourceGatewayAdapterTests` | 53/53 | `resources-unit-owners` |
| Tests.Integration | `OwnerPostcommitPersistenceTests`, `ProjectAdmissionSnapshotRaceTests`, `ProjectProjectionRepairAdmissionIntegrationTests`, `ProjectWorkbenchProjectionMaintenanceIntegrationTests`, `ResourcesOwnerPersistenceTests`, `ResourceStorageObjectIntegrationTests`, `ResourceTestLabAdmissionIntegrationTests`, `WorkbenchOwnerProjectionIntegrationTests`, `ResourcePromotionOutcomeTests`, `ConnectorPluginIntegrationTests`, `UnknownConnectorManifestIntegrationTests` | 55/55 then | `resources-integration-owners` |
| Tests.Integration | `Production_browse_adapter_refuses_stale_origins_before_any_effect` | 3/3 added later | `resources-authority-negatives` |
| Tests.Integration | `Failed_preview_preserves_its_primary_failure_when_real_grant_cleanup_also_fails` | 1/1 added later | `resources-preview-cleanup` |
| Tests.Integration | `ProjectStructureTaskHttpBoundaryTests` | 3/3 | `resources-http-consumers` |
| Tests.Playwright | `ResourcesBrowserTests` | 2/2 | `resources-browser-final` |
| Tests.Unit | `HostPlatformTestClassificationTests`, `SecretScanningTests` | 16/16 | `resources-static-tests-fixed` |

The Integration union now discovers 59: `ResourcePromotionOutcomeTests` gained three
separately discovered/executed authority rows and a preview/cleanup double-failure case.
Its original nine cases cover five real
promotion postwrite faults, lost writer acknowledgement, two Save/Delete diagnostic faults
and a real transaction-committed acknowledgement interceptor. This is 264 distinct selected
Resources/consumer/gate cases, plus the separate 51-case S0 selection.

The original seven Browse cases and shared TestLab postcommit rows remain. Two unused stamp
tests moved to the light project as actual controller lifetime tests. Route policy tests still
run in Unit over the moved policy. Light coverage is 20 Registry, 23 Browse, 10 surface,
2 load-lifecycle, 3 boundary and 2 wire cases. Workspace's actual fallback caller and file
action/picker consumers are included. Resource Memory snapshots run through gateway/admission
tests; Workbench proof covers enlisted owner reads, projections and snapshots. Project Structure
HTTP stays intact. Serialization is directly checked without inventing a Resources HTTP route.

Full Stable was not selected: solution/CI edits register this slice, without changing root build
policy, shared persistence/fixture protocols or cross-cutting DI. Validation widens to the
actual shared child, action runner, moved values and their consumers. This is neither release/
merge closure nor an unfiltered-suite claim.

Failed attempts are retained. The intentional held-cleanup regression was red before its fix.
Early migrated component fixtures edited models before rendered connector/admission readiness;
they now use actual controls and concrete waits. Real menu coverage caught duplicate provider/
host Open actions in the synthetic provider, corrected to Select/Preview capabilities. A
browser initially typed before its connector schema rendered. The first wire fixture encoded
WebLink as 3; the unchanged legacy enum correctly uses 2. Static discovery initially counted four
`[Fact]` strings inside classifier fixtures; source inspection established two actual classifier
and fourteen secret tests. No case was silently removed to achieve successful discovery.

Direct final builds cover Contracts, Configuration.UI, AppComponents, Resources.UI,
Presentation, Resources, Workspace, Composition, Web and the sandbox. Published-host and
runtime boundary checks exercise actual assets and descendants, not placeholder markup.

## Validation matrix coverage

| Requirement IDs | Evidence |
| --- | --- |
| V-ME-01, V-ME-02, V-ME-03, V-ME-04, V-ME-05, V-ME-06 | S0 signed correction, failing-first orderings, actual Query panel/revision/browser, unchanged Scheduler/refusals |
| V-BD-01, V-BD-02, V-BD-03 | Inventory/before screens; evaluated/runtime/negative closure; shared complete renderer and file descendants |
| V-BD-04, V-BD-05, V-BD-06, V-BD-07 | Wire/default, Workspace fallback, route/lifetime/Agent tests, production builds and actual CI membership |
| V-RG-01, V-RG-02, V-RG-03, V-RG-04 | Raw inputs/focus, stable EditContext/versions, captured Save, conflict/independent admission, A-B-A/project/party/reset origins |
| V-RG-05, V-RG-06 | Real lost commit acknowledgement, confirmed-ID postcommit tests, exact single-flight review, row count/no replay |
| V-RG-07, V-RG-08, V-RG-09 | Missing/schema/retired references, filtered-empty/current lifetimes, governed no-edit form and owner refusal |
| V-BR-01, V-BR-02, V-BR-03 | Four sources, real search/page50, stale catalogs/A-B-A, late acquisition and slow exact-once cleanup |
| V-BR-04, V-BR-05, V-BR-06 | Captured promotion/dialog, held/failing callback, original ID and actual publication/logger/revoke faults |
| V-BR-07, V-BR-08, V-BR-09, V-BR-10 | Actual file reopen/download, activation/action tests, queued JS retirement, adapter negatives and preserved budgets |
| V-OW-01, V-OW-02, V-OW-03 | Real Registry CRUD, lifetime/recreation/exact cleanup, Search/Activity/throwing diagnostics |
| V-OW-04, V-OW-05, V-OW-06 | Durable promotion faults, actual commit interceptor, exact observation/dedupe and revoked-grant denial |
| V-OW-07, V-OW-08 | Scope/revision/provider/actor negatives; stable locator and Memory/Workbench/connector/HTTP consumers |
| V-UI-01, V-UI-02, V-UI-03 | Production Web and standalone journeys, actual owned file versus labelled synthetic effects |
| V-UI-04, V-UI-05, V-UI-06 | Published no-DB host, network/font/scoped/JS proof, inspected desktop/focus/stacking and measured edit loops |
| V-CL-01, V-CL-02, V-CL-03, V-CL-04 | Fresh discovery/builds, portability/documentation/evidence/secrets, owned cleanup and signed delivery |

## Static gates and final closure

The complete proposed-tree scan includes new additions: 7,048 files and 32,347 findings
at the final source revision. All added/stale protected findings were inspected. The reviewed
baseline changes by 88 added and 41 stale occurrences to 15,186: moved value/connector/path
metadata, typed FileTools imports/actions, ordinal metadata-key/search policy, the real
reference-only SecretService adapter, the typed preview-cleanup diagnostic key, and README
command fences. No new OS launch/path policy
is hidden by these deltas. Scanner rules and protected scopes remain unchanged. Regeneration
after source edits and enforcement without `--write-baseline` pass. Scanner utilities pass
6 baseline and 4 redaction tests.

The source secret/platform selection passes. Artifact scanning redacted only the synthetic
scanner test keys echoed in discovery/TRX names, preserving counts/results. Final scans have
zero findings, unreadable files or oversized text omissions; non-text binaries/images are
explicitly outside that text scan. Browser logs have zero findings. Documentation validation
passes for 279 maintained Markdown files; all 9 documentation-evidence cases pass. Ten direct
project builds report zero warnings/errors. The final preview diagnostic adjustment has its
own fresh owner test and direct Resources rebuild, also with zero warnings/errors.

Package validation still reports 36 files, 35 manifest entries and 42 sources; 11 package and
14 shared-tool cases pass. These utilities certify package integrity/tool behavior, not product
features, and are excluded from the product test count. No third module was started.

The task-owned PostgreSQL container was stopped/removed only after its exact ID, name and
`candoitall.task=resources-ui-b77fb2` label were verified. Original/extracted/sandbox watch
PIDs 72640, 4028 and 12928 and their owned temporary roots are gone. Published/browser hosts
dispose their process trees; downloads and actual storage fixture files are cleaned by their
owners. Components, FileTools and SharedInfo HEADs/source states were rechecked unchanged.
The ordinary app/database on port 5032 was never used, restarted or stopped.

| Work unit | Closure |
| --- | --- |
| S0: reproduce and fix ME-R1 | Solved, separate signed Memory commit and 51-case receipt |
| R1: full inventory and original baseline | Solved, real Web/file baseline before extraction |
| R2: complete dependency cut and production wiring | Solved, complete shared tree, actual owner adapters, evaluated/runtime negative closure |
| R3: Registry lifetime, drafts and mutation facts | Solved, deterministic and real PostgreSQL/owner proof |
| R4: Browse, promotion, reopen and file actions | Solved, actual file components/owner chain, fault and origin proof |
| R5: faithful sandbox, validation and development loop | Solved, source/published browser, measured loops, static gates and owned cleanup |

Architecture/closure review finds no heavy implementation path in the sandbox and no fake
child substitution, universal controller, parallel configuration engine or relocated authority.
There are no unresolved in-scope blockers. External transport/platform-launch limits are
explicit above. Local signed commit identity and clean status are recorded at delivery in
the ignored `artifacts/resources-ui/closure.json`; no push or merge is requested or performed.
