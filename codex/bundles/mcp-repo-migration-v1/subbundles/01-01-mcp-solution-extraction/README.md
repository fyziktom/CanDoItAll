# SB01 MCP Solution Extraction

## Status

- `Completed`

## Objective

Move the active MCP source, MCP tests, and MCP helper tooling into `C:\repositories\CanDoItAll.Mcp`, then create a standalone MCP solution that builds independently of the main CanDoItAll solution.

## Covered Inputs

- `N001`: `Move our MCPs servers to own repo.`
- `N001`: `create own solution there just for our MCPs`
- `N001`: `They do not have any specific dependencies. Just Components that you can connect as nuget packages`

## Prerequisites

- `C:\repositories\CanDoItAll.Mcp` exists.
- Current MCP source directories exist under `repo://src`.
- Current MCP test directories exist under `repo://tests`.

## Exact Source References

- `C:\repositories\CanDoItAll.Mcp\src\CanDoItAll.Mcp.Core\CanDoItAll.Mcp.Core.csproj`
- `C:\repositories\CanDoItAll.Mcp\src\CanDoItAll.Mcp.Components\CanDoItAll.Mcp.Components.csproj`
- `C:\repositories\CanDoItAll.Mcp\src\CanDoItAll.Mcp.CodeAnalytics\CanDoItAll.Mcp.CodeAnalytics.csproj`
- `C:\repositories\CanDoItAll.Mcp\src\CanDoItAll.Mcp.DotNetWatch\CanDoItAll.Mcp.DotNetWatch.csproj`
- `C:\repositories\CanDoItAll.Mcp\src\CanDoItAll.Mcp.LocalRuntime\CanDoItAll.Mcp.LocalRuntime.csproj`
- `C:\repositories\CanDoItAll.Mcp\src\CanDoItAll.Mcp.Mermaid\CanDoItAll.Mcp.Mermaid.csproj`
- `C:\repositories\CanDoItAll.Mcp\src\CanDoItAll.Mcp.SshOps\CanDoItAll.Mcp.SshOps.csproj`
- `C:\repositories\CanDoItAll.Mcp\tests\CanDoItAll.Mcp.Components.Tests\CanDoItAll.Mcp.Components.Tests.csproj`
- `C:\repositories\CanDoItAll.Mcp\tests\CanDoItAll.Mcp.DotNetWatch.Tests\CanDoItAll.Mcp.DotNetWatch.Tests.csproj`
- `C:\repositories\CanDoItAll.Mcp\tests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests.csproj`
- `C:\repositories\CanDoItAll.Mcp\tests\CanDoItAll.Mcp.Mermaid.Tests\CanDoItAll.Mcp.Mermaid.Tests.csproj`
- `C:\repositories\CanDoItAll.Mcp\tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1`
- `C:\repositories\CanDoItAll.Mcp\tools\CanDoItAll.Mcp.DotNetWatch.Tray\CanDoItAll.Mcp.DotNetWatch.Tray.csproj`
- `C:\repositories\CanDoItAll.Mcp\tools\CanDoItAll.Mcp.ToolHarness\CanDoItAll.Mcp.ToolHarness.csproj`
- `repo://CanDoItAll.slnx`

## Deliverables

- `C:\repositories\CanDoItAll.Mcp\CanDoItAll.Mcp.slnx`
- Migrated MCP `src`, `tests`, and MCP `tools` directories under the MCP repo.
- Main `repo://CanDoItAll.slnx` with migrated MCP project entries removed.
- MCP repository `NuGet.config`, `Directory.Build.props`, and `global.json` or equivalent build infrastructure.

## Dependency Impact

- `SB02` depends on the final MCP project paths and DotNetWatch wrapper path produced here.
- `SB03` depends on the solution inventory and build commands produced here.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof covering shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- Requires `bundle://proof/SB01/manifest.md` and `bundle://proof/SB01/semantic-invariants.md`.

## Implementation Steps

1. Move MCP source/test/tool directories into `C:\repositories\CanDoItAll.Mcp`.
2. Copy or recreate build infrastructure needed by MCP projects without dragging application projects into the MCP repo.
3. Create `CanDoItAll.Mcp.slnx` with only MCP-related projects.
4. Update project references and README paths for the new repository layout.
5. Remove migrated MCP project entries from `repo://CanDoItAll.slnx`.
6. Build and test from `C:\repositories\CanDoItAll.Mcp`.

## Do Not Do

- Do not move Codex skills or workspace settings.
- Do not move `tools/CanDoItAll.Manager`.
- Do not add main application project references to the MCP repository.
- Do not reactivate suppressed Processes or ProjectStructure MCPs.

## Acceptance Checklist

- `C:\repositories\CanDoItAll.Mcp\CanDoItAll.Mcp.slnx` exists.
- The new solution contains only MCP-related projects.
- The main solution no longer contains migrated MCP project entries.
- Component package references remain NuGet package references.
- Build/test proof exists under `bundle://proof/SB01/transcripts`.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll.Mcp\CanDoItAll.Mcp.slnx`.
- Targeted MCP tests from `C:\repositories\CanDoItAll.Mcp`.
- Source assertion transcript proving no migrated MCP paths remain in `repo://CanDoItAll.slnx`.
- Anti-stub audit transcript for migrated production MCP source.
- Critical proof manifest and semantic invariant contract under `bundle://proof/SB01`: `bundle://proof/SB01/manifest.md` and `bundle://proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- N/A. No browser-visible UI surface changes.

## Progression Gate

- `SB02` may start only after the new MCP solution builds, migrated tests run or explicit test blockers are recorded, and `repo://CanDoItAll.slnx` no longer references moved MCP projects.

## Suggested Agent Prompt

Move active MCP source, MCP tests, and MCP helper tooling to `C:\repositories\CanDoItAll.Mcp`; create a standalone MCP solution; remove migrated projects from the main solution; then capture build/test/source-audit proof for `SB01`.
