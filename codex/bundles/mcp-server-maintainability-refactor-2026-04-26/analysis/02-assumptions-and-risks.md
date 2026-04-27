# Assumptions And Risks

## Working Assumptions

- `CanDoItAll.Mcp.Core` is an acceptable shared helper location for configuration, logging, and options wiring that is not tied to `ModelContextProtocol`.
- Tool registration remains server-owned so `CanDoItAll.Mcp.Core` does not need a dependency on the MCP SDK.
- The first implementation pass should prioritize the repeated host setup and two file-splitting hotspots over deep behavioral rewrites.
- Tests should prove behavior preservation rather than only checking line count movement.

## Critical Path Risks

- If shared host helpers accidentally change configuration binding semantics, every MCP server may read settings differently.
- If options validation registration changes, invalid settings could fail later than before or not at all.
- If component catalog static metadata is moved incorrectly, catalog responses may silently lose guidance, tags, examples, or CSS notes.
- If DotNetWatch route mapping is split carelessly, backend routes may compile but lose request replay or cancellation behavior.

## Validation Risks

- A full solution build can surface unrelated package advisory warnings or project issues outside the scoped MCP refactor.
- Tests may not cover every MCP server startup path, so targeted helper tests and focused project builds are required.
- File splitting by partial classes can preserve behavior but may not improve testability unless the split exposes a meaningful seam or helper.
- Browser validation is not applicable; host-level build/test proof must be strong enough for this server-side refactor.

## Reopen Triggers

- Reopen subbundle 01 if any server no longer loads JSON settings plus `CanDoItAllMcp_` environment variables in the same order.
- Reopen subbundle 01 if shared logging no longer routes console logs to stderr for stdio hosts.
- Reopen subbundle 02 if component catalog tests show changed search, examples, CSS token, or guidance output.
- Reopen subbundle 03 if backend route tests/builds show route mapping, replay, cancellation, or auth behavior drift.
- Reopen the relevant subbundle if validation finds a public tool contract or function was removed instead of moved.
