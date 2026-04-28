# Assumptions And Risks

## Working Assumptions

- BaseLib can introduce `DialogHost` while retaining the existing `Dialog` component as the controlled low-level modal.
- Tooltips can be positioned from Blazor mouse/focus event coordinates for this phase; element-rect JS positioning is not required unless Playwright reveals poor behavior.
- Notification service changes can stay backward compatible by keeping `Notify(NotificationMessage)` and `NotificationMessage` names.
- Tailwind output can be regenerated with `npm run tailwind:build`.

## Critical Path Risks

- A weak dialog result model would invalidate sandbox examples, docs, and Playwright proof.
- Reusing the existing `Dialog` component incorrectly could break product module dialogs that already pass `IsOpen`.
- Host components can silently fail if they are not mounted centrally in the sandbox layout.
- Generic component rendering must validate component types before opening service dialogs.

## Validation Risks

- Component tests can prove service state but cannot prove overlay layering, modal sizing, or viewport clipping.
- Tooltip positioning might look acceptable in bUnit but fail in the browser near viewport edges.
- Generated Tailwind output can miss dynamically built class names; classes should be literal in Razor where possible.
- Playwright MCP validation must inspect open overlay states, not only trigger buttons.

## Reopen Triggers

- Reopen subbundle 01 if any service host cannot subscribe/unsubscribe cleanly or host mounting is missing.
- Reopen subbundle 02 if a dialog close path does not complete the awaited result task exactly once.
- Reopen subbundle 02 if modal sizes are visually indistinguishable or clip content at desktop/mobile widths.
- Reopen subbundle 03 if tooltip or notification overlays render behind dialogs, page chrome, or each other.
- Reopen subbundle 04 if sandbox examples do not exercise the service APIs directly with Playwright MCP.
