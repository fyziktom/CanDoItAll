# P31 — Canvas click and drag intent

Date: 2026-10-25. Scope: the reported crowded Project Structure node moving on
pointer-down and preventing double-click. This repair belongs to shared CanvasLib.
It does not change process behavior or close the remaining P28 runtime work.

## Cause and design

The real native page reproduced a 16.45 px horizontal and 131.71 px vertical jump at
63% zoom on pointer-down without pointer movement. Release returned the node to its
original position. The application had not committed a position change.

CanvasLib initialized `interaction.nodeIds` and immediately rendered. The collision
solver treated those IDs as actively dragged even while `moved` was false, changing
mobility, clearance and the resolved-layout key. Snapping also ran on tiny pointer
movements, allowing a click to turn into a committed drag.

The shared runtime now requires six CSS screen pixels of pointer displacement before
activating a node drag. A stationary hold never activates it. The threshold is independent
of zoom and does not impose a timer on intentional dragging. Collision treatment and
snapping begin only after activation. Activation captures the displayed layout before
setting the existing `moved` flag, rechecks editable and visible targets, and uses that
layout as the starting point. Surface changes before activation therefore do not reuse
stale positions or move a removed/read-only node.

Ordinary, grouped, frame and dependency drags use the same boundary. Subsequent moves
retain the existing frame-coalesced patch path. Neighbours stay fixed during dragging;
existing release overlap resolution still applies. A dependency click below the threshold
retains its relationship action. An activated drag clears click history; native and
legacy double-click handlers preserve the browser's slower timing without duplicating
the manual quick-double-activation path.

CanvasLib owns this gesture and layout behavior. `StructureWorkspace` still forwards
typed node movement/opening intents, and `ProjectStructurePage` retains concurrency
admission and persistence ownership. No new engine service, dependency, timer, endpoint,
per-pointer-move interop or project-specific condition was introduced.

## Changes and integration

- Components branch: `codex/canvas-node-drag-intent`, based on current `development`.
- Signed Components commit: `755655fa47d2275ba5feb401197533687320e23c`.
- Runtime edits: `01-foundation.js`, `05-viewport-and-events.js` and
  `07a-runtime-interaction-router.js` under CanvasLib's workbench runtime.
- Eighteen regression cases in `tools/canvaslib/drag-activation.test.cjs`, included in
  `npm run assets:verify`; maintained behavior guidance in `docs/canvas/node-drag-intent.md`.
- The engine CI Components pin selects that exact commit. Publish the Components branch
  before running engine CI remotely against this pin. No merge is part of this repair.
- Engine remains on `concurrent-mutations`. SPA remains unchanged on its colleague-review
  branch.

## Validation

| Check | Result |
| --- | --- |
| Pre-fix gesture regression | 4 passed / 11 failed, reproducing premature activation and click/drag conflicts |
| Final gesture regression | 18 passed, zero skipped; ordinary/frame/dependency, zoom/jitter, grouped/read-only, surface-change rebasing and both double-click paths |
| Components assets | `npm run build:tailwind` and `npm run assets:verify` pass; 56 Node cases total, including the 18 above; generated asset verifiers pass |
| CanvasLib Debug build | Pass, zero warnings/errors, using the repository's standard configuration and normal restore |
| Native reported node | At 63% zoom, pointer-down, 700 ms hold, 2 px diagonal jitter and release preserve the exact displayed position and leave manual position unset |
| Native double-click | Opens the governed screenshot preview; image load and rendered screenshot verified; preview then closed |
| Isolated Chromium drag, 1920 × 1080 | Actual served runtime, generic crowded nodes and recording callbacks; jitter does not activate/write, deliberate movement activates, neighbours stay fixed, seven coalesced drag patches, one release movement callback, no accidental open or script error |
| Native page after fixture | Original page remains open, no active interaction, reported node's manual position still unset; disposable fixture disposed and closed |
| Validation tooling | 6 baseline, 4 secret-scanner and 7 rendering-register cases pass |
| Rendering register | Pass; all 1,212 inputs unchanged |
| Full portability-static | Fresh scan of 10,177 tracked files; final no-write enforcement passes with 15,713 reviewed executable-source findings unchanged; no baseline edit |
| Documentation | 9 evidence-validator cases pass; canonical validator passes for 439 maintained Markdown files |
| Retained proof secret scan | 14 text files scanned, zero oversized/unreadable skips and zero findings; four non-text files excluded |

The first no-restore component build encountered a stale NuGet assets failure in
`ResolvePackageAssets`. A normal restore/build with the Components-pinned SDK succeeded;
no SDK/configuration/output-directory change was required. The 18 cases exercise real
layout and gesture functions with render/effect boundaries recorded; the real browser
proof complements those tests. A full repository .NET suite and consumer Release rebuild
were not run for this JavaScript-only behavior change.

## Live instance and resource closure

The ordinary 5032/7271 host was preserved. Running/WaitingOnTool execution queries timed
out during the pre-refresh check, so they were not treated as evidence of an idle host.
A restart was unnecessary: development static assets are served from source. HTTP
responses and loaded function bodies were checked, then the native page was reloaded
with browser cache disabled. The fix is active in that page. Other open tabs may need
a hard reload. Existing running .NET assemblies are not claimed to be rebuilt.

Components contract discovery was attempted through the existing local MCP bridge twice;
both bounded attempts timed out and their processes exited. The existing component
contracts, runtime, application handlers and real served assets were inspected directly.
No replacement MCP installation or new server/database/container was created.

Ignored local evidence and ownership records are under
`.artifacts/concurrent-mutations/canvas-drag-intent-20261025`: before/after measurements,
native screenshots, isolated browser driver/result, test/build logs and final static
scan. The browser fixture uses no application mutation callback. No temporary fixture
remains. Earlier P29 cleanup blockers are unchanged and are not claimed resolved here.
