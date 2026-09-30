# Workspace Storage catalog UI boundary

Status: complete for AP-R1/AP-R2 and Storage catalog administration; validation closed.
This record covers AP-R1/AP-R2 and Storage catalog
administration only. Data Sources, placement recovery, shared catalog pickers and the
remaining configuration host require separate audits; Workspace is not complete.

Subsequent selection slice: [Storage selection boundary](workspace-storage-selection-ui-boundary.md)
records the failing-first SCAT-R1 repair, the extracted field/dialog and its production
and independent sandbox proof. The measurements below remain this catalog slice's
historical evidence; they are not rerun claims for the selection work.

## Entry and work units

Entry: `components-decoupling`, `73053bd4fe723d56043df410c322c0651f6cea4c`, clean.
The only drift from the reviewed `56f615a19f5d7eb22042584230ceebcc611bf549` is the sealed
[assignment](../../codex/bundles/CanDoItAll_Workspace_Storage_Catalog_UI_Decoupling/prompt.md).
Its review, matrices and unchanged shared foundation are inputs, not fresh runtime proof.
SDK: 10.0.303, Windows, sibling source mode. Read-only sibling revisions:
Components `f258ab6a959a97fa16c01d0858e7dc122728a11a`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`, SharedInfo
`83e21e23bcf43d92b061a6d367ac385241d13cd3`; all clean at entry.
CodeAnalytics, Components and dotnetwatch MCP tools are unavailable. Local source,
MSBuild/CLI watch and Playwright provide the explicit fallback evidence.

The compatible package maps inputs to S0 then C1–C5, with acceptance and application
matrices. Semantic readiness passes; no canonical bundle restructuring is needed.
All work units use Behavioral proof. Generated evidence stays ignored in
`artifacts/workspace-storage-catalog-ui` and `output/playwright/workspace-storage-catalog-ui`.

| Unit | Responsibility | State and gate |
|---|---|---|
| S0 | API read cleanup and current-denial authority | Reproduced and repaired; 82 light, 11 production components and one real-browser case pass |
| C1 | Original wizard, owner/consumer and graph/watch inventory | Captured; ST06 original four cases pass |
| C2 | Light contracts, actual renderer and production composition | Implemented; 33 standalone cases and one production Settings/Recovery component case pass |
| C3 | Draft/effect/profile ownership and real persistence stages | Implemented; 15 real PostgreSQL owner cases pass |
| C4 | Independent bounded scenario host | Source and published Production browser cases pass |
| C5 | Both matrices, publish/browser/watch, static and documentation closure | Both matrices executed; final Release, browser, graph/watch and static gates pass; one Stable classification failure repaired and rechecked |

## Architecture decision before extraction

`StorageSettingsPanel` currently mixes catalog rendering and three-step editing with
`WorkspaceService`, infrastructure types and Recovery composition. Isolate rendering and
editor/read/effect lifetimes in StorageCatalog.UI; use a small StorageCatalog.Contracts
projection instead of moving the broad infrastructure storage API. A production adapter
keeps profile/driver/template/credential/routing/Activity semantics with their real owners.
An independent scenario owner implements the same ports and uses the same controllers.
No separate presentation assembly is justified by this bounded surface.

The module composes the new leaf. Core and API contracts/UI/sandboxes must gain no Storage
edge, and Infrastructure must gain no product contracts reference. Recovery stays in the
module host with an exact captured persisted ID or explicit catalog-wide null. Pickers and
their Agent/Workflow consumers retain their types and authority. A wrapper around the old
backend-dependent renderer was rejected because it would not cut its rendered closure.

Keep the list/editor comparison layout: actual BaseLib ListDetailShell, Steps,
StorageSummaryCard, fields, status and dialogs. Catalog/search is the primary left surface;
the selected editor is the right working surface. Counts support the list; no new metric
cards. Preserve readable normal fields and existing desktop scroll ownership at 1600×1000.
Inspect normal/all-step states, validation, provider selectors and passive Recovery overlay,
including footer reachability, focus/caret and no lateral clipping.

## S0 evidence

New filter `FullyQualifiedName~ApiReadLifetimeTests`, source expectation/discovery 7:
initial run failed 6, passed 1, skipped 0. Four cleanup paths throw disposed-CTS exceptions;
two current-list denial paths fail to retire shared management. The successor-cancellation
negative control passes. The same seven cases pass after the repair. Four additional
cases cover both bounded correction denials and closed/prior-activation late denials.
The complete light API namespace discovers/passes 82, skips 0, in `ApiUiProof`.

Commands use `artifacts/workspace-storage-catalog-ui/Invoke-Proof.ps1`: build-backed
`dotnet test <project> --configuration ApiUiProof --list-tests --filter <filter> /m:1`,
then the same filter with `--no-build --no-restore`, TRX and exact count enforcement.
The project is `tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests`.
Raw failing-first and passing evidence remains separate. Tests substitute only owner ports;
the actual ApiAccessSurface and BaseLib descendants render in bUnit.

Cleanup detaches only the exact owned read source regardless of publication eligibility;
old finally blocks cannot clear a successor. Page denial separately calls its shared
authority owner while the child request is current. Normal child close remains local.
The session clears its retained source and has idempotent disposal. Existing safe receipts
survive retirement; neither retry nor observation reissues credentials.

## Validation selection and checkpoint

Focused light API, production Settings components and the controlled real Web denial journey
are S0 gates. Storage tests are selected from the actual catalog/routing/credential,
file/attachment, Resources, Agent restriction, Workflow, picker and recovery owners named
by the application's non-regression matrix. Each new filter records its source-derived
expectation before build-backed discovery and identical execution. Owner and browser lanes
use a task-owned PostgreSQL 18 server, private files and ports, never the ordinary app.

Broad Stable is triggered at the final frozen checkpoint by the shared StorageCatalogService
SaveCore/DeleteCore persistence changes and root solution membership changes. Execute it once
after focused proof; task size is not the trigger. No broad pass is inherited from older records.
Final closure additionally requires original/final graphs and watch samples, production and
source/published standalone browser proof, reviewed portability deltas and final no-write
enforcement, documentation/evidence and secret scanning, and verified owned cleanup.

The broad build freezes 56 changed executable/build/test files in
`frozen-source-checkpoint.json`. Subsequent concurrency review changed only CatalogSession,
CatalogOperationLedger and their two test files; `ui-followup-checkpoint.json` retains both
hashes and enforces that exact delta. Shared persistence, composition, public contracts and
dependency inputs remain unchanged. The original broad run therefore retains its original
30 Storage cases; final focused discovery/build/execution covers all 33 current cases and
the affected production/sandbox hosts. This private UI delta adds no new broad-gate trigger.
The final manifest additionally records removal of one trailing blank line in the browser
host helper; it verifies identical C# tokens by restoring those exact bytes before comparing
the prior hash. All earlier checkpoint manifests remain intact.
`delivery-source-checkpoint.json` then records exactly one additional test-metadata line:
the Storage owner fixture's required class-level HostPlatform category. Test bodies and
production owners remain unchanged.

## Current owner findings

The new exact-editor entry points guard actual persistence acquisition. Existing callers keep
legacy upsert behavior. Save retains separately committed catalog, per-purpose routing and
Activity stages. The existing runtime write fence admits one captured original profile through
the whole operation; restart publication waits for that operation. Canonical factories remain
bound to the original database. This is an existing process-local fence, not distributed locking.

The original filesystem draft Test uses `StorageDriverInput.FromDraft`, without an active host
binding. Its real driver reports Unavailable under the current host-binding policy. The new
owner deliberately preserves that refusal, including on an existing draft; it does not invent
an implicit rebind or substitute the separate saved-row test service. The first owner execution
passed eight and failed four expectations of Healthy. A direct comparison with legacy
WorkspaceService.TestStorageAsync confirms Unavailable in both paths. The corrected twelve
cases pass with zero skips, testing actual private filesystem calls and original row identities.

Private PostgreSQL SaveChanges interceptors reproduce before-first-routing, after-first-routing,
and after-catalog acknowledgement failures. Catalog IDs remain known for routing/Activity
failures; lost catalog acknowledgement remains Unknown even after observing the real row.
Tests cover deletion between acquisition and flush, real secret runtime resolution, diagnostics
failure, unsaved/existing Test, and a held two-profile operation. No schema migration is added.

## Final responsibilities and semantics

| Owner | Responsibility |
|---|---|
| StorageCatalog.Contracts | Immutable safe catalog/configuration/reference projections, typed command and stage outcome; no project dependencies |
| StorageCatalog.UI | Exact read lanes, stable draft/EditContext and per-field revisions, command admission and bounded receipt history, full three-step BaseLib renderer |
| WorkspaceStorageCatalogOwner | Captured canonical profile/generation, independent real reads, canonical provider/routing projections |
| StorageCatalogCommands | Existing runtime write fence, catalog/routing/driver/credential/Activity orchestration and safe diagnostics |
| StorageCatalogService | Original bootstrap/serialization/routing behavior and strict editor-only existence/system checks at persistence acquisition |
| StorageSettingsPanel | Active production slot, authentication/profile retirement and Recovery with the captured opening ID/null |
| Independent sandbox | Same renderer and controllers with bounded, mutable original scenario stores; simulated health and deferred Recovery |

The wizard retains FileSystem, IPFS and FTP configuration, enabled/read-only/order flags,
exact credential references, all eight tracked purposes, health/capability summaries and
explicit Test/Save/Delete. Steps and reference refresh retain the same draft/EditContext.
Raw incomplete integer text survives until validation. Missing targets never become New;
missing secret IDs stay visible. The production secret list has no disabled-state field;
the UI contract can display a disabled reference without inventing a different secret policy.

Commands capture immutable values before awaiting. All effects in a view share admission
because different catalog IDs can compete for the same default purpose. The scoped ledger
retains at most 32 safe receipts and refuses capacity when none can be safely evicted.
Unknown acknowledgement blocks replay; observation cannot establish original causality.
Known partial routing requires explicit review before a separate corrective Save. Read-back
does not repeat catalog writes, per-purpose routing or driver tests. Diagnostic logger failure
is itself surfaced without erasing already acknowledged stages.

Review reproduced two additional routing reconciliation failures: automatic read-back after
Test and after partially completed routing discarded an unsaved purpose selection. The new
two-case test failed twice before repair. Routing now merges only when routing completion
was acknowledged, still subject to its field revision. Both cases pass in the final 33-case
standalone suite. Endpoint/provider/other fields keep their independent revision checks.

Final concurrency review reproduced two more failures: delayed Save/Test read-back could
replace a later Test result on an unchanged draft, and a reopened view did not render an
admitted operation's completion from the retired view. The two ordering cases and one real
renderer case each failed before repair. Automatic merge now also requires the captured
mutation revision. The scoped ledger notifies current sessions for its exact context, and
disposal unsubscribes the old session. A reopened draft never inherits the old create ID.
All 33 cases pass together; no owner or shared contract changed for these repairs.

Legacy non-UI upsert and delete behavior is unchanged. Metadata-only queries still avoid
unrelated malformed provider configuration; omitted configuration preserves exact bytes.
Save remains catalog, separate purpose writes, then Activity. Read methods can initialize
bootstrap records/rules; they are not described as globally write-free. The existing draft
Test's host-binding refusal remains, and no new FTP/IPFS implementation or implicit binding
is introduced. Driver exception messages and credential material do not enter UI receipts.

Recovery implementation, authorization, reconciliation and shared picker types remain
outside the new leaf. The real production Recovery dialog opens passively with a fixed
target while editor selection changes. Catalog-wide null is explicit. No picker AllowAll,
Agent restriction, Resources actor/source identity or project/file authority was changed.

## Focused proof ledger

Base matrix selections use `ApiUiProof`; the final classification/owner confirmation uses
`Release` as noted below. All use .NET SDK 10.0.303, `/m:1`, source siblings and
build-backed discovery followed by the identical `--no-build --no-restore` filter.
Each table count is expected/discovered/executed/passed; all final rows have zero failures
and skips. Initial failures are retained separately below. The exact consumer filters are
recorded in the appendix. Artifacts use the row's evidence stem under the ignored task root.
The thirteen base selections contain 658 distinct executed/passed cases, with no overlap,
failures or skips (`focused-summary.json`). The UI follow-up and Release owner/light rows
repeat affected cases against the final implementation and are not added to that distinct
count. The two-case host-classification guard separately verifies the Stable repair.

| Project/layer | Filter or selection | Count | Evidence stem |
|---|---|---:|---|
| API light | `FullyQualifiedName~WorkspaceApiUi` | 82 | `api-light-final` |
| Main Components, API | Existing administration/status/capture plus `WorkspaceApiDenialTests` | 11 | `api-production-components` |
| Unit, API | API unit appendix | 29 | `api-unit` |
| Integration, API HTTP | API HTTP appendix | 38 | `api-http` |
| Storage light | `FullyQualifiedName~WorkspaceStorageCatalogUi` | 33 | `storage-light-lifetime-final` |
| Integration, Storage owner | `FullyQualifiedName~StorageCatalogUiOwnerTests` | 15 | `storage-owner-reviewed` |
| Main Components, Storage host | `FullyQualifiedName~StorageCatalogHostTests` | 1 | `storage-host-composed` |
| Unit, Storage consumers | Storage unit appendix | 258 | `storage-unit` |
| Main Components, Storage consumers | Consumer component appendix | 55 | `storage-consumer-components` |
| Integration, Storage consumers | Consumer integration appendix | 67 | `storage-consumer-integration` |
| Core light | `FullyQualifiedName~WorkspaceUi` | 52 | `core-light-final` |
| Resources light | `FullyQualifiedName~ResourceEditorReadinessTests` | 10 | `resources-readiness-final` |
| Playwright | Seven-case exact browser filter below | 7 | `storage-browser-final` |
| Main Components, final UI follow-up | StorageCatalogHostTests plus Shell.SettingsRendererTests | 6 | `storage-host-lifetime-final` |
| Playwright, final UI follow-up | `FullyQualifiedName~StorageCatalogBrowserTests` | 3 | `storage-browser-lifetime-final` |
| Unit, host classification (Release) | `FullyQualifiedName~HostPlatformTestClassificationTests` | 2 | `host-classification-repaired` |
| Integration, host-classified owner (Release) | `FullyQualifiedName~StorageCatalogUiOwnerTests&Category=HostPlatform` | 15 | `storage-owner-host-classified` |
| Storage light, final restored source (Release) | `FullyQualifiedName~WorkspaceStorageCatalogUi` | 33 | `storage-light-release-final` |

```text
FullyQualifiedName~StorageCatalogBrowserTests|FullyQualifiedName~WorkspaceApiDenialBrowserTests|FullyQualifiedName~WorkspaceSettingsBrowserTests.Production_settings_use_real_owners_and_keep_deferred_hosts_reachable|FullyQualifiedName~ApiAccessDeploymentTests|FullyQualifiedName~ApiAccessSettingsBrowserTests
```

The private PostgreSQL 18.6 server uses only task-owned profiles and a random loopback port.
Tests verify exact persisted IDs/counts, real routing records, private file content and
two isolated databases. The actual owner fixture uses real EF persistence, a SaveChanges
interceptor for deterministic acknowledgement faults, real secret resolution, and a driver
decorator for held/failing/degraded results. No allow-all production service was added.

Initial evidence retained separately: AP-R1/AP-R2 seven-case reproduction (six failed,
one passed); API browser selector mismatch (corrected to the actual Refresh tokens button);
one owner fixture compile error (corrected to the real SecretService editor entry point);
owner policy expectations (eight passed/four failed, then twelve passed with the preserved
host-binding result); missing Recovery continuation registration in the new component fixture
(zero passed/one failed, then one passed using AddCanDoItAllApi); routing reconciliation
(zero passed/two failed, then both passed); late-operation ordering (zero passed/two failed,
then both passed); reopened-view completion (zero passed/one failed, then passed). A follow-up
host selection was stopped at discovery because its expectation incorrectly said four; the
actual source selection contains six cases, and the failed command executed none.
Corrected discovery and execution both pass all six.
A first restore-dependent build lacked assets
and was followed by a successful restore/build. No earlier failure is relabelled green.

## Acceptance and application coverage

| Matrix rows | Executed evidence |
|---|---|
| V-AP-01–08 | ApiReadLifetimeTests, existing ApiSession/Page/issuance/receipt tests, production denial component/browser, API owner and secured HTTP selections |
| V-BD-01–03 | Original wizard and ST06 capture; negative runtime/public/evaluated dependency tests; graph/watch table below |
| V-BD-04, V-UI-05 | Production active-slot component with real Recovery registration; passive production Recovery open/close; source/published deferred callback |
| V-ST-01–07 | CatalogStateTests and actual CatalogRendererTests for all providers, immediate/raw fields, missing/exact/ABA reads, current/stale references, revisions and admission |
| V-OW-01–04 | PostgreSQL create/update/delete, missing/protected exact writes, before/during routing faults, Activity/logger faults, lost acknowledgement and non-replay observation |
| V-OW-05–07 | Unsaved/existing actual draft driver semantics, captured held tests, sanitized degraded/unavailable and thrown/missing drivers, health persistence vs Activity acknowledgement |
| V-OW-08–10 | ST06 and catalog/routing/host-binding tests, real credential policy fixture, two-profile fence and retained original row |
| V-OW-11 | All application rows mapped below; no live model/FTP/IPFS lane is needed or claimed |
| V-UI-01–04 | Representative/empty/204-row/missing/error/partial/system scenarios, held original-store effects, production browser CRUD/routing/Test, source/published real wizard and assets |
| V-UI-06 | Original/final watch edit samples and restoration receipts; typing/steps/reference refresh assert zero driver calls |
| V-CL-01–04 | Product/Stable checkpoint, solution/CI membership, reviewed portability, documentation/evidence/package/secret checks and owned cleanup recorded at closure |

Application startup, direct Settings routes and browser back/forward are covered by the
existing Core production journey. It saves defaults, creates/edits/deletes masked private
Secrets, checks Files target handling and explicit history load/confirmation, and reaches
Data Sources, Storage and API. Core's 52 cases retain the Files target-identity fix;
Resources' ten readiness cases retain RS-R1.

Catalog/routing/host-binding unit cases and ST06 integration verify workspace and project
rules, priorities, alternatives and omitted/raw configuration. ManagedFilesStorageIntegration,
ProjectAssetStorage/Creation, generated-image attachments and deletion concurrency cover normal
project storage and actual file reads. ResourceStorageObjectIntegration, ResourcePromotionOutcome,
ResourcesOwnerPersistence and SearchStorageOwnerPersistence retain governed promotion/reopen.
StorageCatalogSelectionComponents preserves exact Agent catalog IDs and AllowAll=false.
MAF attachment/disclosure, workflow storage spreadsheet and ProjectStructure attachment cases
exercise unchanged consumer contracts without a live LLM.

StoragePlacementRecoveryDialogTests plus stable-placement integration cover existing Recovery;
the host test verifies captured ID/null and lazy activation. SettingsRendererTests and selection
components preserve configuration/picker siblings. API secured HTTP tests verify authorization,
while ApiAccessSettingsBrowserTests explicitly restarts against another private database and
proves existing account sessions and machine tokens remain instance-local. Data Sources remains
on its original panel and startup-override locking. No profile activation policy was relaxed.

## Dependency and development loop evidence

| Host | Projects before → after | Packages | Native assets | Watch inputs before → after |
|---|---:|---:|---:|---:|
| Web | 155 → 157 | 140 | 30 | 4539 → 4557 |
| Core sandbox | 8 → 8 | 1 | 0 | 285 → 285 |
| API sandbox | 5 → 5 | 1 | 0 | 263 → 263 |
| Storage sandbox | new → 5 | 1 | 0 | new → 258 |

These are evaluated source-mode restore closures, including sibling replacement, and actual
`dotnet watch --list --configuration ApiUiProof` inputs. Both protected graph JSON objects
and both watch sets compare exactly equal. All closures have zero unresolved project edges
and cycles. The Storage closure is UI, contracts, sandbox, BaseLib and Common; its only
package is the SDK's ASP.NET internal assets. Runtime/public-type negative tests reject
Infrastructure, missing dependencies and cycles, including generic/array element exposure.

Separately, `GenerateRestoreGraphFile /p:UseLocalCanDoItAllLibraries=false` evaluates three
product projects and BaseLib 0.3.0 as a package dependency. An isolated package-mode restore
also passes: three projects, three packages (BaseLib, Common and ASP.NET internal assets),
zero unresolved edges/native assets. The normal feed does not contain BaseLib 0.3.0 (NU1102),
so unchanged Components Release outputs were packed into a task-owned local feed; hashes and
the sibling revision are recorded in `storage-restored-package-closure.json`. A first explicit
CLI feed attempt normalized the HTTPS source as a local path (NU1301); a task-owned NuGet
config resolved it. Neither repository nor machine feeds, dependency versions, source-mode
assets or sibling source files were modified. Nothing was published.

This package proof validates restored closure; it is not a claim of an independently executed
package-mode host. Browser, publish and edit-loop proof uses the supported sibling source
graph. Core/API/Foundation gain no Storage edge, and no boundary guard was relaxed.

The sandbox has real BaseLib Steps, summaries, validation, dialogs/assets and bounded controls.
Desktop inspection at 1600×1000 found a shrinking Stack layout after the first six browser
cases passed. Explicit Stretch alignment and a width assertion corrected it. The seven-case
rerun passed with focus/caret preserved and zero circuit/console/HTTP asset errors. A receipt
caption encoding defect caught in that inspection was corrected before the final checkpoint.
The final three-case browser run after the UI lifetime repairs passes in 31 seconds. It
includes a fresh standalone publish, source-mode host and actual production owner journey.
Current source/published screenshots show the corrected receipt caption, readable fields,
full-width Steps, reachable footer actions and collapsible scenario controls.

All samples used warm source-mode `dotnet watch --non-interactive --no-launch-profile`,
configuration ApiUiProof and Chromium at 1600×1000. Startup ends when the Storage name input
is visible; subsequent Steps interactions separately prove interactivity. The raw helper
labels that event "interactive", but input visibility alone cannot time hydration precisely.
All three reported startup values use the same visible-editor endpoint. Razor samples change
the actual Save caption; C# samples
change the executed new-draft path and observe the resulting input. Final Web uses an
isolated PostgreSQL profile; the sandbox removes the test database environment variable.

| Host | Startup to visible editor ms | Three Razor samples ms | Three C# samples ms |
|---|---:|---|---|
| Original Web | 51945.0103 | 3925.551, 2909.2614, 2909.9912 | 1578.7576, 1865.6693, 1020.5261 |
| Final Web | 49927.2629 | 3921.9944, 2902.816, 2917.9651 | 791.5627, 1988.3624, 1148.1605 |
| Storage sandbox | 7984.6354 | 1902.9916, 871.2317, 1886.7287 | 589.9257, 588.7701, 575.223 |

Both final watch processes stopped and restored exact source bytes in `finally`; restoration
is also checked by the source manifests. The reported final samples were repeated after all
UI lifetime changes (`closure-web-loop.json` and `closure-sandbox-loop.json`). Both restored
hosts then rebuilt successfully. The helper emitted one nullable-analysis
warning in its private-profile seed branch; it is ignored task tooling, not shipped code.
The feature owns no scoped CSS or JavaScript file, so there is no invented edit sample for
those classes. Web can remain broad; the claim is the independent five-project feature loop,
not a statistically established full-Web speedup. Source and published browser cases were
rebuilt/rerun after the final caption correction; both passed in six seconds.

## Static, documentation and archive checks

The complete final portability scan covers 7313 files and 32623 findings without truncation,
including every new source file, staged before the tracked-only scan. Only two added case-policy fingerprints in CatalogSession
and three removed fingerprints in the old renderer required review. They implement moved
case-insensitive display search, not filesystem identity comparison. The inspected baseline
diff preserves scanner policy and has 15206 allowances; no-write enforcement passes.

Portability tooling: six baseline tests and four secret-scanner tests pass. Documentation
evidence tests: nine pass. Maintained bundle delivery tooling: eighteen pass. The unchanged
handoff validates 40 files, five JSON files, 49 links, 39 manifest entries and 35 sources;
its eleven tests and the shared foundation's fourteen tests pass. These tools validate the
archive and evidence format, not product behavior. The final delivery scan and no-write
enforcement pass after the HostPlatform metadata repair. Maintained docs and final artifact
secret scan results are recorded below.

## Broad Stable execution and bounded repair

The named source-mode Release checkpoint executed the complete documented stable filter,
unchanged between build-backed discovery and execution:

```text
Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true
```

With that value in `$storageStableFilter`, the retained command sequence was:

```powershell
dotnet restore CanDoItAll.slnx /m:1 /nr:false
dotnet build CanDoItAll.slnx -c Release --no-restore /m:1 /nr:false
dotnet restore tests/Solutions/CanDoItAll.Tests.Stable.slnx /m:1 /nr:false
dotnet build tests/Solutions/CanDoItAll.Tests.Stable.slnx -c Release --no-restore /m:1 /nr:false
dotnet test tests/Solutions/CanDoItAll.Tests.Stable.slnx -c Release --no-build --no-restore --list-tests --filter $storageStableFilter /m:1
dotnet test tests/Solutions/CanDoItAll.Tests.Stable.slnx -c Release --no-build --no-restore --filter $storageStableFilter --logger trx --results-directory artifacts/workspace-storage-catalog-ui/stable-results /m:1
```

Discovery listed 15,690 descriptors in sixteen assemblies. Execution produced 15,745 cases:
15,744 passed, one failed and none were skipped. The original run remains a failed run in
`stable-results` and `closure-results.json`; it is not relabelled as a clean baseline.
All 3,260 Integration and 2,384 main Components cases passed.

The sole failure was HostPlatformTestClassificationTests identifying the new
StorageCatalogUiOwnerTests fixture, which uses private files and real secret protection,
without its required host category. Adding `[Trait("Category", "HostPlatform")]` fixes CI
selection on Windows/macOS. The classifier itself and every test body are unchanged.
The two-case owning guard and fifteen owner cases selected with `Category=HostPlatform`
are the bounded repair confirmation; no additional production/shared-contract change
requires another broad execution. `stable-closure-decision.json` records that distinction.

Seven existing MemberData methods account for all 55 extra runtime cases. Plugin catalog
simulation expands from one descriptor to six cases; the two Memory cross-caller methods
each expand to five, and Memory ownership dimensions expand to nine. Unit floating-chat
settings boundaries and finalizer schemas each expand to eight; process tool routes expand
to twenty-one. Their existing data providers were inspected. The method-by-method comparison
finds no missing or unexpected method (`stable-discovery-reconciliation.json`).

The unsharded local test step took 150.41 minutes; restore/build/discovery brought the broad
gate to 152.99 minutes. This differs from current CI job scopes: Windows `stable` has a
120-minute budget for Unit/Memory and preceding gates, while Components/Integration use
three host shards with 180 minutes each. The local aggregate exceeds 120 minutes but is
not a measurement of either CI job. No timeout, exclusion or shard policy was changed.

## C# Architecture Gate Result

Status: Pass. Final validation and owned-resource cleanup are complete.

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Resolved | Unacknowledged routing was replaced during read-back | Two failing-first cases; final standalone suite passes | Routing merge requires acknowledged completion and unchanged field revision |
| Resolved | Delayed observation replaced a newer Test result | Two failing-first ordering cases; final standalone suite passes | Merge also requires the captured mutation revision |
| Resolved | Reopened view retained a stale pending receipt display | One failing-first actual renderer case; final standalone suite passes | Context-specific ledger notifications reach current sessions; retired sessions unsubscribe |
| Resolved | New Recovery component fixture lacked Web continuation registration | Initial failure followed by real AddCanDoItAllApi composition and passing passive test | No production ownership change |
| Resolved | Storage owner fixture lacked required host classification | Original Stable failure; final Release guard 2/2 and owner 15/15 pass | Required HostPlatform trait added; classifier and test bodies unchanged |
| None open | No remaining blocking boundary or effect-ownership finding | Responsibility map, inspected changes, real owner tests and negative dependency tests agree | No further action within this slice |

### Dependency direction

Contracts is a plain leaf. UI references contracts and BaseLib; sandbox references UI.
Workspace composes the owner and renderer; Infrastructure references neither the product
contract nor UI. The exact Core/API graph and watch comparisons pass. No service locator,
runtime DI injection, provider implementation or Recovery renderer is hidden in the leaf.

### Partial-class policy

No new partial service was introduced. Pure template/capability policy moved out of the
existing WorkspaceService storage partial and both legacy/production callers use it.
The broad legacy partial retains its non-UI public behavior. New writer, projection,
read state, draft and operation history types have distinct reasons to change. Nested
fault/fixture types are test mechanics, not production architecture boundaries.

### Testability proof

The 33 standalone cases instantiate real BaseLib descendants without runtime owners.
Negative tests reject forbidden/public/unresolved/cyclic dependencies. The 15 PostgreSQL
cases exercise real write stages and original-profile retention; the actual production
Settings host and seven browser cases establish reachable composition. Full product
build succeeds with zero warnings/errors, including the final restored-source Release build;
all 33 current light cases also pass in Release. Stable test build succeeds with 23 existing
analyzer warnings and zero errors. No new architecture guard was weakened.

### Closure decision

The bounded architecture and both application/acceptance matrices are accepted. The single
Stable failure is closed by its exact metadata repair and focused Release confirmation;
the original failed run remains recorded. Broader Workspace families remain deferred by
scope, not hidden behind this renderer. No further slice was started.

## Delivery and resource cleanup

The final product Release build has zero warnings/errors. The source checkpoint verifies
all changed executable/build/test files, retaining each earlier manifest and the exact
bounded follow-up deltas. The sealed handoff is unchanged. Components, FileTools and
SharedInfo are clean at their entry revisions; no sibling source or configuration changed.

The task-owned PostgreSQL container `candoitall-storage-ui-proof-20260929` and its sole
anonymous volume were inspected by exact ID, label and exclusive attachment, then removed.
All five recorded watch ports are closed and recorded temporary roots are absent. Initial
cleanup conservatively stopped when Windows reused old sandbox PID 41688; inspection found
an unrelated Playwright MCP process created after the sandbox had stopped. Its exact process
identity was reviewed and left untouched. The successful cleanup record retains this
distinction (`owned-cleanup.json`); the initial refusal is also retained.

Final maintained documentation validation passes for 296 Markdown files. The final 128 MiB
artifact scan passes: 292 text files, zero findings, zero oversized/unreadable text omitted
and 154 non-text exclusions. It includes the complete 117 MB integration TRX
(`secret-delivery-review.json`). Seventeen synthetic fixture-argument echoes in discovery/TRX
were reviewed against their owning tests and redacted with original/redacted hash records.
TRX outcomes, IDs and counters still parse and reconcile after redaction. No scanner policy
was weakened. Screenshots were inspected separately for layout and sensitive content.

Generated evidence remains ignored under the task artifact/output roots. The maintained
record carries commands, counts, interpretation and limits; the signed commit identity
and final worktree state are reported with delivery.

## Exact application filter appendix

Each filter below was inventoried from its owning source files and discovered/executed unchanged.
Storage unit dynamic data contributes 28 cases in StorageJsonTests; it is included in 258.

### api-unit

Project: `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`. Cases: 29.

```text
FullyQualifiedName~CanDoItAll.Tests.Unit.ApiAdministrationOutcomeTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.ApiAdministrationDurabilityTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.ApiCredentialRulesTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.ApiTokenRegistryTests.
```

### api-http

Project: `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`. Cases: 38.

```text
FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiAccessAuthorizationIntegrationTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiAccessContractTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiSectionPermissionsTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiSessionBoundaryTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiUserSessionIntegrationTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiAdministrationOutcomeHttpTests.
```

### storage-unit

Project: `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`. Cases: 258.

```text
FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StorageCatalogServiceTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StorageCatalogContractTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StorageJsonTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.DefaultStorageRoutingServiceTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StorageToolPolicyTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StorageAccessServiceTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Projects.ProjectAssetStorageServiceTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Projects.ProjectAssetCreationServiceTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.WorkflowStorageSpreadsheetAdapterTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StorageObjectResourceConnectorTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.ResourceStorageObjectPromotionTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StorageTransferPipelineTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StoragePlacementServiceTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StorageStablePlacementDriverTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.StoragePackageReferenceAdoptionTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.StorageRuntimePluginTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Projects.ProjectPackageStorageCatalogSnapshotTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.Projects.ProjectManagedStorageDeletionTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentChatExternalTargetAccessAttachmentTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentChatContextAttachmentFreshnessPolicyTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.MafContextToolAttachmentTests.|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.MafAgentRuntimeAttachmentTests.
```

### storage-consumer-components

Project: `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`. Cases: 55.

```text
FullyQualifiedName~CanDoItAll.Tests.Components.Workspace.StorageCatalogSelectionComponentsTests.|FullyQualifiedName~CanDoItAll.Tests.Components.Workspace.StoragePlacementRecoveryDialogTests.|FullyQualifiedName~CanDoItAll.Tests.Components.ProjectStructure.ProjectStructureTaskResourceAttachmentServiceTests.|FullyQualifiedName~CanDoItAll.Tests.Components.ProjectStructure.ProjectStructureTaskResourceGraphPersistenceTests.|FullyQualifiedName~CanDoItAll.Tests.Components.ProjectStructure.ProjectStructureAttachmentPreviewDialogTests.|FullyQualifiedName~CanDoItAll.Tests.Components.Shell.ResourceFileBrowsePaneTests.|FullyQualifiedName~CanDoItAll.Tests.Components.Shell.SettingsRendererTests.
```

### storage-consumer-integration

Project: `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`. Cases: 67.

```text
FullyQualifiedName~CanDoItAll.Tests.Integration.StorageCatalogContractPersistenceTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.Persistence.ManagedFilesStorageIntegrationTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.Persistence.ResourceStorageObjectIntegrationTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.ResourcePromotionOutcomeTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.ResourcesOwnerPersistenceTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.StorageStablePlacementPersistenceTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.Persistence.StorageMigrationIntegrationTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.SearchStorageOwnerPersistenceTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.MafStorageResultDisclosureIntegrationTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectStructureGeneratedImageAttachmentIntegrationTests.|FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectDeletionSharedStorageConcurrencyIntegrationTests.
```

### Production API component selection

Project: `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`. Cases: 11.

```text
FullyQualifiedName~ApiIssuanceCaptureTests|FullyQualifiedName~ApiTokenAdministrationTests|FullyQualifiedName~ApiUserAdministrationPanelTests|FullyQualifiedName~WorkspaceApiStatusTests|FullyQualifiedName~WorkspaceApiDenialTests
```
