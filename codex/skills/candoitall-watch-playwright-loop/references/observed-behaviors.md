# Observed Behaviors

These notes come from forward-testing the skill against the live CanDoItAll dashboard with `candoitall_dotnetwatch` and Playwright MCP.

## 1. Persistent browser validation works

- Keep one Playwright page open while the managed watch session stays alive.
- Reuse the same page for repeated `browser_evaluate`, `browser_snapshot`, and `browser_take_screenshot` calls.

## 2. `QuietSinceCursor` is good for true static-asset loops

- For edits that the watch session treats as static-asset updates, `candoitall_app_wait(condition="QuietSinceCursor", cursor=<baseline>)` returns quickly and is a good fast-path gate before checking the same page.

## 3. Global CSS is not always the effective surface

- In the tested dashboard, the loaded stylesheets were:
  - `_content/CanDoItAll.Components/...`
  - `_content/CanDoItAll.ComponentKit/...`
  - `app.<hash>.css`
  - `CanDoItAll.Web.<hash>.styles.css`
- Editing `wwwroot\app.css` produced successful static-asset hot-reload signals, but the tested selectors did not affect the visible page.
- Conclusion:
  verify the actual styling owner with Playwright before assuming a shared CSS file is the right surface.

## 4. Browser truth beats watch truth

- A nearby Razor class-string edit did not show up in the browser during the test, even after `WatchSettled` and manual refresh.
- The managed session still reported a successful settled state.
- Conclusion:
  if Playwright still shows the old DOM or styles, stop trusting the watch summary alone. Inspect the DOM, logs, and actual edited surface, then decide whether to restart, rerender, or escalate to atomic validation.

## 5. Manual refresh may still be required

- The runtime suppresses automatic browser refresh, so markup or C# edits should be followed by the correct `candoitall_app_wait` condition and then an explicit Playwright refresh.
- If that still does not show the change, the loop is no longer a safe fast-path iteration and should be escalated.
