# Assumptions And Risks

## Working Assumptions

- The isolated host can run locally with a dedicated control-plane root under repo artifacts without disturbing the user’s current working data.
- The dev database endpoints are enabled in the local development host and can bootstrap a managed SQLite profile reliably.
- The source bundle’s B01-B13 structure is strong enough to map into one umbrella project plus execution-focused subprojects without reinterpreting the business scope.
- Playwright MCP is functional in the elevated admin session and can capture headed browser proof for the local host.

## Critical Path Risks

- If the fresh database does not contain a project-structure agent token yet, the MCP API will reject all planning operations until the settings UI is bootstrapped correctly.
- If the canvas auto-layout is weak for imported structures, the resulting plan may technically exist but still fail the readability goal.
- Because there is no direct public link-write endpoint, dependency modeling may require import-based workarounds that are less ergonomic than direct MCP mutations.

## Validation Risks

- Browser proof can pass while the plan is still manager-weak if the plan covers nodes but not execution control semantics.
- A subproject split can improve readability while hiding missing cross-project control unless the umbrella project still provides a clear roadmap.
- Imported generic blocks can lose typed richness if follow-up node enrichment is skipped.

## Reopen Triggers

- Reopen the backfill phase if major source-bundle areas are missing from the resulting hierarchy.
- Reopen the review phase if the canvas is crowded, unreadable, or leaves critical nodes floating without context.
- Reopen the findings phase if a discovered MCP limitation was worked around but not documented.
