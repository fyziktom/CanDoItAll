# Assumptions And Risks

## Working Assumptions

- The backup branch preserves the removed MCP code if it is needed later.
- The canonical runtime behavior lives in existing services and the API should call those services rather than cloning MCP coordinator logic.
- Project-structure and process API skills should target HTTP endpoints and Swagger/OpenAPI, not MCP tool names.
- Historical bundle documentation can keep old MCP references unless it affects current build, scripts, or installed skills.

## Critical Path Risks

- Removing project references without deleting or updating integration tests will break solution builds.
- Leaving reinstall script parameters or config writers for removed MCPs can regenerate dead Codex/VS Code entries.
- Removing the Settings UI without checking service references can leave orphaned component tests or dead DI registrations.
- Changing the project-structure API route can break current tests if paths are not updated consistently.

## Validation Risks

- A full solution build may surface unrelated existing warnings; only new failures block this cleanup.
- Browser proof for Settings UI removal may be blocked if the app cannot launch in the local environment; record the exact blocker if so.
- Local `config.toml` edits affect this Codex session only after restart, so validate by inspecting file content.

## Reopen Triggers

- Any source reference to `src\CanDoItAll.Mcp.ProjectStructure` or `src\CanDoItAll.Mcp.Processes` remains in active projects, scripts, config, or installed repo skills.
- Swagger misses the new process template parity endpoints.
- New API skills omit the preserved typed block, Mermaid asset, lease, process template, HR matching, or agent API guidance.
- The reinstall script still publishes, stops, or configures the removed MCPs.
