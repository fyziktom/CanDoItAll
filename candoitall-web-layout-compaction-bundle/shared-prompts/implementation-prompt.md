# Implementation Prompt

Implement only the active subbundle for `candoitall-web-layout-compaction-bundle`.

## Non-Negotiable Rules

- Optimize for a maximized large-screen browser first.
- Prefer the smallest correct shared fix over route-specific duplication.
- Reuse existing BaseLib or app components before inventing new wrappers.
- Prefer Tailwind module edits and component `Class` composition over new raw CSS.
- Keep one CanDoItAll watch session alive and make one nearby UI edit at a time.
- Reuse one browser session per route until the current proof is done.
- If a shared change causes more per-page hacks instead of fewer, stop and repair the shared primitive first.

## Proof Rules

- Record the pre-edit watch cursor before each UI change.
- Wait for the correct watch condition before refreshing the route.
- Capture large-screen browser proof first.
- For modal and overlay work, validate the open state explicitly.
- Update `reviews/01-execution-report.md` while the proof is fresh.

## Large-Screen Density Questions

- Are we using the available width intentionally?
- Did we remove unnecessary first-screen height?
- Can search, filters, and reset stay on one row where the viewport reasonably allows it?
- Is explanatory copy still available even if it is no longer always visible?
- Did the change reduce, rather than increase, route-specific layout hacks?

