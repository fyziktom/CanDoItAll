# DotNetWatch MCP Improvements

## Highest-value improvements

1. Detect hot-reload/browser divergence explicitly.
   - `RevisionConfirmed` and `Hot reload succeeded` were not enough to trust the browser state.
   - The server should expose a stronger post-hot-reload readiness state or a warning when health remains `Pending` after hot reload success.

2. Tighten the meaning of app health after watch changes.
   - In the observed divergence case, watch reported success while app health still showed `Pending`.
   - The health model should not leave the session in an ambiguous state after a supposedly successful nearby edit.

3. Add stale-slot cleanup or stronger slot ownership checks for atomic publish.
   - Atomic retry failed with `Access to the path 'CanDoItAll.ComponentKit.dll' is denied.`
   - The server should proactively detect and release stale published slot processes before the publish step reaches file-copy failure.

4. Make session preemption and resume more transparent.
   - Backend-managed build resumed both the published app and the watch app into new session ids.
   - The behavior works, but it is harder to follow than necessary when measuring flows end to end.
   - A clearer summary of what was stopped, what was resumed, and why would reduce cognitive load.

5. Offer a first-class "fresh watch restart" command.
   - The stale-DOM issue was resolved by a full watch restart.
   - That action is common enough to deserve a dedicated helper instead of a manual stop/start sequence.

## Nice-to-have improvements

1. Report content-volume savings directly.
   - The operation log tool already knows raw and surfaced counts.
   - It would help to also show total text size reduced so context savings are obvious.

2. Surface likely root-cause classes in watch failures.
   - Example classes: Razor parse failure, hot-reload applied but runtime stale, health endpoint unavailable, published slot lock.

3. Distinguish page-owned layout from shell-owned layout in UI validation helpers.
   - The projects board fit the viewport, but the whole document still scrolled because of the shell's dev-only `Tuning Mode` panel.
   - A viewport helper that can target a specific test id would make layout measurements easier to interpret.

## Overall judgment

The MCP workflow clearly improved developer focus during noisy build/watch cycles, but it still needs stronger truthfulness around hot reload and stronger automatic cleanup around atomic publish. Those two issues were the main sources of friction in this session.
