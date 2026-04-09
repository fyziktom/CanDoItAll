# Scope Inventory

- `CanDoItAll.Mcp.Processes`: local stdio MCP server, coordinator, tool contracts, and bootstrap
- `CanDoItAll.Composition`: shared migration bootstrap reused by the web host and process MCP
- `tools\Install-CanDoItAllProcessesMcp.ps1`: focused installer
- `tools\Reinstall-CanDoItAllMcps.ps1`: standard reinstall and discoverability workflow
- `CanDoItAll.Mcp.Processes.settings.json`: committed local settings
- `docs\processes-mcp-setup.md`: operator guidance
- `codex\skills\candoitall-processes-mcp\SKILL.md`: restart-aware repo-managed skill guidance
- generated config outputs: `.vscode\mcp.json`, `%USERPROFILE%\.codex\config.toml`, `.artifacts\mcp-installs\install-manifest.json`
