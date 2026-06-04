# Assumptions And Risks

## Working Assumptions

- `C:\repositories\CanDoItAll.Mcp` is the intended long-term repository root for active MCP server projects.
- Main-repo MCP settings files remain workspace-specific and should not be moved.
- `tools/CanDoItAll.Manager` is not an MCP server and stays in the main repo because it depends on main application infrastructure.
- `CanDoItAll.CodeAnalsis` remains a sibling repository dependency for the CodeAnalytics MCP unless build proof shows the project can consume packages instead.

## Critical Path Risks

- Moving the DotNetWatch wrapper without updating resetup, tray defaults, Codex config, and VS Code config would strand the most important MCP entrypoint.
- Leaving old MCP project entries in `CanDoItAll.slnx` would make the main repo build fail after source removal.
- Deleting broad `.artifacts` folders could remove unrelated proof or runtime state. Cleanup must target MCP install and shadow history only.
- CodeAnalytics uses sibling project references; the new repo must preserve those relative paths or replace them deliberately.

## Validation Risks

- Resetup touches local user Codex config and shortcuts; host proof should use skip flags when validating repository behavior without changing unrelated user state.
- DotNetWatch shadow builds can leave live processes holding files. Cleanup must stop install-owned processes before removing MCP artifacts.
- Tests may depend on paths that assumed the main repo root. Migration proof must run tests from the MCP repository.

## Reopen Triggers

- Reopen `SB01` if any migrated project still references `repo://src/CanDoItAll.Mcp.*`, `repo://tests/CanDoItAll.Mcp.*`, or main-solution MCP entries after migration.
- Reopen `SB02` if resetup publishes from main-repo MCP paths or skill sync no longer reads from `repo://codex/skills`.
- Reopen `SB02` if `.artifacts/mcp-installs` still contains suppressed or historical MCP installs after cleanup.
- Reopen `SB03` if docs describe old main-repo MCP source paths as current.
