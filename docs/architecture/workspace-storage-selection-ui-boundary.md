# Workspace Storage selection UI boundary

Execution record for the compatible external Behavioral
[Storage selection bundle](../../codex/bundles/CanDoItAll_Workspace_Storage_Selection_UI_Decoupling/prompt.md).
Entry: `8bbb9e29e32887de98d567eddb68277627cb67c7` on `components-decoupling`, clean.
Its only difference from reviewed implementation `cbb135c7c8d76ff50a624c142f12faf8c9b55f91`
is the 40-file input bundle. Historical packages and shared foundation remain sealed input.

Status: S0 and P1-P5 are completed; implementation and validation are closed. Workspace is not
fully decoupled: Data Sources, placement Recovery and residual configuration hosts remain.

## Environment and ownership

Windows, SDK 10.0.303 (pin 10.0.302/latestPatch), sibling source mode, Parity assets.
Configurations: `StSelProof` for production/browser proof, `StSelLight` for independent
component suites. Builds sharing outputs are serialized. Read-only siblings:
Components `f258ab6a959a97fa16c01d0858e7dc122728a11a`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`, SharedInfo
`83e21e23bcf43d92b061a6d367ac385241d13cd3`; all clean at entry.
CodeAnalytics, Components and dotnetwatch MCP were unavailable. Current-branch source,
actual sibling contracts, evaluated restore/watch graphs, build-backed discovery,
CLI watch and real Playwright are the explicit fallback.

Private PostgreSQL 18.6: container `cda-storage-selection-proof-20260929`, exact ID
`d43324985f3ab70800832efca7a14d5503b630f612494f3df5b946ee4967f671`, label
`codex.task=workspace-storage-selection-ui`, ephemeral loopback port 50222, 2 GiB / 4 CPU.
Each host owns disposable control-plane, vault and file roots. No ordinary application
on port 5032, retained database, external Storage driver or live LLM was operated.
Ignored local receipts: `artifacts/workspace-storage-selection-ui`; screenshots:
`output/playwright/workspace-storage-selection-ui`.

## S0 reproduction and current consumer inventory

SCAT-R1: six real CatalogSurface row cases discovered/executed; four failed and two
retry controls passed before the fix. Same-row activation replaced the acquired draft
during ordinary editing and held Save/Test/read-back. The current acquired, non-deleted
same-target draft now no-ops before a new read. Name, raw order/FTP port, validation,
EditContext, wizard step and receipts survive. Failed, missing, deleted and unacquired
same-ID selections still acquire again. Different-target/New and A-to-B-to-A behavior
remain in the retained suite. All 39 catalog UI cases pass after the fix.
Production same-row interaction also passes with unsaved name/raw order and validation
retained, the original three receipts and unchanged persisted name.

The current reference scan found one direct rendered consumer: AgentDetailsDialog,
Workspace Tools. The source adapter/registration and original eight picker tests are
the other compiled seam consumers. Runtime callers use their existing Storage owner
contracts. Before moving code, the original eight cases and actual Agent child Apply /
parent Cancel / explicit Save / reopen journey passed. The Agent's captured-session
callback, parent persistence and access normalization remain with their existing owners.

## Boundary and lifecycle decision

| Boundary | Responsibility |
|---|---|
| StorageSelection.Contracts | Immutable display-safe metadata, profile/generation stamp, read-only source |
| StorageSelection.UI | Actual field, chooser, local staging/search and exact read/dialog lifetimes |
| StorageSelection.UiSandbox | Bounded scenarios, two independent parent fixtures, reset/failure/held reads |
| Workspace adapter | Existing catalog read/bootstrap owner, minimal safe projection and profile retirement |
| Agent host/owner | Parent draft/EditContext, captured-session guard, explicit Save and persistence |
| Existing Storage runtime | Catalog grants, disclosure, read-only policy, routing and driver admission |

No fourth Presentation layer, generic chooser framework, service locator, administrative
mutation ledger or new durable write protocol was introduced. The real AppComponents
ResourceCardPicker/SelectedReferenceTable and BaseLib dialogs are reused. Their neutral
closure is intentional; no shared component was copied or moved. The old five Workspace
renderer/model files were removed. Workspace references Contracts only; Agent directly
composes the new UI. Its broader Workspace reference remains for unrelated consumers.
Foundation, MAF, AppComponents and protected Workspace leaves gain no reverse edge.

The production field receives the actual parent session cancellation token. Intent
captures normalized IDs, AllowAll/Disabled and source context; each request has its own
linked lifetime. Equal normalized echoes preserve staging. Changed selection, policy,
parent or source retires the original intent, including away-and-back changes. Late
results, failures, CatalogsLoaded callbacks and finally cleanup check the exact original
operation. Cancellation and DialogReference close only the owned child, never CloseAll.

Apply requires an acquired current snapshot, rejects direct calls while loading/error/
retired, and validates returned IDs again in the field. Empty/unknown IDs cannot be
introduced. Missing/disabled saved IDs remain removable; removed disabled IDs cannot be
re-added; enabled read-only catalogs remain selectable without granting write authority.
AllowAll/Disabled preserve the explicit list. Cancel, Escape, backdrop, retirement and
disposal emit no new list. Source failures preserve IDs and expose safe retry; retained
old labels carry an explicit stale-details warning. Empty success and actual missing
references remain distinct from unavailable metadata.

Read counts: initial empty field 0; saved labels once; equal echoes, local search and
toggles 0 extra; each chooser open and explicit retry 1. Details and chooser refresh are
separate bounded lanes; a newer chooser snapshot retires an older details read. No
picker action invokes catalog Save/Test/Delete, Agent Save, file launch or driver APIs.
Existing bootstrap may update trusted-root validation/updated timestamps on a read;
integration checks that behavior, exact remote snapshot preservation and unchanged
routing. Safe projection omits configuration, credentials, runtime capabilities and raw
health exception messages. URI userinfo/query/fragment are removed, including FTP query
text that .NET otherwise treats as path text. Logs contain context and exception type.

## Discovery and execution ledger

For each row, build-backed `dotnet test <project> -c <configuration> --list-tests --filter
<filter> /m:1` precedes same-filter execution with `--no-build --no-restore` and TRX.
Filters use `FullyQualifiedName~` on each listed term, joined with `|`. Counts below are
expected = discovered = executed; final results have zero skips. Leaf suites use
StSelLight; application/browser suites use StSelProof. BaseLib is the sibling test project.

| Evidence prefix / project | Filter terms | Final passed |
|---|---|---:|
| selection-closure / selection UI | CanDoItAll.Tests.Components.WorkspaceStorageSelectionUi | 30 |
| catalog-final / catalog UI | CanDoItAll.Tests.Components.WorkspaceStorageCatalogUi | 39 |
| api-final / API UI | ApiReadLifetimeTests | 11 |
| core-final / Core UI | WorkspaceFileIdentityTests, WorkspaceHistoryAndShellTests, WorkspaceDraftTests | 42 |
| resources-final / Resources UI | ResourceEditorReadinessTests | 10 |
| application-components-final / main Components | AgentDetailsDialogSettingsTests, AgentDetailsDialogDeletionTests, AgentDetailsDialogCapabilityTests, AgentDetailsDialogAvatarGenerationTests, AgentDetailsDialogThinkingEffortTests, AgentDetailsDialogProjectStructureAccessTests, ResourceCardPickerTests, SelectedReferenceTableTests, SettingsRendererTests, StoragePlacementRecoveryDialogTests | 58 |
| application-integration-final / Integration | StorageSelectionAdapterIntegrationTests, AgentEditorAdapter, MafStorageResultDisclosure, ManagedFilesStorage, ResourceStorageObject, WorkspaceCoreOwnerOutcome, ApiAccessContract, StorageCatalogUiOwner, StorageCatalogContractPersistence | 58 |
| application-unit-final / Unit | StorageSelectionProjectionTests, StorageRuntimePlugin, StorageToolPolicy, StorageAccessService, DefaultStorageRouting, SettingsRenderer, HostPlatformTestClassification | 51 |
| baselib-dialogs / BaseLib | DialogNavigationOwnershipTests, DialogModuleLoadRaceTests | 19 |
| application-browser / Playwright | WorkspaceSettingsBrowserTests, WorkspaceApiDenialBrowserTests, StorageCatalogBrowserTests.Production_wizard, StorageSelectionBrowserTests, StorageSelectionSandboxBrowserTests | 5 production pass; 2 sandbox initially fail |
| sandbox-browsers-final / Playwright | StorageSelectionSandboxBrowserTests | 2 repaired pass |
| adapter-final / Integration after FTP repair | StorageSelectionAdapterIntegrationTests | 2 repeat |

This is 325 distinct final passing cases across selected scopes, not an unfiltered
repository pass. The two adapter reruns and prerequisite/baseline reruns are not added
to that distinct count. The selection leaf preserves all eight original semantics and
adds 19 lifecycle/handler cases and three boundary guards. Actual compiled assembly/
public-type and restored graph closure is checked; forbidden, unresolved and cyclic
fixtures fail. Filesystem boundary checks retain HostPlatform classification.

Failed attempts remain explicit. Main component discovery found 58 rather than initial
55 because the settings term also selects three Workflow image settings cases. Unit
discovery initially omitted the classification class; corrected source inspection found
two real classifier methods, distinct from embedded fixture attributes, giving 51.
Neither mismatch counted as execution. A new integration test had a snapshot/summary
compile mismatch and then incorrectly expected bootstrap timestamps never to change
(57 pass, 1 fail). Corrected owner-aware assertions pass all 58 without owner edits.
Unit initially passed 50/51 and exposed the FTP projection leak; the final 51 and the
two registered-adapter cases pass after the bounded repair.

Initial browser/bUnit failures exposed readiness assumptions (queued selection events,
disposed bUnit instance, prerender click and dialog initial-focus completion), repaired
by waiting for actual state. The combined browser run passed all five production cases
but both sandbox caret assertions raced the dialog's initial focus. Both now wait for
the real native dialog focus, assert caret position before/after ArrowLeft, and pass.
Assertions were not weakened. Prior successful sandbox runs are not substituted for
these repaired final runs. Existing unrelated build/analyzer warnings remain in logs.

## Application non-regression matrix

| Row | Result and evidence |
|---|---|
| A-START | Pass: private Web Settings/Agent routes, real registrations; affected-root build receipt below |
| A-AGENT-STAGE | Pass: child Apply/parent Cancel leaves persisted IDs empty; explicit Save stores exact IDs, AllowAll=false; reopen resolves saved catalog |
| A-AGENT-STATE | Pass: real Agent component preserves EditContext, unsaved instructions, permissions, nonempty capability selection, serialized project access, secrets and read/write flags; zero save before parent Save; browser persisted read-back |
| A-ACCESS | Pass: Storage runtime policy/allowlist and canonical MAF disclosure tests deny/filter other catalogs, restrict empty lists and deny read-only writes without dispatch |
| A-LIFETIME | Pass: cancellation-ignoring reads, policy/selection/parent A-B-A, equal-ID source change, real adapter profile retirement, two independent browser parents |
| A-CATALOG | Pass: SCAT-R1, 39 catalog UI cases, 15 real catalog owner and four persistence cases retain original health/routing/captured input behavior |
| A-BOOTSTRAP | Pass: registered adapter preserves trusted-root refresh, exact remote snapshot/routing; UI counts prevent render/search bootstrap loops |
| A-CORE | Pass: Files identity, Core history/draft, Resources readiness, ten Core owner outcome cases, routes and Providers redirect |
| A-API | Pass: 11 current-denial/cleanup cases, five API contract integration cases and real production denial/retry |
| A-FILE | Pass: ManagedFilesStorage, ResourceStorageObject and MAF tests read harmless owned content through canonical grants; default routing retained |
| A-DEFERRED | Pass: passive Data Sources route and real Recovery open/close; six Recovery component cases and settings renderer/fallback tests; no transfer/external dispatch |
| A-GRAPH | Pass: evaluated protected graph/watch equality, new runtime/restore closure and reverse-edge guards |

Validation mapping: V-S0-01..04 → S0 proof; V-SE-01..14 → original/lifecycle UI,
projection and real dialogs; V-PR-01..05 → Agent, adapter and runtime evidence;
V-BD-01..05 → boundary guards, graph/watch comparisons, build and CI membership;
V-UI-01..04 → real browser proof below; V-UI-05 → measured loops;
V-CL-01..04 → application matrix, repository gates and cleanup.

## Graph/watch and browser proof

Evaluated dgspec/assets include sibling package-to-source replacement, packages and
native assets. Watch inputs come from `dotnet watch --project <host> --list
--configuration StSelProof`. All graphs have zero unresolved edges and cycles.

| Host | Projects including root, before → after | Packages | Native assets | Watch inputs, before → after |
|---|---:|---:|---:|---:|
| Core sandbox | 8 → 8 | 1 | 0 | 285 → 285 |
| API sandbox | 5 → 5 | 1 | 0 | 263 → 263 |
| Catalog sandbox | 5 → 5 | 1 | 0 | 258 → 258 |
| Web | 157 → 159 | 140 | 30 | 4557 → 4561 |
| Selection sandbox | new → 17 | 2 | 0 | new → 690 |

Protected graph and watch sets are exactly equal. Web adds only the two new leaves,
replacing five old watched renderer/model paths with nine new paths; packages/native
assets are unchanged. Selection uses Markdig 1.1.2 and framework Internal.Assets
10.0.11 through the real neutral components. Its closure includes BaseLib/Common/
Canvas/Overlay, FileTools UI/core abstractions, SharedKernel, RecordBrowsing,
AppComponents and Conversations.Components. No fixed five-project target was assumed.

Source and fresh separately published DLL hosts run in Development and Production,
respectively, without database/vault configuration. Real parent/child dialogs exercise
search, caret, keyboard Space/ArrowLeft, Apply/Cancel/Escape/backdrop, notes, policy
toggles, large catalogs, missing/disabled/read-only IDs, failure/retry and held old reads.
Real fonts/styles, clipboard module and no page/console/HTTP/circuit errors are checked.
The source has at most four held reads and an explicit fixture-only persistence label.

Inspected 1600×1000 images: production-agent-selection, source-large, published-nested,
published-large-applied, published-failure and published-two-owners. The production field fits the Agent's existing
Workspace Tools subpanel and keeps its existing parent scroll/footer. In the sandbox,
the primary surface is the bounded results list; search and actions remain reachable,
no horizontal body overflow occurs, long names/IDs wrap, and only results scroll for
500 rows plus a missing reference. The selected-reference table works in its narrower
content column. Parent/child top-layer order, child-only closure and second-parent
survival are verified. No dashboard/stats row or new feature CSS/JS was introduced.

## Broad Stable and architecture review

No new unfiltered Stable pass is claimed. Named candidates assessed were root solution/
test membership and replacement of the existing scoped read-source registration. These
add leaf projects and CI membership without changing build policy, generic components,
canonical factories, common fixtures, durable owner protocols or runtime composition.
The UI read seam has one inventoried production consumer and does not replace Storage
runtime contracts. The bounded application matrix and affected-root builds define this
closure. Changes to shared owners, canonical factories, generic components, fixtures or
cross-cutting DI would invalidate it and require a newly named broad gate. The previous
slice's mixed Stable receipt and focused repairs are not counted as fresh proof.

## C# Architecture Gate Result

Status: Pass.

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| None | No bounded architecture blocker | Actual production consumer, evaluated/runtime closure and negative tests | None |

Dependency direction: contracts point inward; the old renderer responsibility is removed;
each new source/intent/renderer/fixture has one owner. Razor code-behind partials are
ordinary component halves, not new runtime partial clusters. Independent real-renderer
tests and negative lifecycle/graph cases establish testability. Production registration
and the actual Agent route use the new seam. Nested as well as direct forbidden,
unresolved and cyclic fixtures reject invalid graphs. Closure may proceed for this slice.

## Final loop, gate and cleanup receipt

The seven final direct `dotnet build <root> -c StSelProof --no-restore /m:1` builds pass
with zero warnings/errors: Contracts, selection UI, selection sandbox, Workspace,
AgentFramework, Composition and Web. Root/test solutions and all three actual CI
component lists include the new leaf projects/tests. No product project references tests.

### Development-loop samples

Startup ends only after opening a real chooser, toggling and applying a selection.
Razor samples change the actual Choose button; C# samples change the executed catalog
option label. All times are milliseconds; individual samples are retained.

| Host / receipt | Startup | Razor samples | C# samples |
|---|---:|---|---|
| Original Web / original-web-complete | 52922.513 | 1908.792, 1878.729, 1894.620 | 1367.421, 2448.789, 1699.438 |
| Extracted Web / final-web | 50197.397 | 1918.276, 2913.427, 1889.996 | 1023.328, 2277.938, 2070.439 |
| Independent sandbox / selection-sandbox-complete | 11405.561 | 3950.726, 573.498, 532.574 | 537.437, 433.412, 515.118 |

The sandbox's normal watch refresh closes its fixture parent. Its complete samples
therefore wait for the watcher update, explicitly reload the browser and reopen the
parent/chooser before asserting the visible marker. These numbers include that work;
they do not claim preservation of a fixture draft across development refresh. The
`parentReopenedAfterRefresh` field in raw samples records additional recovery by the
poller, not this planned reload/reopen step. Web samples kept their original open editor.
Neither run establishes a general full-Web speedup or a cold-build benchmark.

Failed/partial measurements were preserved: first original Web startup 50961.245,
Razor 1913.092/2914.116/1900.978, then an overly broad C# replacement caused restart;
second startup 47615.273, Razor 2919.695/1906.398/2920.742, then a Windows mapped-file
write failed. The probe now changes only the executed constructor argument and retries
the exact reversible write. First sandbox startup 12575.346 then its refreshed route
removed the parent; second startup 11497.263, Razor 3517.450/300.235/340.852 then a
chooser action raced refresh. The explicit post-update reload/reopen completed all six
samples. No failed attempt is presented as a complete measurement. Finally blocks stop
only owned watcher trees and restore exact source bytes; final SHA-256 comparisons pass.
There is no feature-owned CSS/JS. Shared styles, fonts, dialog and clipboard assets are
proved in source and separately published Production hosts.

### Repository gates and sensitive-data review

- Package validation: 40 files, five JSON documents, 57 local links, 41 sources and
  39 manifest entries; 11 package tests and 14 shared-tooling tests pass. The package
  and shared foundation remain unchanged. Compatible-shape semantic entry/closure
  applies without a canonical schema rewrite.
- Complete proposed-tree portability: 7,378 files scanned, 32,712 findings, no truncation.
  The final tracked scan includes all newly staged files. The policy's existing 52
  large/binary exclusions are reported; no new source is excluded. Six baseline-tool
  and four secret-tool tests pass. Four added fingerprint groups and one stale group
  were reviewed: two display-name sorts moved out of Workspace, and two README matches
  are the PowerShell code-fence label. They are not filesystem case assumptions or
  elevation commands. The inspected baseline diff has 15,208 allowances; final
  enforcement without `--write-baseline` passes with zero delta.
- Documentation evidence: nine cases pass. Canonical documentation validation passes
  for 301 maintained Markdown files, including the new project READMEs and this record.
- Secret review includes the complete staged source diff (new files included) and
  171 artifact text files, with a 60 MB ceiling, zero oversized/unreadable text files.
  The scanner reports 15 matches: 14 synthetic projection-test sentinel occurrences
  (including discovery/TRX encoding and source) and one `string.Empty` password
  assignment in the redactor. Every match was checked against exact source and recorded
  in `secret-review.json`: zero actual credentials and zero unexplained findings.
  This is an explicitly reviewed result, not a zero-finding scanner pass. Its three
  excluded helper source/project files were also read; their code generates disposable
  fixture configuration rather than embedding credentials. Eleven PNG files are outside
  text-scanner coverage; representative normal/error/overlay images were inspected.

### Cleanup, work-unit closure and remaining Workspace scope

Exact container ID, name and task label were verified before removing only the owned
PostgreSQL container and its anonymous volume. No task-labelled container remains.
All seven owned watcher receipts resolve to no running process and no retained fixture
root. Browser/source/published hosts are disposed by their owning fixtures. The final
ordinary-port check found no listener on 5032; no action targeted that port. Components,
FileTools and SharedInfo remain clean at the revisions above. The original bundle has
no diff. Local ignored proof logs and screenshots are retained deliberately.

| Work unit / raw input | Closure |
|---|---|
| S0 / SCAT-R1 | Solved: failing-first real row proof, minimal owning-session guard, retry and production regressions |
| P1 / actual selection inventory | Solved: current sole rendered Agent consumer, baseline browser/read and graph/watch proof |
| P2 / leaf boundary | Solved: Contracts/UI/sandbox, direct production wiring and evaluated protected directions |
| P3 / lifetime and authority review | Solved: exact parent/source/request guards, handler negatives, safe retry/projection and unchanged runtime authority |
| P4 / real sandbox | Solved: actual components, two parents, bounded late reads and independent Production publish |
| P5 / application and closure matrices | Solved: 325 distinct final passing cases, builds, browser, loops, mandatory gates and safe cleanup |

Completed Workspace slices: Settings Core, API Access, Storage catalog administration,
and Storage selection field/dialog. Still deferred: Data Sources/profile activation and
transfer, placement Recovery/continuation, and residual configuration hosts. Their
production registrations remain active. No next slice was started. Delivery is a signed
local commit containing this record; no push, merge, deployment or signing bypass.
