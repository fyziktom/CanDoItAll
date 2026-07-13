# Architecture Checkpoints

Each checkpoint runs `csharp-architecture-review-gate` and records its result in `bundle://reviews/csharp-architecture-gate.md` plus the execution report. A Fail reopens named owners; “cleanup later” is not a progression result.

## Checkpoint A — SB05 Storage Foundation

- Responsibility: native browse contracts/registry/settings are separate from existing read/write/delete responsibilities.
- Dependency: Infrastructure has no FileTools/Integration/module/package edge; no new cycle.
- Provider model: new provider uses registration, not registry/switch edits; duplicates fail.
- Testability: browse/provider behavior is directly tested without `StoragePlacementService` or Web host.
- Scale: returned page bounds also bound inspected entries, metadata calls, retained state, and cancellation; no full enumerate/sort/hash before page one.
- I/O: remote transports reuse connections, stream response content, and enforce length/range budgets without file-sized bridge copies.
- Performance audit: actionable findings in `analysis/03-dotnet-performance-audit.md` are fixed or disproved by measurements; new hot-path anti-pattern scan is recorded.
- Partial/size: no partial driver boundary; shared path/transport collaborator has one reason to change.
- Cleanup: remove duplicate path/URI/transport mapping introduced by implementation; preserve existing behavior.
- Unlock: SB06 only after both a filesystem and a fake remote provider exercise the native contract.

## Checkpoint B — SB09 Integration Backbone

- Projects: Abstractions/implementation boundaries and package graph match `architecture/02-csharp-dependency-direction.md`.
- Composition: declarative registration only; no `IServiceProvider` in behavior, no `BuildServiceProvider`.
- Security: effects cannot execute from unsigned token/path/browser item; handle is actor/runtime/operation/revision bound.
- Cache: Disabled pass-through and scoped key/revision proof; distributed mode fails closed.
- Endpoint: explicit authorization current-context proof.
- Tests: isolated behavior without Web host plus composition/endpoint smoke with host.
- Intent split: authorized known-file interaction is independent from FileBrowser construction; collection browsing alone owns browser/session state.
- Unlock: SB10 only after fresh dependency/cycle proof and one authorized content-open downstream check.

## Checkpoint C — SB11 Pilot

- Parent page/component responsibility did not grow materially; session/content/dialog ownership is focused.
- Components MCP choices/examples are documented or the phase is blocked.
- Project filter/scope logic is directly testable outside Razor.
- Browser proof uses the real adapter/handle/content source, not a fixture-only provider.
- Scale proof uses a large deterministic source/fake and structural counters; the browser renders only bounded accepted state.
- UI: one scroll owner, open overlay, no clipping/layering, loading/empty/error/result states, desktop viewports only.
- Cleanup: delete pilot duplication/dead branches and strengthen names/types/tests before expansion.
- Unlock: SB12 only on Pass.

## Checkpoint D — Per Story SB12-SB16

Before each story closes:

- no new page partial or broad manager;
- domain scope owned by correct module;
- no forbidden project edge;
- directly tested scope/coordinator/component state;
- host effects reauthorize;
- collection stories initialize FileBrowser only for browse intent; known-file opens initialize FileInteraction directly;
- no regression in provider/search/content structural counters or scoped performance scan;
- story-specific positive/negative and desktop browser proof;
- next similar source/type can use the seam without editing a monolith.

## Checkpoint E — SB17 Expansion

- Before/after line/member/responsibility inventory for all touched hotspots.
- Project/module dependency and cycle review.
- Duplicate preview/open/save/source/cache logic removed.
- Project Structure image/PDF double-click remains direct FileInteraction with zero browser calls, while browse actions remain collection-based.
- Selected FileInteraction packages/renderers only; unsupported formats explicit.
- Workbench process policy points inward to Processes; Projects does not reference Workbench/Resources.
- No new partial; any temporary bridge has SB18 removal/deprecation decision.
- Full affected test and cross-story browser smoke.
- Repeatable performance/scale envelopes remain within the accepted structural budgets.

## Final Checkpoint — SB18

- Same architecture claim is supported by source, project references, tests, packages, browser, and proof.
- Every earlier foundation remains trusted after final observations.
- Existing Persistence/ControlPlane module cycle is unchanged; no new project/module cycle.
- Architecture gate result is Pass; no “Pass with follow-up” for required scope.
- Representative 100,000-entry/fake-transport/direct-known-file performance regression proof passes.
