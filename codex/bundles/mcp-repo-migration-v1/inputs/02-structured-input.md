# Structured Input

## Objectives

- Move active MCP server code, helper MCP tooling, and MCP tests into `C:\repositories\CanDoItAll.Mcp`.
- Create a standalone solution in the MCP repository that builds and tests only MCP-related projects.
- Remove migrated MCP project entries from the main `CanDoItAll.slnx`.
- Update `repo://tools/Reinstall-CanDoItAllMcps.ps1` so it accepts or discovers the MCP repository root and publishes MCP binaries from there.
- Preserve main-repo ownership of MCP settings and Codex skills.
- Remove historical MCP install and shadow artifacts from `repo://.artifacts`.
- Add MCP repository README and docs.

## Non-Objectives

- Do not move the main web application, application modules, non-MCP tools, or Codex skills.
- Do not reactivate suppressed `CanDoItAll.Mcp.Processes` or `CanDoItAll.Mcp.ProjectStructure`.
- Do not redesign MCP tool behavior unless the move exposes a build/runtime path defect.

## Validation Expectations

- `dotnet build` succeeds for the new MCP solution.
- MCP test projects run from the new MCP repository.
- The resetup script can rebuild/publish MCP binaries from the MCP repository while syncing skills from `repo://codex/skills`.
- `.artifacts` no longer contains old MCP installs or historical MCP shadow build directories after resetup.
- The main solution no longer references migrated MCP projects.
