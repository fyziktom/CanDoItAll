# C# Architecture Gate

## Preparation Design Gate Result

Status: `Pass`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking at execution entry | FileTools SDK 10.0.301 unavailable; snapshot empty | `inputs/01-source-artifacts.md` | SB01 provision exact SDK or Blocked |
| Blocking before UI | Components MCP transport closed | two failed preparation calls | SB01 repair/retry; SB10 cannot enter without it |
| High | Existing unsigned managed-file endpoints are not authority | `ManagedFilesEndpointRoutes.cs`, `StorageJson.cs`, `Program.cs` | SB07 governed hardening |
| High | Project Structure/Processes/Projects owners are large/partial | inventory line counts/snapshot | focused owners, no new page partial, cleanup gates |
| Medium | Existing Infrastructure module cycle | snapshot Persistence <-> ControlPlane | do not worsen; fresh before/after proof |

### Dependency Direction

Target graph preserves Infrastructure independence and uses a small Integration.Abstractions plus outer Integration implementation to avoid reverse/cyclic module edges.

### Partial-Class Policy

No new partial is allowed. Project Structure file behavior must use top-level scope/coordinator/window types.

### Testability Proof

Preparation defines direct isolated seams plus host/component/browser layers and shallow-pass negatives. Implementation proof is pending per subbundle.

### Closure Decision

Bundle architecture may proceed to SB01. Each critical implementation/cleanup subbundle reruns this gate from actual code and proof.

## Execution Gate History

Append one result per architecture checkpoint with snapshot IDs, findings, repairs, dependency direction, partial policy, testability evidence, and progression decision.

### SB02 Native Browse Contract Gate — 2026-07-12

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Repaired | Initial contract file held multiple cohesive model categories in 674 lines | `snap-20260713015249-91e8d499` | Split primitives, budgets, models, drivers, and settings before closure |
| Info | Several domain records naturally expose 9-13 members | `snap-20260713015817-7f2dc30d` | No action; responsibilities remain singular and directly tested |

Dependency direction: no `.csproj` change; Infrastructure has no FileTools/Integration/module reference. No project cycle was introduced; the known Persistence/ControlPlane module cycle is unchanged.

Partial-class policy: Pass. No partial or nested architecture boundary was added.

Testability proof: direct registry, record, JSON, and catalog tests instantiate the new owners without Web, EF for pure behavior, or an existing broad runtime. Positive fake providers and typed negative cases pass.

Closure decision: SB02 may close. SB03/SB04 may use the native seam. Any provider that needs leaked SDK types, unbounded work hidden behind a page, or unsupported capability members reopens SB02.

### SB03 Filesystem Provider Gate — 2026-07-12

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Repaired | A deliberately shallow first implementation materialized all children before returning page one | `proof/SB03/transcripts/failing-first-bounded-page-one.txt` | Replaced with lazy provider-native bounded iteration before closure |
| Info | Browse driver exposes 18 focused operation members in 329 lines | `snap-20260713022023-d26717a4` | Accepted; path, cursor, and entry mapping are already separate cohesive collaborators |
| Info | Cursor state and path policy expose nine members each | `snap-20260713022023-d26717a4` | Accepted as cohesive serialized/path-policy contracts; direct adversarial tests cover them |

Dependency direction: no `.csproj`, package, or project-reference change. Infrastructure remains independent of FileTools, UI, Web, and modules. Scoped CodeAnalytics reports no dependency cycle.

Partial-class policy: Pass. No partial or nested provider boundary was added.

Testability proof: direct real-filesystem tests cover traversal, symbolic links, stale continuation, live replacement, cancellation, unsupported order, inspection limits, and redaction. A real 100,000-entry fixture proves 51 first-page and 101 second-page inspections without listing retention.

Closure decision: SB03 may close. The filesystem side of SB05 is unlocked after SB04 passes. Any later path leak, followed reparse point, stale content, provider cache, or O(total-children) page-one behavior reopens this gate.

### SB04 Remote Provider Gate — 2026-07-12

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Repaired | Existing IPFS constructed `HttpClient` per call and both remote reads buffered complete bodies | SB04 source diff and `proof/SB04/behavioral-proof.md` | Replaced with factory-managed HTTP and response-owned bounded streams |
| Repaired | Initial FTP transport reached the architecture large-file threshold at 363 lines | pre-cleanup `snap-20260713025027-d26717a4` | Extracted focused obsolete-request factory and RFC machine-list parser |
| Repaired at SB05 re-entry | IPFS listing parsed the complete bounded JSON envelope before page one | 10,000-entry production response review/test | Added incremental nested-array reader; page one inspects two entries and stops before body completion |
| Info | Browse adapters expose 17-18 operation members | final `snap-20260713025457-d26717a4` | Accepted for one cohesive provider operation; protocol transports/cursor/parsers are separate and directly fakeable |

Dependency direction: Infrastructure added only `Microsoft.Extensions.Http` 10.0.0 for handler pooling; no project/provider SDK reference. No FileTools/module/UI/Web dependency or scoped cycle exists. Final snapshot: `snap-20260713031012-d26717a4`.

Partial-class policy: Pass. No partial or nested provider boundary was added.

Testability proof: 16 focused cases run with direct fake transports or a recording HTTP handler and cover CID/MFS policy, revision change, FTP fact reliability, malformed input, cancellation, byte bounds, client reuse, lifetime-owned streams, and redaction. Optional live proof is supplementary and honestly skipped without environment credentials.

Closure decision: SB04 may close. SB05 is unlocked to review the complete Storage browse foundation. False remote capabilities, guessed FTP types, mutable/immutable confusion, raw secret leakage, or unbounded transport work reopens this gate.

### SB05 Checkpoint A Storage Foundation — 2026-07-12

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking, repaired | IPFS page one parsed the whole bounded listing envelope | 10,000-entry production HTTP test | Incremental nested-array reader now stops after page plus lookahead |
| High, repaired | Filesystem and remote cursor codecs duplicated cryptographic protection | source responsibility review | One internal cursor protector; typed provider codecs retain state/fingerprint ownership |
| High, previously repaired | FTP protocol transport exceeded the source-size gate | snapshots `snap-20260713025027-d26717a4` and `snap-20260713031012-d26717a4` | Request factory and MLSD parser remain focused owners |

Dependency direction: Pass. No project-reference change, FileTools/module/UI/Web edge, or scoped cycle. The sole SB04 package edge is `Microsoft.Extensions.Http` 10.0.0 for handler pooling; SB05 adds none.

Partial-class policy: Pass. Zero partial or nested provider boundaries.

Testability proof: direct contracts, path, provider, fake transport, production HTTP handler, parser, cancellation, redaction, 10,000-entry early-stop, and 100,000-entry filesystem tests pass without Web/full orchestration. Existing integration smoke also passes.

Final snapshot: `snap-20260713031012-d26717a4`, one project, 69 documents, 131 scoped types, 827 members, zero scoped dependency/cycle, zero Storage warnings.

Closure decision: unqualified Pass. SB06 is unlocked. Any contradiction in Storage confinement, freshness, bounds, capability honesty, protocol facts, or dependency direction reopens the owning foundation phase and this checkpoint.

### SB07 Authority And Effect Boundary — 2026-07-12

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking, repaired | Unsigned reference tokens and direct managed paths were sufficient content authority | failing-first host characterization | Fixed routes now require opaque handle header; unsigned references return 401 and managed paths return 410 |
| High, repaired | Initial revision check alone left an external-write window before filesystem commit | final mutation review | Expected revision is checked again after staging and immediately before replacement |
| Info | The scoped Web snapshot reports existing broad Web/Composition owners | `snap-20260713042852-baab347b` | No action in SB07; no changed security owner is large and later bundle gates own broad UI cleanup |

Dependency direction: Pass. Web -> Integration/Composition/Infrastructure; Integration -> Abstractions/Infrastructure. Infrastructure has no FileTools/Integration edge and no project cycle exists. The known Infrastructure Persistence/ControlPlane module cycle is unchanged.

Partial-class policy: Pass. No partial authority, registry, effect, route, or context owner was added. Largest integration security owner is 278 lines.

Testability proof: direct fake context/policy/storage/clock tests reject forged and cross-context handles before storage; real filesystem revision tests cover failure and commit; a real ASP.NET host covers authorized/unsigned routes; a zero-browser spy proves direct known-file independence.

Closure decision: unqualified Pass. SB08 is unlocked. Any later URL/path authority, stale context acceptance, log disclosure, or revision/overwrite bypass reopens SB07.

### SB08 Cache And File-Catalog Revision — 2026-07-12

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking, repaired | Direct `StorageBrowsePage` caching could not be deserialized by HybridCache | failing-first 4-test transcript | Cache exact bounded UTF-8 positional DTO bytes and reconstruct through domain validation |
| High, repaired | Initial key preprocessing did not cap every config/source/scope input | final performance review | Added 64 KiB config, 512-character scope ID, 256-source, endpoint/fingerprint, request-contract, per-partition, and global retention bounds |
| High, repaired | General integration DI unconditionally replaced placement registration | composition review | Moved placement revision to an explicit checked composition-root extension that rejects missing/ambiguous/custom registrations |
| Info | Affected types have cohesive member-count findings | `snap-20260713051010-baab347b` | Accepted; zero affected warning and every implementation owner is below 300 lines |

Dependency direction: Pass. HybridCache 10.0.0 exists only in Integration. Infrastructure/Abstractions/UI/Web are free of its types. Project graph is unchanged and the known Infrastructure module cycle is not worsened.

Partial-class policy: Pass. No partial cache, revision, DTO, policy, key, metrics, or producer owner.

Testability proof: deterministic fake driver/runtime/revision/time plus real HybridCache prove disabled calls, coalescing, isolation, bounds, expiry, cancellation/failure, stale-listing denial, and post-revision selection. Save/placement tests prove after-persistence publication; host tests prove checked production composition.

Closure decision: unqualified Pass. SB09 is unlocked. Any later cache-in-driver/UI, distributed fallback, unbounded input/value, stale authority, or failed-mutation revision reopens SB08.

### SB09 Checkpoint B Integration Backbone — 2026-07-12

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Revalidated | Long-lived Components and CodeAnalytics MCP transports closed | fresh direct installed-server calls | Both servers completed required calls successfully; direct JSON-RPC inputs are retained for repeatability |
| Baseline, isolated | Broad format command reports existing whitespace violations in `RuntimeHostServiceCollectionExtensions.cs` from line 249 | zero-context diff changes only line 4 and lines 68-69 | Do not create an unrelated 1,000+ line format rewrite; focused affected format passes |
| Info | Integration owners expose cohesive member-count findings | `snap-20260713052405-baab347b` | Accepted; no Integration warning, no owner above 300 lines, direct seams are tested |

Dependency direction: Pass. The five-project graph is Composition -> Integration/Infrastructure; Integration -> Abstractions/Infrastructure; Web -> Composition/Integration/Infrastructure. Infrastructure and Abstractions have no reverse edge. HybridCache remains Integration-only, and the sole cycle is the unchanged Infrastructure Persistence/ControlPlane module cycle.

Partial-class policy: Pass. No partial adapter, authority, cache, revision, endpoint, or composition owner. RuntimeHost contains only declarative registration calls.

Testability proof: all 435 FileTools tests, 79 affected main unit tests including the real 100,000-entry bounded fixture, and 8 HTTP-host tests pass. Direct known-file interaction resolves content with no browser service; cached stale listings cannot mint authority; aggregate revisions select new listings only after successful persistence.

Closure decision: unqualified Pass. Components catalog/recommendation, healthy managed watch, and persistent Playwright readiness are available. SB10 UI pilot is unlocked. Any pilot contradiction in package/static assets, mapping bounds, authority, cache, revision, or composition reopens the owning foundation and this checkpoint.

### SB10 Project Files Pilot — 2026-07-13

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking, repaired | Progressive search lacked typed duration, concurrency, match-count, and retained-byte limits | SB01 SB10 re-entry and 440 FileTools tests | Extended the package contract before UI consumption; the pilot declares and displays measured bounds |
| High, repaired | Single-source Compact FileBrowser stretched its navigation row through the dialog | first 1900x1200 screenshot and 45 component tests | Repaired the shared package grid contract; no host CSS override |
| High, repaired | Integration reduced the host-wide HybridCache key limit to 128 and faulted AgentFramework warmup | failing composition test and managed runtime log | Preserve host limits; validate FileTools keys locally and apply only cooperative minimum capability |
| High, repaired | Host positive minted Project authority for an invented scope and out-of-root occurrence | fail-closed 7/8 host rerun | Fixture now creates a real project and bound file; all 8 host tests pass without weakening revalidation |
| Info | Pilot owners remain below the large-file warning and have cohesive responsibilities | `snap-20260713072501-9c272781` | SB11 will decide whether physical file splitting improves ownership before broader reuse |

Dependency direction: Pass. Projects depends on Integration.Abstractions/Infrastructure and selected FileTools UI packages. Integration depends on Abstractions/Infrastructure. Infrastructure has no FileTools or module edge. Fresh focused snapshot `snap-20260713072501-9c272781` reports zero scoped cycles. The broad snapshot `snap-20260713072254-a584d4e1` reports only the prepared baseline ControlPlane/Persistence cycle outside this slice.

Partial-class policy: Pass. No partial or nested service was added. `ProjectsPage` owns typed open/close state only; `ProjectsBoard` owns a callback only; storage binding, browser coordination, activation, and lifetimes are top-level owners.

Testability proof: 23 direct integration/authorization tests, four Projects component tests, eight real HTTP-host tests, 45 FileBrowser component tests, and real Playwright production wiring pass. The 120-item fixture asserts inspected/returned/retained/rendered facts. The lifetime test disposes FileBrowser before content read and proves revocation only at interaction disposal.

Closure decision: unqualified Pass. SB11 is unlocked to perform Checkpoint C cleanup and extension smoke. Broader UI remains blocked until SB11 passes. Any synthetic authority, stale binding acceptance, unbounded search/state, browser-dependent interaction, host cache override, or package-layout regression reopens the owning phase and SB10.

### SB11 Checkpoint C Pilot Cleanup — 2026-07-13

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| High, repaired | The 266-line pilot cluster mixed Project binding, coordination, and lifetime ownership | source responsibility review | Split top-level binding and lifetime owners; coordinator is now 112 lines |
| High, repaired | Caller cancellation after authority grant could cancel cleanup and leak the handle | focused cancellation regression | Cleanup uses a non-cancelled token after grant; exact handle release is asserted |
| Moderate, repaired | Composite binding, occurrence activation, and item authorization allocated LINQ arrays/substrings on repeated paths | scoped performance scan | Replaced with single-pass selection and span checks while preserving fail-closed ambiguity |
| Info | Existing parent `ProjectsPage` is broad, but the pilot adds only typed dialog state/orchestration | source diff and owner table | No extraction required; 25 page lines and 11 board lines contain no browse/authority policy |

Dependency direction: Pass. No project/package reference changed in SB11. Projects continues to consume Integration.Abstractions/Infrastructure plus selected FileTools components; Integration points inward to Abstractions/Infrastructure; Infrastructure has no reverse FileTools/module edge. Fresh five-project snapshot `snap-20260713080121-9c272781` reports zero scoped cycles and zero findings in the six changed C# owners.

Partial-class policy: Pass. Binding, coordinator, and lifetime owners are separate top-level sealed classes. No partial, nested service, facade-only abstraction, or service locator was introduced.

Testability proof: 24 focused authorization/integration tests, five Projects component tests, eight real host tests, an independent ProcessRun-style source extension smoke, a post-grant cancellation cleanup regression, zero-warning Web build, focused format scan, and managed Playwright rerun pass. The browser is disposed before known-file content read, and interaction disposal alone revokes authority.

Component/UX proof: shared FileBrowser and FileInteraction owners remain unchanged; no host CSS override was added. Original accepted screenshots were inspected and the production route was rerun at 1900x1200 and 1440x900, including exact/no-result search, read-only handoff, overlay geometry, bounded rows, network success, and zero console errors.

Closure decision: unqualified Pass. SB12 is unlocked. Any later source that requires coordinator edits, cancellation-dependent cleanup, browser-owned content authority, parent-page policy growth, or host wrapper/CSS repair reopens SB10/SB11.

### SB12 Project Portfolio And Project Card Files — 2026-07-13

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking, repaired | The new aggregate pane inherited BaseLib Stack's start alignment, collapsing FileBrowser to a 2 px root and intercepting row input | first 1900x1200 browser proof | Stretch the focused pane and give FileBrowser an explicit bounded flex contract; final component/browser reruns pass |
| High, repaired | Broad Projects owners would have grown if card rendering and hierarchy/filter policy stayed inline | before/after inventory | Extract top-level `ProjectPortfolioCards` and pure `ProjectFileFilterProjection`; page 798 -> 782 lines, board 666 -> 583 lines |
| Info, accepted | Four new cohesive typed owners exceed the analyzer's low 9-member informational threshold | `snap-20260713091027-759c0917` | Keep directly tested typed records/coordinator/lifetime owners; none is a large-source or mixed-responsibility warning |
| Revalidated | A broad snapshot still sees the prepared Infrastructure Persistence/ControlPlane module cycle | broad snapshot `snap-20260713090844-8bf17eec` | It remains outside the scoped Storage/Integration/Projects slice; final focused snapshot has zero cycles |

Dependency direction: Pass. Comparing SB11 snapshot `snap-20260713080121-9c272781` with final snapshot `snap-20260713091027-759c0917` reports no project-reference or package-reference change in any scoped project. Projects has no Workbench/Resources source edge; Integration and Infrastructure retain their inward direction.

Partial-class policy: Pass. Projection, aggregate coordinator, workspace lifetime, cards, portfolio pane, and focused dialog are top-level owners. No partial, nested policy, service locator, broad facade, or `ProjectModalHost` growth exists.

Testability proof: 27 focused unit tests, 19 Projects component tests, 12 integration/host tests, zero-warning Release Web build, focused format, stale source/catalog/unauthorized/error negatives, and real desktop pointer/keyboard flows pass. Cards and Files receive the same directly tested projection instance.

Component/UX proof: exact Components `Tabs`/`TabsItem` and `CheckBox<TValue>` contracts were inspected and reused. Real `/projects` proof at 1900x1200 and 1440x900 covers shared filters, deterministic source revisions, exact/no-result search, card dialog, independent read-only handoff, Back reconstruction, action-overlay geometry, one actual scroll owner, zero lateral overflow, and zero unexpected primary-run console/network errors.

Closure decision: unqualified Pass. SB13 is unlocked to reuse the accepted project-scope semantics and neutral integration contracts. Filter divergence, stale source/location acceptance, reverse Workbench/Resources reference, browser-dependent content authority, or a focused-dialog/layout regression reopens SB12.

### SB13 Project Structure File Scopes — 2026-07-13

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| High, repaired | Scope resolver exceeded the 300-line owner threshold because it also held request and scope-key contracts | source responsibility review, 367-line initial owner | Extracted `ProjectStructureFileRequests.cs`; resolver is 295 lines |
| High, repaired | Process-run root semantics still lived in Workbench at the SB13 progression gate | ownership/source audit | Moved pure `ProcessRunArtifactRootPolicy` to Processes.Application; Workbench consumes the typed result |
| Revalidated | Direct image/PDF dialog could shallow-pass visually while constructing browser state | zero-call component spies and direct coordinator source audit | One authorized FileInteraction, zero FileBrowser/session/browse/search calls |
| Tool fallback | Fresh CodeAnalytics transport closed on two focused snapshot attempts | recorded MCP errors and `proof/SB13/behavioral-proof.md` | Used checked project-reference inventory plus successful full Release graph build; retry at SB14/SB17 |

Dependency direction: Pass. Integration.Abstractions has no project edge; Integration points only to Abstractions/Infrastructure; Projects points to Abstractions/Infrastructure/SharedKernel; Processes.Application has no Workbench/module edge; Workbench consumes the already-referenced Processes.Application policy. The full Release Web graph builds with zero warnings and no project cycle.

Partial-class policy: Pass. No new `ProjectStructurePage.*.cs` exists. The existing ToolWindows partial receives typed callbacks/state only; resolver, coordinator, browser workspace, window, and direct interaction are top-level owners. Source assertions find no `browse-files` branch in a page partial.

Testability proof: 54 focused unit tests, 28 focused component tests, one real authorized endpoint integration test, 2,584 broad unit passes with one unrelated seed-version baseline failure, zero-warning Release build, focused format/performance scans, and real two-viewport managed browser proof. Hostile absolute/traversal/stale metadata fails before provider I/O; direct asset spies fail any browser construction. The final component regression double-clicks a real FileBrowser row and proves the explicit `File open` state replaces the stale `Resolving` label.

Component/UX proof: `CanvasFloatingWindow` owns desktop move/resize/minimize/restore persistence; the focused window owns one browser results scroll area and explicit workspace replacement. Real 1900x1200/1440x900 proof covers project/node scopes, search/open, Include-subprojects revision, direct PDF/image dialogs, overlay clipping, hostile error/retry, and zero unexpected console/network errors.

Closure decision: unqualified Pass. The process-owned root-policy consumer progression smoke passes and SB14 is unlocked. Any path authority leak, direct-asset browser call, page-owned session, duplicated dispatch, new partial, overlay/scroll regression, or process-policy return to Workbench reopens SB13.

### SB14 Process Run Artifact Browsing — 2026-07-13

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking, repaired | Runtime scope validation found a singleton composite binding graph consuming the new scoped process binding source | managed Release startup failure and direct lifetime regression | Scope the binding provider, authorization coordinator, and browse-session factory; runtime and 43 affected unit tests now pass |
| High, repaired | The inherited process root policy accepted any relative path containing the current run ID | policy red-team tests and source review | Restrict authority to managed `artifacts/process-runs` and `output/.../process-runs` namespaces; reject traversal, absolute, wrong-run, and unrelated roots |
| Moderate, repaired | Initial bounded root validation used a dictionary double lookup and multi-enumeration LINQ | standard .NET performance scan | Use `TryAdd` and one bounded `HashSet` validation pass |
| Tool fallback | Fresh focused CodeAnalytics transport closed again | repeated installed-server `Transport closed` responses and `proof/SB14/behavioral-proof.md` | Use checked project-reference inventory, source assertions, scoped-lifetime proof, and successful full Release graph; retry at SB17 |

Dependency direction: Pass. Integration.Abstractions has no project edge; Integration points only to Abstractions/Infrastructure; Processes.Application has no module/Workbench edge; Modules.Processes points to Processes.Application and Integration.Abstractions but not Workbench. Workbench consumes the process-owned policy through its existing Processes.Application edge. The warning-clean full Web graph builds with no project cycle.

Partial-class policy: Pass. Root policy, neutral contract, current binding provider, coordinator, lifetimes, and dialog are focused top-level owners. No dashboard partial, nested policy, service locator, or facade-only abstraction was introduced. `LiveProcessesDashboard` gains only 32 lines of typed run-ID/open-close orchestration and contains zero FileBrowser/FileInteraction/storage-binding/session behavior token.

Testability proof: 17 final root/provider/coordinator tests, 43 affected lifetime/authority unit tests, 3 component tests, 8 real integration-host tests, scope-validation runtime startup, zero-warning Release Web build, focused format, performance recipes, source/dependency assertions, and managed two-viewport browser proof pass. Stale scope fingerprints and hostile roots fail before catalog/provider access.

Component/UX proof: `ProcessRunFilesDialog` owns loading, explicit error/Retry, current source reconstruction, browser workspace, and independent read-only interaction. Host cache and session retention are both Disabled. Real 1900x1200/1440x900 proof covers run-details entry, one managed current root, initial content, externally created file, replaced bytes after refresh, fixed chrome, sole result scrolling, and zero browser console errors/warnings.

Closure decision: unqualified Pass. SB15 is unlocked. Generic path inference, cached live roots, stale binding reuse, dashboard browser policy, singleton/scoped regression, or a Processes-to-Workbench edge reopens SB14.

### SB15 Resources External Storage And Promotion — 2026-07-13

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking, repaired | The current storage bootstrap updates `UpdatedAtUtc` during reads, so including that operational timestamp in the authority fingerprint made unchanged sources appear stale | integration characterization and catalog review | Fingerprint authority-relevant provider/configuration/credential state only; stale configuration tests remain fail-closed |
| High, repaired | Initial FileInteraction width repair passed an unsupported `Class` component parameter and failed only at live render time | managed browser console and component activation | Remove the unsupported parameter and style the rendered `.cdi-ft-interaction` root through scoped CSS; rerun both viewports with zero console errors |
| Moderate, repaired | Generic `InvalidOperationException.Message` could expose implementation detail through the Browse pane | final redaction review | Surface only typed provider/promotion messages; map all other exceptions to fixed safe text |
| Tool fallback | Fresh focused CodeAnalytics and Components calls closed at the installed MCP transport | recorded `Transport closed` responses and `proof/SB15/transcripts/source-architecture-audit.txt` | Use checked project-reference inventory, source assertions, direct hostile tests, and successful warning-clean full Release graph; retry at SB17 |

Dependency direction: Pass. Resources points to Projects, Integration, Integration.Abstractions, and Infrastructure. Integration source/project scans contain no Resources or Workbench edge. The full Release Web graph builds with zero warnings/errors and no project cycle.

Partial-class policy: Pass. Catalog, binding, coordinator, connector, promotion/writer, reopen, pane, and dialog are focused top-level owners. No new partial class or page business behavior was introduced; the existing page partial owns tab selection and registry refresh only. The unrelated memory snapshot provider was not reused or expanded.

Testability proof: 22 final catalog/promotion/connector/persistence/reopen unit tests, 3 component tests, one real PostgreSQL plus bootstrap-filesystem integration test, zero-warning Release Web build, focused format/diff/performance/source checks, and real two-viewport managed browser proof pass. Forged, stale, cross-actor, wrong-storage, persistence, cancellation, invalid schema, ordinary-save bypass, missing-source reopen, and duplicate cases are direct tests.

Component/UX proof: shared BaseLib Tabs/ListDetailShell/Dialog/Stack/Cluster/SurfaceCard/Button/Alert/Status components and packaged FileBrowser/FileInteraction own rendering. Real 1900x1200/1440x900 proof covers truthful group counts, promotion, governed registry, runtime-restart duplicate/reopen, actual content, sole source/result scrolling, bounded interaction, zero final console warnings/errors, and controlled resource cleanup.

Closure decision: unqualified Pass. SB16 is unlocked. Persisted handles/tokens, display-path authority, stale source acceptance, revision-before-save, editable governed identity, silent source omission, or browser-state-dependent reopen reopens SB15.

### SB16 FileInteraction View/Edit Migration — 2026-07-13

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking, repaired | Canvas still projected a legacy unsigned managed preview URL and requested it before the governed dialog | real 401 network request and graph projection review | Delete route derivation; retain media metadata and emit empty `MediaPreviewUrl` |
| Blocking, repaired | The immutable handle issuance revision rejected every later persisted revision, so a second save and conflict rebase could never succeed | live two-tab failure plus direct save-target review | Let the revisioned storage driver compare current storage truth; retain exact handle/context/operation and explicit Overwrite authorization |
| High, repaired | Managed Mermaid still diverted to the notes-only legacy viewer and the shared wrapper disabled HTML labels only inside flowchart configuration | real renderer DOM and direct Mermaid runtime probe | Route managed attachments into direct FileInteraction; publish Components.Mermaid 0.1.3 with root plus flowchart `htmlLabels=false` |
| High, repaired | Shared overlay Stack inherited grid/justify behavior and collapsed the interaction width | first 1900x1200 DOM geometry | Make the outer shared Stack own one `minmax(0,1fr)` column and stretch both axes |
| Tool fallback | Fresh focused CodeAnalytics and Components calls closed at the installed MCP transport | recorded `Transport closed` responses and `proof/SB16/transcripts/source-architecture-audit.txt` | Use checked project/package graphs, source assertions, direct tests, and the warning-clean full Release graph; retry at SB17 |

Dependency direction: Pass. Workbench owns the optional FileInteraction/Markdown/Mermaid UI packages. Integration references only Integration.Abstractions and Infrastructure. Infrastructure has no FileTools interaction, Components, Mermaid, or Markdig package. The full Release Web graph builds with zero warnings/errors and no project cycle.

Partial-class policy: Pass. Coordinator, policy, explicit renderer composition, Mermaid adapter, direct dialog, and authorized save target are focused top-level owners. No new `ProjectStructurePage.*.cs`, service locator, renderer discovery, nested service, or page-owned storage/authorization decision was introduced. The migrated tracked legacy owners shrink by 107 net lines.

Testability proof: 51 focused main unit tests, 16 component tests, two real PostgreSQL integration tests, 59 FileTools Core, 72 FileTools Components, 23 Markdown, and three Components Mermaid hardening tests pass. Direct spies reject FileBrowser construction; hostile Markdown/SVG/unknown/oversize cases, failure/cancel/edit-during-save/overwrite, replacement/disposal, sequential save, and two-session rebase are covered.

Component/UX proof: shared Overlay/BaseLib wrappers and the Components Mermaid wrapper own the surface. Real 1900x1200/1440x900 proof covers Markdown view/edit/preview/save/close guard, strict Mermaid, raster/PDF readiness, inert hostile states, conflict and explicit rebase, revision/API byte truth, full-width geometry, and zero final console warnings/errors or unsigned route requests.

Closure decision: unqualified Pass. SB17 is unlocked. An unsigned preview, browser-owned single-file lookup, active untrusted type, loose Mermaid HTML label, dirty-state loss, stale revision acceptance, ungoverned overwrite, package-layer leak, duplicate renderer path, or new page partial reopens SB16.

### SB17 Expansion Architecture Cleanup Gate — 2026-07-13

Status: `Pass`

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| High, repaired | The Project Structure page still owned known-file interaction replacement, cancellation, and disposal semantics after the expansion wave | before/after responsibility inventory and direct page audit | Extract sealed internal `ProjectStructureKnownFileInteractionSlot`; retain only UI feedback and orchestration in the page |
| High, repaired | Close/replacement could race an operation that had completed and disposed its cancellation source, causing cancellation to throw | lifecycle concurrency review and focused regression tests | Add completion-aware cancellation and disposal; replacement, superseded-open, close, and dispose tests pass |
| Revalidated | The expansion added references and UI packages across four stories | current project/package graph and source ownership audit | All six added project edges are acyclic and intentional; FileTools/Components packages remain in UI modules, not Infrastructure |
| Tool fallback | Fresh focused CodeAnalytics and Components calls closed at the installed MCP transport | recorded `Transport closed` responses and `proof/SB17/transcripts/source-architecture-audit.txt` | Use checked reference graph, source/package assertions, tests, and warning-clean full Web graph |

Dependency direction: Pass. Current reference inventory shows only the declared Integration/Integration.Abstractions expansion edges, no reverse path for any added edge, no Workbench/Resources edge from Projects, and no Workbench edge from Processes or Resources. Infrastructure retains its single existing boundary and the full Release Web graph builds without warnings or cycles.

Partial-class policy: Pass. No new partial declaration exists. The pre-existing Project Structure partial cluster receives no migrated known-file lifetime owner; the new 182-line sealed slot is top-level, directly tested, and has no DI interface, service locator, or runtime construction shortcut. The only added `GetRequiredService` call is composition-time alias wiring for a decorated concrete service.

Testability and scale proof: 123 affected unit tests, including the real 100,000-entry filesystem case, 61 affected component tests, 11 real PostgreSQL integration tests, a final 16-test lifecycle rerun after the race repair, zero-warning Release Web build, scoped format, source assertions, and the standard .NET performance scan pass. No optimization claim is made; bounded catalog/scope/provider materialization remains intentional.

Component/UX proof: package selections stay explicit per host and known-file dialogs construct no FileBrowser. Managed desktop checks cover Projects, Project Structure, Resources, accepted Process evidence, and a final-source Project Structure open/close/reopen/save/reopen lifecycle with one interaction root, zero browser roots, zero unsigned preview elements, clean console, and no legacy preview request.

Closure decision: unqualified Pass. SB18 is unlocked. Page-owned known-file lifetime, cancellation/disposal races, a new partial, reverse module edge, Infrastructure UI-package leakage, duplicate content/save/cache authority, browser construction for a known file, or cross-story desktop regression reopens the earliest owner and SB17.
