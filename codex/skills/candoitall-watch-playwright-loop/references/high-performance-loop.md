# High-Performance Loop

Use this checklist when UI iteration speed matters more than broad exploratory work.

## Fast path

1. Keep one app session alive.
2. Keep one Playwright page alive.
3. Capture the pre-edit cursor from `candoitall_app_status`.
4. Edit one nearby file.
5. Wait on that cursor.
6. Refresh the same page only if the edit type requires it.
7. Validate the same page.
8. Record the screenshot-review answers and any gate decision while the proof is visible.
9. Move on only after the browser confirms the change.

## Edit classification

### Styling edit

- Prefer `QuietSinceCursor` first.
- Fall back to `WatchSettled` if the log stream is noisy.
- Check computed style with `browser_evaluate`.
- Avoid touching shared layout files unless the fix really belongs there.

### Markup edit

- Use `WatchSettled`.
- Refresh after the wait succeeds.
- Check the exact text, class list, or DOM structure that should have changed.

### C# or render-logic edit

- Use `RevisionConfirmed` when the runtime generation should advance.
- Refresh after the wait succeeds.
- If the runtime generation does not advance, treat that as a failed proof and inspect logs before editing more files.

## Browser checks that matter

- target element text
- target element class list
- target element computed style
- nearby spacing and overflow
- the target large-screen desktop viewport after meaningful application layout changes
- small, medium, and large widths only for reusable basic `CanDoItAll.Components.BaseLib` work or explicit user scope

## Proof order

1. exact DOM or computed-style assertion
2. optional screenshot after the assertion passes
3. screenshot review answers recorded while the image is fresh
4. broader viewport proof only for reusable basic BaseLib work or explicit user scope

## Stop conditions

Stop the nearby-edit loop and diagnose if any of these happen:

- the browser still shows stale UI after the correct wait and refresh
- the page updates, but not from the file you expected
- the watch session restarts unexpectedly
- multiple edits are pending and you can no longer tell which one caused the current DOM

## Recovery path

1. Read `candoitall_app_status`.
2. Read focused `candoitall_app_logs` from the same cursor window.
3. Open the backend manager page if transport or ownership looks suspicious.
4. Use the atomic lane when shared layout or cross-route risk is high.
