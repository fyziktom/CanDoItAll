# Components decoupling closure

CD1 is **in progress**. This record is the current closure register; historical family
receipts and the shared bundle retain their original scope and results.

## Current decision

`CanDoItAll.Application.UI` owns the Dashboard, Runtime Capabilities and safe database
profile presentation. It depends on the existing AppComponents and BaseLib presentation
closure. Web retains routes, snapshot/cache/timer policy, capability probes, database
profile authority, navigation, startup gating and application context. The application
root and framework recovery remain native.

The Dashboard's immutable display records move into the light assembly; consumers are
explicitly migrated. They are application-only values, not an HTTP serialization contract.
The clock remains native and supplies the render timestamp. Activity selection stays in
the host. Existing identity-based link construction is a neutral route contract, protected
by the real Home page assertions. No cache or polling service is added to the renderer.

Runtime and database models cross the seam as narrow, safe display projections. Their
native enums continue to determine authority and readiness; display text never determines
permission. Database activation carries the rendered profile identity. Native admission
and opening lifetime must protect repeated callbacks and late completion.

A single application UI library is justified by independent rendering of these application
operations. AppComponents remains feature-neutral; Configuration.UI remains a schema-field
renderer. A view-controller framework, new transport, second cache and backend redesign
are unnecessary. No new partial classes will be added to the native hosts.

## Composition and proof

The Dashboard keeps compact header statistics, four quick actions, six project cards and
two activity tabs. Runtime keeps the full capability card and its real CapabilityFact
child. Database profiles keep the Wide dialog, safe copy controls, status distinctions and
stable footer. Utilities and status retain their current compact shell composition. The
existing AppShell body owns page scrolling; dialog bodies own their own overflow. Desktop
proof targets 1920 × 1080, with constrained desktop geometry for flyouts and overlays.

Characterization includes HomePageTests, DashboardSnapshotCacheTests,
DashboardSnapshotLoadRunnerTests, DashboardSnapshotLoaderTests, RuntimeCapabilitiesPageTests,
MainLayoutDatabaseProfileTests, MainLayoutTopBarTests, MainLayoutRetirementTests and
MainLayoutCollaborationTests. Independent component tests must exercise the actual leaf
without Web registrations. Browser journeys must prove native integration, publication,
assets and cross-family shell behavior. Expected discovery is derived for each exact lane
before execution; historical results are never counted as fresh execution.

The root solution registration and shared shell composition require the final broad Stable
gate. Its current serial aggregate invocation follows Testing's canonical local command.
Completed assembly receipts are retained. Static portability and maintained-documentation
checks are also mandatory; source census and package verification alone do not close them.

## Entry evidence

- Application: `663fb1391e4acb12439173f1cb1d287fb1f24685`, clean
  `components-decoupling`; only CD1 package additions since review
  `fd9e43547457eb18901185da47de123efdfd8108`.
- Components: `f3745356182444656edfdef56aa827095632d3ea`; FileTools:
  `3a080ecd31068a77c1e1bd639f7a78e21c93db85`; both clean.
- SharedInfo: `30330978cf48d702382663a3d5231880fc034936`; SDK 10.0.303,
  Windows, Release, sibling source dependencies.
- CodeAnalytics snapshot `snap-20261011111130-dacba1c1`: inventory reports 215 projects,
  4,684 documents, 208 product and seven supporting test projects. Build/dashboard
  responses exceed the transport limit; the persisted snapshot was inspected locally.
  Its 788 diagnostics comprise 602 duplicate generated/top-level type warnings, 95 partially
  interpreted DI factories, 27 unresolved DI registrations and 64 truncated diagram notices.
  No workspace-load failure is reported. The 12,384 types, 92,312 members and 47,003 type
  relationships support the census; ambiguous DI and dynamic rendering require source and
  runtime checks. Diagram truncation is not used as a completeness certificate.
- The optional lexical collector read 1,119 entries with zero unreadable inputs. Dynamic
  activation, public types, evaluated references and assets require complementary review.
- Baseline Web build passed in 123.8 seconds with one existing nullable warning in
  StructureWorkspace.razor. CD1 package verification and its 34 helper tests passed.
- Local GPG challenge signed and verified with fingerprint
  `96E836FAA8854EE98ABC10903C206549E1D7EAD6`; commit verification follows each checkpoint.

## Dashboard and readiness checkpoint

The actual Home and Runtime Capabilities routes now consume the independent application
renderers. Existing native cache, timer, failure, cost and capability meanings are retained.
The focused native selection discovers and passes 41 cases across HomePageTests,
DashboardSnapshotCacheTests, DashboardSnapshotLoadRunnerTests, DashboardSnapshotLoaderTests
and RuntimeCapabilitiesPageTests. The independent suite discovers and passes 14 cases,
including actual capability children, separate views and direct/transitive/unresolved
dependency negative controls. Both owning test assemblies were rebuilt in Release.

The entry Web executable, built before production edits, reached HTTP 200 at
`/settings/runtime-capabilities` in 8.33, 9.20 and 9.27 seconds (three launches, the same
seeded private PostgreSQL 18 database, warm filesystem). This measures server startup,
not interactive circuit readiness or edit-visible time. An earlier probe used an incorrect
URL and timed out; that failed attempt is retained separately. No speedup is claimed.

## Current closure census and application mapping

The authoritative [source register](components-decoupling-register.json) and
[execution evidence](components-decoupling-evidence.json) accompany this document. The
preceding checkpoint counts retain their historical meaning. Current focused results are
28 independent application cases, 41 native Dashboard/cache/Runtime cases and 21 native
shell/admission/lifetime cases. Root and Stable Release builds passed. The final canonical
Broad Stable run is required by the root project and shared-shell changes and is running.

| Rendering owner | Retained native responsibility |
| --- | --- |
| `Application.UI/Dashboard/DashboardSurface` | Home routes, profile identity, cache/clock/timer, refresh/read admission, failure and retirement |
| `Application.UI/Runtime/RuntimeCapabilitiesSurface` and `CapabilityFact` | Existing snapshot provider and complete native capability-to-display mapping |
| `Application.UI/Profiles/DatabaseProfileDialogSurface` | MainLayout startup acknowledgement, profile authority, selection and activation for the next process |
| `Application.UI/Shell/ShellUtilitiesSurface` | Actual utility actions, recent-profile reads, navigation and captured target admission |
| `Application.UI/Shell/ShellStatusBar` | Workspace/runtime status and context; the real HelpPopover remains in the render tree |

The new safe records do not expose an editor, connection string, credential-bearing profile
or native owner through an object parameter. `DatabaseProfileIntent` carries opening and
profile identity. MainLayout rejects stale/busy callbacks, retires before asynchronous close,
does not cancel an admitted activation and distinguishes pending restart from the actual
running profile. Unknown outcomes block resubmission until a new opening reads authority.
Late outcomes cannot reopen or alter a successor. The activation coordinator and schema did
not change. Public Dashboard C# types were explicitly migrated; no HTTP contract changed.

The register contains 1,184 reviewed inputs: 342 extracted renderers, 291 native hosts,
98 framework hosts, 16 compatibility entries and 437 imports/build/assets/non-rendering
inputs. Scenario rows are labelled separately from product effects. Each row includes its
normalized digest, type/project, caller/route, native/visual owners, descendants and resolved
family asset/scenario/evidence references. No active REMAINING or UNCLASSIFIED renderer is
being deferred. The prior 247-row agent census has 186 matching normalized digests, 61
changed inputs and no missing files. Changed descendants were reviewed against current
source; old results were not promoted to fresh execution.

Ordinary C# rendering, component partials, dialogs, deferred slots, routes and registrations
were reviewed alongside native Razor bodies. The important indirect activations are:

| Activation | Current disposition |
| --- | --- |
| ConversationShellHost / DynamicComponent | Native contributor/window composition. Agent and Simple Chat contributors retain native session/content owners and use existing light conversation children. |
| WorkspaceSettingsRendererHost | The current WorkflowSettingsRendererSource selects the native image-generation adapter, which consumes its light settings surface. Executor/plugin schemas add no visual registration. |
| Agent chat/activity/attachment slots | Native controllers retain streams, authorization and effects; actual AgentChatSurface, AgentActivitySurface and attachment children remain light. |
| Capability/team/editor dialogs | Native adapters retain admission/save/result conversion; complete capability, metadata, member, editor and icon-picker children remain in the existing libraries. |
| Workflow canvas/settings | WorkflowCanvasSurface owns rendering; prompt-picker and native settings slots retain their declared effects. |
| Workbench deferred content | ProjectStructureDeferredSurface owns render admission and fragment lifetime; Canvas/inspector/execution renderers are already extracted and accepted outcomes stay native. |
| File interaction composition | Existing FileTools presentation contracts select preview children and their declared assets. |
| CRM/HR view contracts | Existing narrow view/read ports remain valid; complete descendants stay in CrmHr.UI without another extraction or backend DTO mirror. |

ProjectEventsCalendar and ProjectStructureCanvas have no current source/dialog/contributor
activation and retain their compatibility role. The 14 older agent compatibility entries
retain their reviewed unreachable disposition. Components.Git contains only an assembly
marker; its Git dependency is not an independently renderable feature boundary. Its README
now says so. These explicit decisions do not exempt an entire native directory.

## Evaluated graph and guard

All 199 projects under src were evaluated in Release source mode, plus the new application
UI and scenario host in package mode: 201 project/mode tuples, zero missing direct project
inputs. Actual Compile/Razor/content items, converted sibling references and packages were
recorded. Family entries include references, available static-web-asset manifests and test
projects. Package evaluation is not package-runtime certification; the new source and published
paths have fresh browser proof. Fast/Parity modes apply only to families defining those modes.
The repository's configured BaseLib 0.3.0 is absent from the configured public NuGet feed
(read-only version-list check retained). Directory.Build.targets defaults to mandatory sibling
source mode. Package publication/runtime certification is therefore an external release task,
not a passed CD1 runtime lane; no substitute prerelease or local package override was installed.

UI runtime closures and compiled public parameters/descendants were inspected. New boundary
tests reject direct, transitive, unresolved, public-native-parameter and hidden dynamic-child
violations. Existing neutral contracts are not confused with native implementations. The
marker-only Git project is not claimed as a light renderer.

Final CodeAnalytics snapshot `snap-20261011130319-b8328ec3` covers 217 projects, 4,702 C#
documents, 12,405 types, 92,427 members, 53,803 member relationships, 47,037 type relationships
and 532 DI registrations, without workspace-load errors. Its 728 diagnostics comprise 606
generated/duplicate-type ambiguities, 95 partially interpreted DI factories and 27 unresolved
registrations. Persistence and Mermaid exports were not requested for this rendering analysis.
The response exceeded transport limits; the persisted snapshot and digest were inspected locally.
The refreshed application-contract snapshot `snap-20261011125935-47ab8cf8` covers 10 C#
documents, 25 types and 135 members without diagnostics, but omits resolved references/generated
Razor in that scope. Evaluated/compiled/source/browser evidence supplies those missing dimensions.
Final impact selection uses 66 actual diff seeds/ranges and the Stable and Playwright workspaces.
It returns Low confidence, incomplete coverage and AllSuppliedSuites: Stable has seven load
diagnostics and ambiguous generated/unknown source relationships; the scoped Playwright index
contains 217 source tests without its resolved project closure. This cannot narrow the native
gate. The current full Stable invocation and manual cross-family browser selection supply the
executable coverage. The browser selection is explicitly bounded; no full-browser-suite pass
or current whole-Web binary reuse is claimed.

The CI rendering-review guard requires explicit review for changed/new/stale inputs, valid
classification, owner/rationale fields and resolved family references. Seven positive/negative
controls pass. It has no automatic baseline-write command. Its bounded lexical discovery is
not a claim of semantic/reflection completeness; current source review and runtime gates remain
required. All maintained family links are indexed from the UI README and architecture index.

## Current browser and development-loop evidence

Independent source and Production-published scenarios pass all Dashboard states, activity tabs,
capability children, eight profile states, real clipboard, flyout and status popover. The native
profile case clicks actual activation, reads pending/runtime authority, restarts the owned OS
process and confirms the new runtime profile. DOM, clipboard and server logs exclude its fixture
password. No profile trace was produced or claimed as inspected.

Source and published native Dashboard cases pass persisted project/run identities, partial usage,
projection lag, controlled owner-read failure, retained totals, retry, back navigation and the
Runtime utility/refresh. The temporary owned-database table rename is restored in finally.
Seeded run history is data proof, not workflow execution. Expected server read errors are retained;
page-error and failed-asset collections are empty in both cases.

The cross-workspace selection discovered 21 cases: 20 passed initially; one old Workbench test
expected pre-PC4 dialog dismissal. It now verifies the retained original intent, closes explicitly
and still requires Completed, 100%, recorded step counts and the generated child under the exact
workflow node. Its targeted rerun passed. This is a composite current result, not an invented
fresh green 21-case invocation. Cases cover Workbench/canvas, native workflow execution and process
authoring, uploads, Projects/Files, CRM/HR, Prompts, Memory, Resources, Scheduler, Plugins, TestLab,
Workspace/Data Sources, Collaboration and Agent/Simple Chat/floating conversation integration.
External model boundaries use declared loopback fixtures, not paid/live-provider certification.

Reviewed screenshots include the 1920 by 1080 native stale Dashboard, runtime cards and desktop
profile dialog, and the 1100 by 760 published dialog. Labels/icons/controls are readable; cards
and overlays do not overlap; the dialog footer stays visible and inside the viewport. The original
wide dialog, including its open space, is preserved. Application and dialog bodies retain their
separate scroll ownership. This is presentation preservation, not a redesign.
The final source/published rerun also proves Tab stays inside the real modal and Escape closes it.
It waits for CopyButton's actual `data-copy-state=copied` before comparing the native clipboard
value. The earlier asynchronous predicate admitted a Promise before the copy finished; the
failed attempt remains in evidence. All three final application browser cases pass.

Current Debug watch inventories contain 704 sandbox and 4,929 native inputs. The managed sandbox
applied changes in the same browser document: Razor 4,174 ms, restore 353 ms, C# scenario 444 ms,
restore 58 ms. C# scenario data required its normal selection action; that interaction was not
reported as compilation time. The installed backend parser remained ChangeDetected after the SDK
reported successful updates. Failed waits and browser outcomes are retained separately; installed
MCP and sibling work were preserved. Automatic approval rejected the native CLI benchmark initially;
the operator then explicitly authorized the prepared script. The native Debug host applied Razor
in 5,746 ms and its restore in 3,457 ms; a C# projection edit applied in 3,276 ms and restored in
3,327 ms. The browser observed Razor in 3,853 ms and its restore in 1,563 ms; compiler completion
and DOM timing measure different intervals. C# values appeared 192 ms after the normal Refresh
capabilities action, separately from compilation. The same document and host process survived.
An earlier wrong Refresh locator is retained as a failed invocation. The native SDK also reports
Missing Asset manifest warnings, despite the separately passing actual asset requests. Both owned
watch hosts are stopped and temporary probes are restored exactly. The same PostgreSQL fixture
and standard outputs were reused. No feature-owned scoped-CSS or JS implementation was introduced;
fresh source/published browser cases exercise the actual shared assets.

## Next-refactoring handoff

Stable presentation contracts are the boundary for the next agent. Native data/persistence
authority, profile/project admissions, writes, operation receipts, orchestration, provider
execution, routes and composition remain deliberate owners. Preserve captured targets, drafts,
independent reads, accepted/unknown/rejected distinctions and retirement when refactoring them.

| Separate work | Current owner and safety floor | Acceptance before changing it |
| --- | --- | --- |
| Authoring-aware portable history transfer/export | Native Process authoring/persistence and transfer services retain refusals when history cannot be preserved. | Lossless identity/history/receipt transfer and compatibility/recovery proof before removing refusals. |
| Large catalog paging | Existing native query/catalog owners and bounded selectors. | Stable ordering and selection across pages, honest loading/error states and durable save/launch targets. |
| Backend/module/concurrency ownership | Existing application/persistence owners, admissions and receipts. | Correct dependency direction and actual committed/unknown/rejected behavior, including two views and delayed outcomes. |
| Wider release/platform/live-provider certification | Release/CI and external-provider owners. | Their exact platform/deployment/migration and permission/budget gates; CD1 does not certify them. |
| Watch status parser | Shared MCP owner; unrelated local work remains preserved. | Recognize current SDK messages and reconcile status with the persistent browser without concealing failed waits. |

No additional generic component-extraction wave is proposed. Final validation, signature ancestry,
startup timings and owned-resource disposition must be recorded before the global completion state changes.

Portability review at this checkpoint moves two existing `/processes/live` lexical findings
with the Dashboard and adds the documented route and shell code-fence findings. These are
route/documentation text, not process discovery or elevation. Both portability tool suites
pass and full no-write enforcement passes with 15,383 reviewed executable-source findings.
The final source, shell, browser and broad gates remain required.

## Shell and profile checkpoint

The actual MainLayout consumes DatabaseProfileDialogSurface, ShellUtilitiesSurface and
ShellStatusBar. Safe display projections exclude the native credential-bearing editor.
Profile callbacks carry the rendered opening and profile identities. The native owner
rejects duplicate admission and stale selection, retires closed openings before awaiting
browser storage, preserves an accepted activation through close/disposal, and requires
explicit canonical readback after an uncertain acknowledgement. Successful activation
reports pending restart without replacing the running database identity.

The independent assembly now discovers and passes 28 cases, including real nested dialog,
copy, row, status and utility controls, public native parameter rejection and an actually
rendered forbidden DynamicComponent negative control. The native shell filter discovers
21 cases and passes all 21 after rebuilding Release. The first attempt passed 19 and
failed two new tests because a rerender retired the DOM click handler between lookup and
dispatch. Atomic renderer-dispatch interaction corrected the tests; assertions and product
behavior were preserved. Five tests use the real activation coordinator with a controlled
admission pause or synthetic lost acknowledgement, and actual PostgreSQL profile readback.

## C# Architecture Gate Result

Status: Pass for the implemented boundary; final validation remains in progress.

### Findings

No blocking architecture finding remains in the implemented application seam. Existing native
effects were retained; display bodies were moved rather than copied. No universal controller,
provider bag, extra cache, polling loop or speculative factory was introduced.

### Dependency direction

Web consumes Application.UI; Application.UI consumes the existing AppComponents/BaseLib
presentation closure. Evaluated references and compiled public/descendant negative controls
agree. CodeAnalytics reports no project cycle and no application cycle. Its three global
module cycles and 17 type cycles belong to existing backend/primitive structures; the filtered
dependency response still includes global cycle data, which is not silently discarded.

### Partial-class policy

Existing MainLayout partials retain native admission and lifecycle. No new native partial was
added, and the old display partial was removed. The leaves are independently rendered Razor
components consumed by real routes and shell slots.

### Testability proof

The 28 independent cases render actual children without native services. The 21 native shell cases retain
real profile activation/readback and delayed or retired outcomes. The 41 Dashboard/Runtime cases
exercise actual owners and identity-bearing links. The browser lanes cover real process restart,
publication and every affected shell family; loopback model fixtures are labelled as such.

### Closure decision

The component architecture may be consumed by the next refactor. Global CD1 completion waits for
the running broad Stable gate, comparable final startup samples,
documentation/signature receipts and removal of the owned PostgreSQL fixture after its last use.
PC4's captured launch, retained-origin recovery and role-resource corrections remain unchanged.
