# Large-desktop validation and measurable development loop

Supported new visual proof is 1920×1080 at 100% scale. A second genuinely large desktop viewport
is optional only for a concrete functional geometry case. Do not spend this task tuning mobile,
small or medium breakpoints. Do not remove existing shared-library responsive tests to hide failures.

Validate the entire filter form, advanced region, result actions, cursor controls, metadata and
nested content dialog. Hit-test important actions, verify tab/focus/Escape and scrolling, and assert
that content/footer/actions are visible rather than merely present in DOM. Use the actual shared
Components; do not rewrite grids/dialogs for screenshots. No fetch-on-focus/hover side effects.

Record before/after evaluated graph, watch set, SDK, source pair and host mode. Verify independent
source and publish assets, fonts, scoped CSS and imports. The complete History scenario must work
without native registrations or database. Prefer completing existing sandbox composition; justify
an extra host or leaf through actual graph and development usability, not an arbitrary project cap.

Measure three controlled Razor, C# and scoped-CSS edit→visible updates; JavaScript only if this slice
owns relevant JS. Separate initial build, graph load, server readiness, browser navigation/hydration
and warm edits. Restore all probe bytes/hashes, serialize builds/watch/tests, and record PID changes.
Distinguish same-PID hot reload from rebuild/restart, failed trials and latency variance.

PP2's five-project sandbox reports successful repeated hot reload. That neither proves every Web
edit faster nor retroactively cures PP1's historical second-update failure. Do not open a new toolchain
redesign unless an actual repeatable current regression blocks the selected slice. [S04]
