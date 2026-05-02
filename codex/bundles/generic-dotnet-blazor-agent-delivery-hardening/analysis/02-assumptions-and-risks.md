# Assumptions And Risks

## Working Assumptions

- The organization seed catalog is the source of truth for default agents, built-in capabilities, and inline skills.
- The MAF workspace tool bridge is the right place to expose generic build/run/test helpers to agents.
- `workspace_dotnet_run` should be a generic bounded startup tool, not a Blazor-specific helper.
- Existing project-structure and process access metadata remain the right way to give agents process context without reattaching retired MCP capabilities.

## Critical Path Risks

- If `workspace_dotnet_run` is only documented and not exposed as a real tool, agents will keep writing brittle per-app launch scripts.
- If Blazor instructions remain sample-shaped, agents may overfit unrelated app requests to converter/calculator interaction patterns.
- If the Blazor specialist is not refreshed into existing managed catalogs, the web app may continue using stale default agents.
- If live validation manually repairs app source, the test will not prove that process-controlled agents can deliver independently.

## Validation Risks

- Live process validation may be slow or provider-dependent; failures must distinguish product gaps from provider/network failures.
- Browser proof must inspect real generated apps, not only a route returning HTTP 200.
- Source scans may find historical fixture names in tests; only active seeded instructions and core prompts should be treated as blockers unless the fixture text leaks to agents.

## Reopen Triggers

- Reopen subbundle 02 if seeded agents still cannot call `workspace_dotnet_run`.
- Reopen subbundle 03 if active seeded instructions contain calculator/converter/unit-topic hardcoding.
- Reopen subbundle 04 if either random-topic process run needs manual app-source repair.
- Reopen earlier subbundles if live agents repeatedly fail for a generic tooling or instruction reason.
