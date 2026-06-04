# QA Prompt

Validate the migration against the raw request, not only the implementation summary.

- Confirm `C:\repositories\CanDoItAll.Mcp\CanDoItAll.Mcp.slnx` exists and contains only MCP-related projects.
- Confirm `repo://CanDoItAll.slnx` no longer contains migrated MCP project entries.
- Confirm component package references remain NuGet package references, not main-repo project references.
- Confirm resetup publishes MCP binaries from `C:\repositories\CanDoItAll.Mcp` while syncing skills from `repo://codex/skills`.
- Confirm `.artifacts/mcp-installs` and `.artifacts/mcp-server-shadow` no longer contain pre-migration historical MCP install/shadow content after cleanup.
- Confirm docs in the MCP repository describe server inventory, settings ownership, build/test, and resetup.
