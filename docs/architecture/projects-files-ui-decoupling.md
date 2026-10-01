# Projects Files rendering boundary

## Decision and ownership

The two former Files components mixed complete rendering with file-scope acquisition,
preview grants, native actions and asynchronous cleanup. P2 moves their complete markup
to `CanDoItAll.Projects.Files.UI`. Small immutable presentation records carry neutral
FileBrowser sessions, read-only FileInteraction content and callbacks bound to their
originating activation. The renderer does not resolve scopes or acquire authority.

The Projects module retains both original component entry points as effect hosts. A
feature-specific surface session coordinates the shared resource lifetime; the existing
pilot and portfolio coordinators retain scope resolution, and FileTools retains actual
authorization, grants, trusted paths and downloads. There is no new file registry,
HTTP boundary, universal controller, service locator or replacement browser.

The host stages an independently owned portfolio workspace instead of concurrently
mutating the accepted workspace through `UpdateAsync`. It preserves the selected source
and valid current location when accepting a replacement. Old callbacks, failures and
cleanup are fenced by activation identity, including A to B to A. Cancellation sources
belong to admitted operations and are disposed only when those operations finish.

P1's typed Files slot remains the composition boundary. Neither Projects.UI nor
Projects.Contracts nor the P1 sandbox gains a Files dependency. The new leaf references
BaseLib and neutral FileTools browser/interaction components. Its independent sandbox
adds only neutral synthetic providers and real registered viewers. Production module
and Web references point toward the new leaf, never back from it.

## Alternatives and proof

Keeping the markup in forwarding components would preserve the expensive build graph.
A generation check around the old mutable workspace cannot undo stale provider changes.
Serializing all refreshes would let a non-cooperating old resolution block future
selection. Independent preparation and identity-based acceptance avoid those problems
without changing FileTools session or authorization protocols.

Neutral tests render both leaves without the production module. Controlled production
host tests hold acquisition, grant release and actions, then verify actual sessions,
content access, source membership and disposal. Existing PostgreSQL page, admission,
package and deletion tests remain required. Browser proof uses 1920 by 1080 and checks
real viewer geometry, downloads and Agent/Workflow-produced files. Evaluated closures,
standalone publish, served assets and restored edit probes establish the build boundary.

The preview contract also carries the host's immutable neutral viewer composition.
FileInteraction does not discover that composition from DI: its explicit parameter must
be set. Both surfaces now use the same registered viewer policy as activation. The
sandbox renders actual Markdown and Mermaid instead of accidentally selecting built-in
plain text. Read-only mode and the 4 MiB content limit remain enforced.

Both Files renderers also give the existing action popover alternate positions when its
anchor is at the viewport edge. This local Tailwind rule and the generated application
asset keep all action labels visible; the shared FileTools component remains unchanged.
Browser proof requires the entire menu, not merely its Download button, in the viewport.

## Current renderer census and dependency proof

| Location | Remaining responsibility |
|---|---|
| Projects.UI: five renderers | Board/cards, hierarchy, overview/editor modal and deletion status; unchanged P1 ownership |
| Projects.Files.UI: two renderers | Entire Files dialog and portfolio pane, including actual browser and registered preview |
| Projects module: ProjectsPage | Route, profile, canonical reads/writes, package owner, navigation and Files composition |
| Projects module: two Files components | Effect-host entry points; the internal surface session owns accepted activation/resource lifetimes |
| Projects module: ProjectsAgentChatContextProvider | Original agent context and completion subscription |
| Each assembly's _Imports | Namespace imports only |

All 39 pre-existing UI/sandbox roots retain exactly their evaluated project closures and
package references. Projects.UI remains five projects; the P1 sandbox remains six.
Files.UI evaluates eight projects and its sandbox eleven. Whole Web changes from
164 to 165, Projects from 33 to 34; the full inspected graph changes from 181 to 183
projects. Cycle and missing-reference checks pass. The neutral assembly tests reject a
forbidden transitive owner, EF and unresolved edges.

The P1 watch set remains exactly 300 paths. Web changes from 4,597 to 4,606 paths;
the new Files sandbox watches 479. Tailwind's existing `@source "../src"` includes the
moved markup. No package version, shared Components/FileTools source or dependency
branch rule changed. Components development was already published and verified at entry;
an unmatched direct feature-branch CI run is still not a development-targeted CI run.

## Wider validation decision

This is a bounded feature checkpoint, not a release, merge or CI closure. The root
solution additions and three CI list additions register only the new isolated renderer,
sandbox and light test project. They do not change shared build anchors, SDK, package
versions, runtime registrations or existing protected closures. The new public Files
presentation records have only this feature's production/sandbox consumers. FileTools
authority, shared browser/session protocols, persistence/schema and common owners remain
unchanged. The preview-factory cleanup repair is confined to the Projects owner.

Those changes are bounded by current graph/assembly negative controls, explicit build
and owning selections, native authorization/lifetime tests, neighboring Workspace and
Resources tests, and real consumer browser journeys. They do not invalidate an otherwise
independent package contract under the bundle's named-trigger policy. Broad Stable is
therefore not repeated. Historical P1 Stable results are not represented as green proof
for P2. Portability-static final no-write enforcement remains mandatory and separate.

## C# Architecture Gate Result

Status: Pass

### Findings

No remaining architecture blocker. The reviewed choices and rejected alternatives are
recorded above; lifetime, owner, graph and browser results are in the
[execution evidence](projects-files-ui-p2-evidence.md).

### Dependency direction and partial-class policy

The rendering, view records, activation resource set, operation cancellation, native
workspace adaptation and guarded JS delivery each have a distinct responsibility. The
original components contain rendering orchestration only; no full renderer is hidden in
a forwarding leaf. No new partial class, service locator, runtime registration facade,
storage registry or authority implementation was introduced. Existing coordinators,
FileTools grants, trusted paths and action runner remain the actual owners. Independent
replacement avoids mutating a newer workspace through an old resolver.

### Testability proof and closure decision

Controlled unit, real-owner component and browser proof are separate evidence layers.
Neutral boundary tests include forbidden transitive dependency and unresolved-edge
negative controls. The bounded architecture may close; a change to authority, shared
session protocols or broadly consumed contracts would reopen the gate and test selection.

## Execution record

Entry: application `47651dd679a14e706a764a76ff84ba197a90ccb4`, Components
`4a858412d2c2a3f6123bf23d8c4584f05b47627d`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`; SDK 10.0.303,
source-reference mode, configuration `ProjectsFilesProof`. Entry graph: 41 roots,
181 evaluated projects. CodeAnalytics/Components MCPs were unavailable; evaluated
MSBuild and exact sibling source are the recorded fallback.

P1-R1 was reproduced in four native seed/delete cases, then all 14 mutation cases
passed. `AdmissionRefused` preserves the acknowledged save and old plan, blocks the old
draft and releases only its mutation slot. The catch is correlated to the exact native
pre-write admission; genuine unknown acknowledgements remain locked.

P1-R2 was reproduced through actual modal teardown and bUnit's renderer exception
observation. All 18 leaf cases passed, including seven interop lifetime cases. Pending
imports no longer stall teardown; late modules are released once and cannot validate or
save a successor. Unexpected active validation/cleanup failures remain diagnostic.

The pre-extraction Files desktop baseline passed: hydration 2.72 seconds, three Razor
samples 2.37/1.85/1.85 seconds and three C# samples 1.71/1.80/1.30 seconds. Probe hashes
were restored. The deferred family owns no separate CSS or JavaScript; the download
module remains in AppComponents. These warm local measurements do not promise a speedup.

P2 is complete with real production and independently published sandbox proof. The
supported desktop checks verify actual viewer content, read-only geometry, complete
action-menu visibility and downloaded bytes, including Agent/Workflow-produced assets.
The sandbox's measured hydration is 0.45 seconds; three Razor samples are
1.33/0.83/0.83, C# 0.68/0.71/0.68 and scoped CSS 0.69/0.63/0.48 seconds. Probe bytes
were restored. Final no-write portability enforcement passes.

The [execution evidence](projects-files-ui-p2-evidence.md) records exact selections,
mixed failures and their causal corrections, current binary/source/asset provenance,
desktop review, resource shutdown and remaining wider limitations. Raw evidence is
task-owned under `artifacts/projects-files-p2/20261001-47651dd6`. S0 and P2 close only
their bounded requirements; application release readiness and the inherited selector
and live-inference prerequisites remain separate.
