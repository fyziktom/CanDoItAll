# Assumptions And Risks

## Assumptions

- The Processes workspace should continue to use the current managed database profile without adding separate token or settings UI.
- The definition list should summarize the same version shape the authoring workspace treats as current, not aggregate every historical version.
- Existing MCP tools and browser verification remain available for closure proof.

## Critical Path Risks

- If the first-render guard fix is wrong, the browser can still show `Definitions 0` on `/processes`, which invalidates all later verification because the UI would remain disconnected from the real profile data.
- If the summary-version selection is wrong, counts may stop doubling for the smoke case but still drift from the editor/runtime model in draft-versus-published scenarios.

## Validation Risks

- Browser proof depends on the local web app starting cleanly under a development URL. Static-asset or startup regressions must be distinguished from the Processes fixes.
- MCP proof is only meaningful if the smoke definition is still persisted in the active managed profile.
- The count regression is subtle enough that build-only proof is insufficient; service-level or browser-level assertions must confirm the visible totals.

## Reopen Triggers

- Reopen subbundle 01 if `/processes` still renders an empty state on the first visit without query parameters.
- Reopen subbundle 02 if a published definition with a cloned draft shows doubled role or step counts anywhere in the workspace or tests.
- Reopen the entire bundle if the repair requires token/config wiring or any change outside the agreed UI/counting/database scope.
