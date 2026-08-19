# Source hotspot map

| ID | Path | Area | Prepared observation | Evidence |
|---|---|---|---|---|
| ROOT-001 | `global.json` | Build | Pins .NET SDK 10.0.302. | Verified |
| ROOT-002 | `Directory.Build.props` | Build | Repository-wide build and package versions; no Windows-only TFM. | Verified |
| ROOT-003 | `CanDoItAll.slnx` | Architecture | Current solution includes the new Processes stack, process drivers, Security.Abstractions, and MAF runtime abstractions. | Verified |
| FILE-001 | `src/Integration/CanDoItAll.FileTools.Integration/ConfiguredDesktopFileLauncher.cs` | Desktop integration | Delegates capability to the external FileTools desktop package. | Verified |
| FILE-002 | `src/Integration/CanDoItAll.FileTools.Integration/CanDoItAll.FileTools.Integration.csproj` | External dependency | Uses CanDoItAll.FileTools.Desktop package 0.1.18. | Verified |
| FILE-003 | `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs` | Desktop integration | Combines trusted path resolution, preferred application records, and desktop launch. | Verified |
| MAF-003 | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandEnvironmentPolicy.cs` | Process environment | Windows-heavy allowlist and case-insensitive environment map on every OS. | Verified |
| MAF-004 | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceExecutableLocator.cs` | Executable resolution | Always probes .exe/.cmd/.bat and does not prove Unix execute permission. | Verified |
| MAF-005 | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs` | Process execution | Typed direct process execution is already mostly portable; process-tree semantics remain unproven on Unix. | Verified |
| MAF-006 | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandProcessRunner.cs` | Process execution | Central command runner, path aliasing, environment filtering, receipts, and product-target audit. | Verified |
| MAF-007 | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/LocalMcpCommandPolicy.cs` | MCP | Case-insensitive command-name policy mixes Unix and Windows executable suffixes. | Verified |
| MAF-008 | `src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/McpExecutableResolver.cs` | MCP | More OS-aware executable resolution than WorkspaceExecutableLocator, creating duplicate semantics. | Verified |
| MAF-009 | `src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpProcessLauncher.cs` | MCP | Direct local stdio MCP process launch. | Verified |
| MAF-010 | `src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpEnvironmentBinder.cs` | MCP | Binds raw and inherited environment values into MCP processes. | Verified |
| MAF-011 | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs` | MCP | OS-aware npm/node names but recursively scans global npx cache and chooses newest match. | Verified |
| MAF-012 | `src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs` | External tools | Contains another local process runner with independent timeout/kill/output behavior. | Verified |
| WB-001 | `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs` | Runtime nodes | Windows-only PowerShell/runas launcher; runtime plans are serialized as PowerShell command text. | Verified |
| WB-002 | `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureDirectDotNetCommandPolicy.cs` | Runtime nodes | Legacy command classifier is cmd/PowerShell-centric. | Verified |
| MGR-001 | `tools/App/CanDoItAll.Manager/CanDoItAll.Manager.csproj` | Manager | References System.Management in the general Manager project. | Verified |
| MGR-002 | `tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs` | Manager | WMI inventory on Windows and weak name-only Unix fallback; path comparisons are broadly case-insensitive. | Verified |
| MGR-003 | `tools/App/CanDoItAll.Manager/TailwindWatchSupervisorService.cs` | Manager | FileSystemWatcher-based change detection with partial cross-platform command branches. | Verified |
| MGR-004 | `tools/App/CanDoItAll.Manager/WatchSupervisorService.cs` | Manager | Main watch process supervisor; exact local characterization required. | Search-confirmed |
| MGR-005 | `tools/App/CanDoItAll.Manager/TuningExecutionAdapter.cs` | Manager | Additional process-launch surface; exact local characterization required. | Search-confirmed |
| PLUG-001 | `src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerHostToolService.cs` | Plugins | Constructs LocalWorkspaceProcessHost directly and maintains another environment/executable policy. | Verified |
| PROC-001 | `src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs` | Processes | Process drivers include a Platform layer, but this layer owns process strategy composition, not generic host OS services. | Verified |
| PROC-002 | `src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDescriptors.cs` | Processes | Standard adapter capability descriptors. | Verified |
| PROC-003 | `codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md` | Architecture | Explicitly assigns process semantics and recovery to Processes rather than MAF. | Verified |
| PROC-004 | `codex/bundles/MAF-Refactor/architecture/15-exact-code-adaptation-inventory.md` | Architecture | Current MAF refactor adaptation map and ownership constraints. | Verified |
| TEST-001 | `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | Tests | Primary unit test project. | Verified |
| TEST-002 | `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | Tests | Integration test project. | Verified |
| TEST-003 | `tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj` | Tests | Browser validation project. | Verified |
| TEST-004 | `tests/Unit/CanDoItAll.Tests.Unit/LocalWorkspaceProcessHostTests.cs` | Tests | Existing process-host tests. | Search-confirmed |
| TEST-005 | `tests/Playwright/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs` | Tests | Current application launch fixture uses ProcessStartInfo. | Search-confirmed |

All paths must be revalidated during A00/B00. Search-confirmed paths require direct content inspection.
