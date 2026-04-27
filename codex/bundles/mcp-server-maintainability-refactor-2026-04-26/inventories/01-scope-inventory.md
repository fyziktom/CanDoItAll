# Scope Inventory

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260426215347-aec8ae51`
- Solution: `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Scoped projects: 8
- Scoped documents: 122

## MCP Projects

| Project | Role in refactor | Notes |
| --- | --- | --- |
| `CanDoItAll.Mcp.Core` | Shared helper home | Referenced by all scoped server projects. |
| `CanDoItAll.Mcp.CodeAnalytics` | Host migration consumer | Repeats settings/logging/options setup. |
| `CanDoItAll.Mcp.Components` | Host migration plus catalog split | Contains the largest catalog service hotspot. |
| `CanDoItAll.Mcp.DotNetWatch` | Host migration plus route split | Dual stdio/backend host and long runtime files. |
| `CanDoItAll.Mcp.LocalRuntime` | Supporting runtime library | No host migration, but affected by DotNetWatch build. |
| `CanDoItAll.Mcp.Processes` | Host migration consumer | Has infrastructure registration beyond shared host setup. |
| `CanDoItAll.Mcp.ProjectStructure` | Host migration consumer | Repeats settings/logging/options setup. |
| `CanDoItAll.Mcp.SshOps` | Host migration consumer | Repeats setup and registers redaction/log helpers. |

## Long File Inventory

| File | Approx source lines | Planned action |
| --- | ---: | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Catalog\ComponentCatalogService.cs` | 1680 | Split static metadata from service behavior. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Runtime\AppRuntimeModels.cs` | 1623 | Inventory only in this pass unless validation reveals a low-risk split. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Runtime\SessionCoordinator.cs` | 1207 | Inventory only in this pass; do not rewrite runtime coordination casually. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Program.cs` | 395 | Split backend route mapping and route execution wrapper. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Coordination\TargetCoordinator.Compose.cs` | 707 | Inventory only; already uses partial split and needs deeper behavior-specific bundle before changing. |

## Test Surface

| Test project | Relevant files |
| --- | --- |
| `CanDoItAll.Mcp.Components.Tests` | `ComponentCatalogServiceTests.cs`, `ComponentsToolsTests.cs` |
| `CanDoItAll.Mcp.DotNetWatch.Tests` | `InfrastructureTests.cs`, `AppSessionLifecycleTests.cs`, `BundleImprovementTests.cs`, `TailwindCompanionTests.cs` |
| `CanDoItAll.Mcp.Processes.Tests` | Existing process template and tools tests |
| `CanDoItAll.Mcp.ProjectStructure.Tests` | Existing coordinator and tools tests |
