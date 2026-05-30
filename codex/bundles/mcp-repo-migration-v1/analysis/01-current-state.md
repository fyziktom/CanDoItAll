# Current State

## Main Repository MCP Footprint

- Active MCP source projects currently live under `repo://src/CanDoItAll.Mcp.CodeAnalytics`, `repo://src/CanDoItAll.Mcp.Components`, `repo://src/CanDoItAll.Mcp.Core`, `repo://src/CanDoItAll.Mcp.DotNetWatch`, `repo://src/CanDoItAll.Mcp.LocalRuntime`, `repo://src/CanDoItAll.Mcp.Mermaid`, and `repo://src/CanDoItAll.Mcp.SshOps`.
- MCP tests currently live under `repo://tests/CanDoItAll.Mcp.Components.Tests`, `repo://tests/CanDoItAll.Mcp.DotNetWatch.Tests`, `repo://tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests`, and `repo://tests/CanDoItAll.Mcp.Mermaid.Tests`.
- MCP helper tools currently live under `repo://tools/CanDoItAll.Mcp.DotNetWatch`, `repo://tools/CanDoItAll.Mcp.DotNetWatch.Tray`, and `repo://tools/CanDoItAll.Mcp.ToolHarness`.
- `repo://CanDoItAll.slnx` includes those MCP source/test/tool projects today.

## Reinstall Script State

- `repo://tools/Reinstall-CanDoItAllMcps.ps1` uses one `$RepoRoot` for both workspace settings/skills and MCP project paths.
- The script publishes Components, CodeAnalytics, SshOps, Manager, and DotNetWatch tray into `repo://.artifacts/mcp-installs`.
- DotNetWatch uses `repo://tools/CanDoItAll.Mcp.DotNetWatch/Start-CanDoItAllDotNetWatchMcp.ps1` to build a shadow host under `repo://.artifacts/mcp-server-shadow`.
- Skill sync currently reads from `repo://codex/skills`, which is the behavior the user wants to keep.

## Destination Repository State

- `C:\repositories\CanDoItAll.Mcp` exists with `.gitignore` and a placeholder `README.md`.
- It does not yet have a solution, source projects, tests, docs, or NuGet configuration.

## Artifact State

- `repo://.artifacts/mcp-installs` contains current and historical MCP installs, including suppressed `CanDoItAll.Mcp.Processes` and `CanDoItAll.Mcp.ProjectStructure`.
- `repo://.artifacts/mcp-server-shadow` contains current, previous, failed, builds, and retired-builds data.
- `repo://.artifacts` also contains many older test/build output folders from prior work; only MCP install and MCP shadow history are owned by this request.
