# P29 native Blazor closure review

Review target: signed engine checkpoint `c22afc56a`, Components
`14cf7b15fe0902cfc0b460e661aa30b809d03edb`, FileTools
`7885f6098e1bc0e202f8e2cc8ee26a9e89945af7`. Proof tier: Behavioral.

## Subbundle gate results

The existing compatible phase-card bundle retains its established input, plan, review and
validation roles. This is a manual semantic closure check of P29, not canonical structural
validation or final closure of the whole concurrent-mutations bundle.

| Gate | Result | Evidence and progression |
| --- | --- | --- |
| Process recovery | Pass | Generic planner repair; 40 selected regressions; real native rework completed Succeeded/Complete without escalation. |
| Native operation coverage | Pass within recorded Windows scope | [88-entry matrix](../validation/P29-native-create-action-matrix.csv), operation table, native receipts and independent canonical readback. Unsupported/external effects and clipboard verification limits remain explicit. |
| Concurrent editing | Pass | Real process/UI and floating-agent/UI overlap, both writes retained; stale note/file/Gantt submissions refused with retained drafts and explicit recovery. |
| Final window repair | Pass | Three native failing Reset/Hide sequences before correction; 15 overlay and 35 host cases pass; three final native sequences and minimize/expand/hide pass. A queued callback cannot affect a successor project. |
| Build and static proof | Pass | Standard Release Web and owning test build, exact 35-case discovery, portability 15,712 unchanged, rendering 1,212 inputs, documentation and secret checks. |
| Resource closure | Blocked | Automatic approval review rejected the exact user-authorized Python-folder retry with "blocked by policy". No deletion success or whole-bundle closure is claimed. |
| Branch constraint | Pass | Engine stays on concurrent-mutations; Components and FileTools retain their repair branches; SPA is clean at 1139939 on codex/concurrent-mutations and was not merged. |

The raw request's process recovery and concurrent native editing requirements are solved
by the retained runtime and mutation evidence. Native action validation is solved for the
documented applicable Windows lane; it does not assert every provider/runtime mode or an
unverifiable system-clipboard result. Incremental signed commits and maintained documents
are current. P28 activation, revocation, retry, capacity and unknown-work resolution retain
their separate open status. Reopen this slice if view-state/feedback identity, native
admission, overlay lifecycle or a pinned shared component changes.

## C# architecture gate result

Status: Pass with follow-up for this bounded behavior repair.

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Non-blocking for this patch | CodeAnalytics workspace health is incomplete because it promotes existing NuGet vulnerability warnings to workspace errors. | Snapshot snap-20261024175849-53f2b163 loads one project/406 documents with 57 WS0002 diagnostics, all NuGet vulnerability messages. Actual Release compilation succeeds. | Retain the package-upgrade/analysis-health follow-up; do not describe this snapshot as a clean global architecture audit. |

Dependency direction: the final patch changes no project references, composition,
registrations, interfaces or domain/persistence protocol. The existing routed Workbench
host owns view state and feedback; shared OverlayLib owns interop lifetime. Source review
adds no module dependency. The scoped snapshot reports one module and five type cycles;
without a clean full dependency load it is not evidence of global cycle freedom.

Partial-class policy: no new partial, layer, manager or type is added. The helper factors
the existing viewport update and three equivalent view-state assignments. Origin reuse is
conditional on the exact prior surface plus current selection, actor and lifetime. Save
feedback refreshes only presentation when its origin was current before the update. Other
surface or context changes retain their invalidation rules. There is no new database
roundtrip, global lock or exception suppression.

Testability proof: controlled OverlayLib callbacks prove late geometry cannot reopen a
hidden or disposed window, while normal/minimized geometry and callback failures retain
their behavior. Native-host tests exercise real admitted persistence and recreated project
lifetimes, not only renderer flags. The shallow fix of dropping all stale-action checks
would fail successor-project, selection A/B/A, forged-target and recreated-lifetime cases.
The host source contains no fixture identity, product-example branch, TODO or stub added
by this repair. The final native dependent flow also passes Canvas/Gantt navigation.

## Browser validation analytics

Target: 1920 by 1080 desktop. Canvas/Gantt remain the primary surfaces; toolbox, index,
inspector, health and signals remain optional floating support. Existing count badges and
editor/dialog composition are unchanged. Canvas owns pan/zoom; each long floating body
owns its scrolling, with header actions outside that scrolling body. The inspected final
canvas screenshot retains readable controls and bounded overlay bodies. Floating windows
can overlap each other by design; Hide and Minimize release the underlying surface. The
inspector's constrained column remains readable. Minimize reduces Signals from 470 to
about 60 pixels and Expand restores it. A maximized canvas must be docked to reach its
underlying view tabs. Two final round trips render five Gantt rows and the same dependency,
then return to Canvas without page errors or host lifetime exceptions.

Relevant normal, dialog and asset-preview screenshots and the final open-window screenshot
are retained under the owned ignored proof root. Native dialog and asset findings are
recorded in [the validation record](../validation/P29-native-blazor-validation.md).

## Final commands and proof ownership

The ordinary 5032/7271 application uses its retained development database. Automated host
tests use only the separate PostgreSQL 18 loopback fixture via a privately supplied test
connection. Discovery matched the declared 35 cases before execution:

```powershell
dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release --no-restore /m:1 /nodeReuse:false
dotnet build tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj -c Release --no-restore /m:1 /nodeReuse:false
$filter = 'FullyQualifiedName~ProjectStructureGraphLifetimeTests|FullyQualifiedName~ProjectStructurePageActionLifetimeTests.Insights_'
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj -c Release --no-build --no-restore --list-tests --filter $filter /m:1
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj -c Release --no-build --no-restore --filter $filter /m:1
```

The first test build exposed a missing namespace import in the repaired fixture; after
that test-only edit, its assembly was refreshed with BuildProjectReferences=false against
the already-current references. The final 35-case run passed. No unfiltered test project
or solution run was necessary for the local lifetime and view-state contract. Earlier
unrelated checks were reused only where their source and pinned dependencies were unchanged.

The signed implementation, evidence, scoped acceptance and remaining cleanup blocker agree.
Manual testing may continue on the retained ordinary host; no deployment or merge follows
from this review.
