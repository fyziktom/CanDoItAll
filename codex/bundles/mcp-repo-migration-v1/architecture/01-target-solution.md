# Target Solution

## Repository Boundaries

- `C:\repositories\CanDoItAll.Mcp` owns MCP source, MCP tests, DotNetWatch wrapper, tray app, and MCP tool harness.
- `C:\repositories\CanDoItAll` owns application code, workspace MCP settings, Codex skills, and resetup orchestration.
- `C:\repositories\CanDoItAll.CodeAnalsis` remains a sibling dependency consumed by `CanDoItAll.Mcp.CodeAnalytics`.

## MCP Repository Layout

```text
CanDoItAll.Mcp/
  CanDoItAll.Mcp.slnx
  Directory.Build.props
  NuGet.config
  README.md
  docs/
  src/
    CanDoItAll.Mcp.*
  tests/
    CanDoItAll.Mcp.*.Tests
  tools/
    CanDoItAll.Mcp.DotNetWatch/
    CanDoItAll.Mcp.DotNetWatch.Tray/
    CanDoItAll.Mcp.ToolHarness/
```

## Main Repository Layout After Migration

- `repo://CanDoItAll.slnx` keeps application, module, plugin, component, and non-MCP tool projects.
- `repo://tools/Reinstall-CanDoItAllMcps.ps1` remains the main resetup entrypoint.
- `repo://CanDoItAll.Mcp.*.settings.json` stay in the workspace root.
- `repo://codex/skills` remains the skill sync source.

## Resetup Contract

- `$RepoRoot` means the main CanDoItAll workspace root.
- `$McpRepoRoot` means the MCP source repository root and defaults to a sibling `CanDoItAll.Mcp`.
- Published MCP binaries still land under `$RepoRoot\.artifacts\mcp-installs`.
- DotNetWatch shadow builds still land under `$RepoRoot\.artifacts\mcp-server-shadow`.
- Generated Codex and VS Code MCP configs must point to the correct executable or wrapper paths after migration.
