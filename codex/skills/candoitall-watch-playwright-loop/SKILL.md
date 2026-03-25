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

## Required loop

1. Call `candoitall_workspace_info`.
2. Start or reuse the app with `candoitall_app_start`.
3. Wait for `WatchReady` before beginning UI edits.
4. Open one Playwright page on the target route and keep it open.
5. Capture a baseline with `candoitall_app_status`:
   - `sessionId`
   - `lastCursor`
   - `revision`
   - `watch.lastHotReloadOutcome`
6. Make one nearby edit.
7. Wait from the pre-edit cursor with the correct `candoitall_app_wait` condition.
8. Re-check the same Playwright page.
9. Only continue if browser truth matches the intended change.

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
5. Prove the exact intended change with DOM or computed-style checks.
6. Move to the next edit only after the proof passes.

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
- Prefer one exact assertion over a broad page snapshot. Proof should name the element, text, class, or style that changed.
- For responsive checks, resize or reuse the same page context instead of reopening the route in a fresh browser session.
- Re-check desktop, tablet, and mobile after meaningful UI changes.
- Refresh only after the managed wait completes.
- Because automatic browser refresh is suppressed, expect to refresh manually for markup and C# edits.

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
