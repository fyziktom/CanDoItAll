---
name: candoitall-watch-playwright-loop
description: Use when Codex is working with the CanDoItAll dotnetwatch MCP and Playwright MCP together, especially for Blazor or ASP.NET UI work that needs a persistent browser tab, small-step hot-reload validation, accurate wait-condition selection, and a fast nearby-edit loop without stacking unverified changes.
---

# CanDoItAll Watch Playwright Loop

Use one managed app session and one persistent Playwright page. The loop is only fast when Codex proves each edit before making the next one.

## Goal

- keep hot-reload loops near plain `dotnet watch` speed
- use Playwright as the browser truth, not the watch log
- prevent overlapping edits, repeated waits, and stale-page confusion
- tune CanDoItAll applications at the target large-screen desktop viewport
- record the screenshot review and gate decision while the proof is fresh

## Required loop

1. Call `candoitall_workspace_info`.
2. Start or reuse the app with `candoitall_app_start`.
3. Wait for `WatchReady` before beginning UI edits.
4. Open one Playwright page on the target route and keep it open.
5. Maximize the browser window, or resize it to fill the available large-screen desktop work area before the first layout judgement.
6. Capture a baseline with `candoitall_app_status`:
   - `sessionId`
   - `lastCursor`
   - `revision`
   - `watch.lastHotReloadOutcome`
7. Make one nearby edit.
8. Wait from the pre-edit cursor with the correct `candoitall_app_wait` condition.
9. Re-check the same Playwright page.
10. Record the visual review answers and validation result in the bundle analytics or linked evidence file.
11. If the current change is a critical foundation, prove one dependent interaction or downstream surface before continuing.
12. Only continue if browser truth matches the intended change.

## Session contract

- Reuse the current healthy managed app session. Do not call `candoitall_app_start` again just because the browser still shows stale UI.
- Reuse the same Playwright page for the route under test. Refresh the page when needed; do not open a new tab for the same proof.
- Record the pre-edit cursor before every edit. Every wait must be tied to the cursor from the immediately preceding browser-validated state.
- Never queue a second edit while the first edit is still waiting on watch or browser proof.

## Shared backend rule

- The machine should expose one shared `candoitall_dotnetwatch` MCP backed by the CanDoItAll install.
- If the app under test lives in another repo, start that repo's `.csproj` through `candoitall_app_start` or the backend manager project picker.
- Do not create a second repo-specific dotnetwatch MCP such as `<repo>_dotnetwatch`.
- If the shared install is missing or stale, switch to `candoitall-dotnetwatch-setup` before doing UI work.

## Per-edit protocol

1. Read `candoitall_app_status` and record `sessionId`, `revision`, and `lastCursor`.
2. Make one file change in the effective UI surface.
3. Call `candoitall_app_wait` with the matching condition and the recorded cursor.
4. Refresh the same Playwright page only after the wait succeeds when the edit type requires refresh.
5. Prove the exact intended change with DOM, computed-style, and visual checks.
6. Log the route, viewport, Playwright MCP actions, assertions, screenshot paths, and screenshot-review answers used for that proof.
7. If the edit unlocks later work, validate one dependent interaction before closing the proof.
8. Move to the next edit only after the proof passes.

## Performance rules

- One edit at a time. Do not stack a second UI edit before the first one is browser-validated.
- Keep one Playwright tab open for the full loop. Reopening the browser wastes time and hides stale-state problems.
- Prefer local effective surfaces first:
  - current `.razor`
  - current `.razor.css`
  - local module stylesheet
  - component-level style source
- Split work into phases:
  - structure or behavior first
  - styling polish second
- For styling passes, stay in the fast path and avoid touching unrelated files.
- If watch and browser truth diverge, stop editing and diagnose immediately.

## Wait-condition matrix

Use the smallest proof that matches the edit.

| Edit type | Wait condition | Browser action |
|---|---|---|
| Initial app ready | `WatchReady` | Open the page after ready |
| CSS or static asset change | `QuietSinceCursor` or `WatchSettled` | Re-check same page without full browser reopen |
| Razor markup or component structure change | `WatchSettled` | Refresh the page after the wait succeeds |
| C# UI logic change expected to advance runtime generation | `RevisionConfirmed` | Refresh the page after the wait succeeds |
| Restart-heavy change | `RestartCompleted`, then `Healthy` if needed | Refresh after replacement runtime is healthy |
| Atomic candidate validation | `TransactionCommitted` | Open candidate URL in a separate page |

## Browser validation rules

- Use `browser_evaluate` for exact DOM, text, class, and computed-style checks.
- Use screenshots as evidence after the DOM proof, not as the only proof.
- On the first layout pass, capture a large-screen screenshot after the browser is maximized and answer the visual questions from the bundle execution reference.
- Record at least one analytics entry per validation pass with route, viewport, Playwright MCP actions, assertions, screenshot path, and pass or fail outcome.
- Do not stop at capturing the screenshot. Read it, answer the visual questions, and record the decision while the proof is visible.
- When validating overlays such as tooltips, help affordances, menus, or floating popovers, prove the open state with both geometry and visual evidence:
  - open the overlay in Playwright
  - check bounding boxes or computed style when needed
  - verify the content is not clipped by the viewport or its parent container
  - verify the overlay is not hidden behind adjacent floating windows or chrome
- When the current edit is a critical foundation for later subbundles, validate one downstream interaction or dependent surface before proceeding.
- If any answer about readability, overlap, spacing, alignment, or space usage is not acceptable, fix that before proceeding.
- Prefer one exact assertion over a broad page snapshot. Proof should name the element, text, class, or style that changed.
- Do not run tablet/mobile/small/medium application checks unless the user explicitly requests them.
- For reusable basic `CanDoItAll.Components.BaseLib` changes, reuse the same page context and re-check small, medium, and large viewports.
- For other shared libraries, preserve existing responsive behavior when touched without expanding responsive scope implicitly.
- Refresh only after the managed wait completes.
- Because automatic browser refresh is suppressed, expect to refresh manually for markup and C# edits.
- Use the `screenshot` skill when browser capture is insufficient or when desktop/window context matters.
- If you cannot produce a real Playwright MCP interaction for the current validation target, stop and document the blocker instead of guessing.

## Recovery rules

- If `WatchSettled` succeeded but the page still shows old UI:
  - inspect DOM text and classes in Playwright
  - inspect `candoitall_app_status`
  - inspect `candoitall_app_logs`
  - verify that the edited file is the effective surface
- If a change is still not visible after the correct wait plus refresh, do not keep editing.
- Escalate to one of:
  - focused diagnosis on the current watch session
  - backend manager page
  - atomic candidate validation
- If the watch session restarts, treat the prior cursor as invalid and capture a new baseline before continuing.

## Anti-patterns

- Do not mix multiple UI edits into one wait cycle.
- Do not trust `Hot reload succeeded` alone.
- Do not use manual `dotnet watch`, `dotnet run`, `dotnet build`, or `dotnet test` while the managed watch session is healthy unless the MCP server itself is being repaired or benchmarked.
- Do not keep reopening new Playwright tabs for the same page.
- Do not widen scope when the current browser state is unclear.
- Do not paper over stale browser state by restarting the app unless logs or manager state show the current session is unhealthy.

## References

- Read [references/high-performance-loop.md](references/high-performance-loop.md) for the concrete fast-path and recovery checklist.
- Read [references/observed-behaviors.md](references/observed-behaviors.md) for the tested heuristics behind these wait choices.
- Use `candoitall-dotnetwatch-setup` first when the shared backend or machine wiring needs repair.
