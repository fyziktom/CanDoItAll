# mcp-improvements-bundle-1-using-experiences

This bundle captures the real workflow of redesigning the projects page while using `CanDoItAll.Mcp.DotNetWatch` watch, atomic publish, and backend-managed build flows.

Contents:

- `01-user-stories-and-layout-findings.md`
- `02-watch-loop-observations.md`
- `03-atomic-update-measurements.md`
- `04-manual-vs-mcp-build-comparison.md`
- `05-dotnetwatch-mcp-improvements.md`
- `artifacts/`
- `tools/`

Key outcomes:

- The projects board was reorganized into a denser command bar plus scrollable card surface while keeping all existing project operations.
- Desktop first-card entry moved from `600px` to `393px`; mobile first-card entry moved from `1415px` to `693px`.
- The projects board itself now fits inside a `900px` desktop viewport (`boardTop=149`, `boardBottom=809`).
- Remaining full-document scroll is caused by the shared dev-only `Tuning Mode` panel rendered below the page, not by the projects board itself.
- The watch lane produced two verified hot-reload/browser divergence cases where `RevisionConfirmed` and `Hot reload succeeded` did not match what a fresh browser actually rendered.
- A fresh watch restart resolved the stale-DOM problem immediately.
- Final atomic publish of the finished layout took about `115.8s`.
- Final backend-managed build took `62.6s` and reduced `44` raw log entries to `6` surfaced lines.
- Final unmanaged manual build took `28.9s` and produced a `22` line / `4.8 KB` log.

Useful artifact folders:

- `artifacts/baseline-before`
- `artifacts/after-fresh-watch-visible-tags`
- `artifacts/after-atomic-visible-tags`
- `artifacts/watch-divergence-log.ndjson`
- `artifacts/manual-build-web.log`
