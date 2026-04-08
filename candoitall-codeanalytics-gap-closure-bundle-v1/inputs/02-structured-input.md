# Structured Input

## Core Objective

- Close both residual parity findings in `CanDoItAll.Mcp.CodeAnalytics` and prove the installed MCP now answers the affected Zyphonote scenarios cleanly.

## Hard Constraints

- Use the bundle workflow end to end: prepare, validate, execute, and close.
- Keep the MCP hosted in `C:\repositories\CanDoItAll` while reusing sibling analysis libraries from `C:\repositories\CanDoItAll.CodeAnalsis`.
- Do not copy analysis code into the host repo.
- Capture any remaining trouble as bundle findings instead of hiding it in prose.

## Source Artifacts

- The user request in `inputs/00-original-request.md`
- The two open findings from `candoitall-codeanalytics-zyphonote-parity-bundle-v1`
- The Zyphonote scenario matrix and the previous rerun scorecard

## Input Coverage Signals

- `finding-01-solution-inventory-mixes-product-and-test-projects.md` must be closed with first-class tool behavior, not only client heuristics.
- `finding-02-legacy-focused-context-behavior-intent-alias-fails.md` must be closed with deterministic compatibility handling or an equally strong server-side fix.
- The rerun score must stay at or above the current `47 / 50` while removing the two named residuals.

## Dependency And Sequencing Signals

- Inventory shaping is a critical foundation because rerun precision for Scenario 1 depends on it.
- The legacy alias fix is independent in implementation but both fixes must land before reinstall and rerun closure.
- Reinstall and rerun are the closure phase and must not start until the code changes and tests are stable.

## Validation Expectations

- Unit tests for the sibling repo changes
- Build proof for the host MCP project
- Reinstall proof through `tools\Reinstall-CanDoItAllMcps.ps1`
- Installed-server proof for the affected gap scenarios and regression proof for the existing Zyphonote parity path
- Bundle validator proof for `--stage prepared` and `--stage completed`

## UI Validation Strategy

- N/A for this analysis-only MCP workflow.

## Browser Validation Analytics

- N/A for this analysis-only MCP workflow.

## Working Assumptions

- Project classification can be derived reliably from existing `ProjectFact` data such as project name, path, and package references without rebuilding the snapshot domain model.
- The focused-context alias gap is fixable at the host input boundary or in the shared abstractions without weakening the current deterministic intent flow.
- Reinstalling the MCP remains the correct proof path even if a Codex restart is later needed to refresh local tool schemas.

## Primary Risks

- Changing inventory semantics could accidentally hide useful supporting-project references unless the response shape preserves them explicitly.
- Changing focused-context intent handling could regress the current enum-driven path if the alias mapping is too loose.
- The installed MCP may require a Codex restart for native schema refresh if the public tool input shape changes.
