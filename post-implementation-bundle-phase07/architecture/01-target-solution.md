# Target Solution

The phase07 repair bundle is a closure artifact, not a fresh implementation plan. Its architectural job is to preserve the exact reopen lanes if later evidence contradicts the current closure review.

The boundary for phase07 is:

- `CanDoItAll.Mcp.Processes` stays a thin local stdio orchestration surface
- canonical process behavior stays in `CanDoItAll.Modules.Processes`
- database bootstrap stays on the shared composition helper, not a web-only path
- install and discovery stay inside the repo-standard MCP workflows and synced skills

If those boundaries continue to hold, every repair lane in this bundle remains blocked.
