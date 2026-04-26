# Scope Inventory

## Documentation Inventory

| Area | Current Source | Planned Change | Subbundle |
| --- | --- | --- | --- |
| Root overview | `C:\repositories\CanDoItAll\README.md` | Rewrite as current product/architecture overview with diagram and links. | `03-root-and-project-readme-refresh` |
| Docs index | `C:\repositories\CanDoItAll\docs` | Add `docs/README.md` and link key docs. | `03-root-and-project-readme-refresh` |
| Detailed architecture | Missing current page | Add `docs/architecture-beta.md` with architecture-beta, C4, and sequence diagrams. | `02-architecture-diagram-and-process-doc-refresh` |
| Architecture index | `C:\repositories\CanDoItAll\architecture\adrs\README.md` only | Add top-level `architecture/README.md`. | `03-root-and-project-readme-refresh` |
| UI shared-components docs | `C:\repositories\CanDoItAll\docs\ui-shared-components\README.md`; `C:\repositories\CanDoItAll\docs\ui-shared-components\architecture\stack-and-architecture.md` | Repair stale single-project component description to split library architecture. | `03-root-and-project-readme-refresh` |
| Project READMEs | Project directories under `src`, `tests`, `tools` | Add missing `README.md` files for every tracked `.csproj` directory. | `03-root-and-project-readme-refresh` |

## Project Families

| Family | Projects |
| --- | --- |
| Web host and composition | `CanDoItAll.Web`, `CanDoItAll.Composition`, `CanDoItAll.Infrastructure`, `CanDoItAll.SharedKernel` |
| Product modules | `CanDoItAll.Modules.Activity`, `AgentFramework`, `Automation`, `Collaboration`, `CrmHr`, `Factory`, `Processes`, `Projects`, `Prompts`, `Resources`, `Security`, `TestLab`, `Validation`, `Workbench`, `Workspace` |
| AgentFramework libraries | `CanDoItAll.AgentFramework.Components`, `Core`, `Hosting`, `Maf`, `Models`, `Persistence` |
| Component libraries | `CanDoItAll.Components`, `BaseLib`, `CanvasLib`, `Common`, `OverlayLib`, `Sandbox`, `WebGlLib`, `WebGlSandbox` |
| MCP servers | `CanDoItAll.Mcp.Core`, `CodeAnalytics`, `Components`, `DotNetWatch`, `LocalRuntime`, `Processes`, `ProjectStructure`, `SshOps` |
| Database migrations | `CanDoItAll.Migrations.PostgreSql`, `CanDoItAll.Migrations.Sqlite` |
| Space3D | `CanDoItAll.Space3D.Mouse.Components`, `Driver`, `Sandbox` |
| Tests | `CanDoItAll.Tests.*`, `CanDoItAll.Mcp.*.Tests` |
| Tools | `CanDoItAll.Manager`, `CanDoItAll.Mcp.DotNetWatch.Tray`, `CanDoItAll.Mcp.ToolHarness`, `CanDoItAll.RpiValidationArtifacts`, `CanDoItAll.ScenarioSeeder` |
