# Runtime/tools/process prepared findings

These findings are preparation evidence, not a substitute for the mandatory local scan.

## F-024 — P1: Process host

**Paths:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`

**Current observation:** The central workspace host already uses typed argv and cross-platform ProcessStartInfo, but Unix process-group, signal, and tree-kill behavior is not proven.

**Risk:** Timeouts may leave grandchildren running or terminate incompletely.

**Required direction:** Characterize Kill(entireProcessTree), cancellation, and stream drain on all target OSes before adding a purpose-specific native adapter.

**Confidence:** `Verified`

## F-025 — P0: Environment semantics

**Paths:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandEnvironmentPolicy.cs`

**Current observation:** The inherited allowlist is Windows-heavy and stored in an OrdinalIgnoreCase dictionary on every OS.

**Risk:** Unix variables can be dropped or case-collapsed, while Windows-only variables are treated as universal.

**Required direction:** Separate safe common variables from OS/tool profiles and preserve host environment key semantics.

**Confidence:** `Verified`

## F-026 — P1: Executable resolution

**Paths:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceExecutableLocator.cs`; `src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/McpExecutableResolver.cs`

**Current observation:** Two resolvers have different suffix and fallback rules; the workspace locator probes Windows suffixes on Unix.

**Risk:** The same capability can resolve differently in MCP, workspace tools, plugins, and Workbench.

**Required direction:** Create one pure candidate/resolution contract with OS-specific leaf rules, execute-bit checks, stable diagnostics, and no implicit shell.

**Confidence:** `Verified`

## F-027 — P0: Workbench runtime nodes

**Paths:** `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`

**Current observation:** Runtime availability is Windows-only and every .NET, Docker, Conda, Python, or script plan is rendered as PowerShell text launched through powershell.exe/runas.

**Risk:** Unix hosts cannot launch runtime nodes, and ordinary direct commands inherit shell quoting/injection and presentation coupling.

**Required direction:** Compile typed executable/argv/environment plans; execute directly; treat explicit shell scripts and optional terminal presentation as separate capabilities.

**Confidence:** `Verified`

## F-028 — P1: Python environments

**Paths:** `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`

**Current observation:** Virtual environments assume Scripts/Activate.ps1.

**Risk:** Linux/macOS virtual environments use different layouts and do not require activation for deterministic execution.

**Required direction:** Resolve and invoke the environment interpreter directly; keep activation only as optional terminal display behavior.

**Confidence:** `Verified`

## F-029 — P1: Elevation

**Paths:** `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`

**Current observation:** Elevation is modeled as Windows runas.

**Risk:** Mapping this automatically to sudo/pkexec would create a privilege-escalation and interactive-hang risk.

**Required direction:** Make elevation a separate optional capability that is unavailable by default on Unix/macOS unless an explicitly governed adapter is configured.

**Confidence:** `Verified`

## F-030 — P0: Process ownership

**Paths:** `tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`

**Current observation:** Windows discovery uses WMI command lines; Unix fallback only searches process name and often lacks command-line identity.

**Risk:** Recovery/cleanup can miss owned processes or terminate an unrelated process with the same name.

**Required direction:** Persist launched-process identity first, then use OS-specific discovery as a bounded recovery aid with PID/start-time/user/executable/command/workspace proof.

**Confidence:** `Verified`

## F-031 — P1: Manager Windows dependency

**Paths:** `tools/App/CanDoItAll.Manager/CanDoItAll.Manager.csproj`; `tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`

**Current observation:** System.Management and WMI behavior live in the general Manager project.

**Risk:** Windows-only implementation details remain easy to call from neutral code and complicate platform analysis.

**Required direction:** Move WMI behind a Windows-only leaf adapter/registration boundary; keep the Manager contract neutral.

**Confidence:** `Verified`

## F-032 — P1: MCP command policy

**Paths:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/LocalMcpCommandPolicy.cs`; `src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/McpExecutableResolver.cs`

**Current observation:** Allowed names and executable discovery use separate case/suffix models.

**Risk:** A command can pass the allowlist but resolve unexpectedly, or be rejected on one OS despite an equivalent installed executable.

**Required direction:** Validate the resolved executable identity against a capability-owned allowlist using host-appropriate rules.

**Confidence:** `Verified`

## F-033 — P1: Playwright MCP setup

**Paths:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs`

**Current observation:** Resolver recursively scans a global npx cache and chooses the most recently written cli.js.

**Risk:** Selection is nondeterministic and can cross workspace/user trust boundaries.

**Required direction:** Use a controlled, versioned application tool root with explicit integrity/version evidence; do not depend on global cache discovery for production.

**Confidence:** `Verified`

## F-034 — P1: Duplicate process runners

**Paths:** `src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`; `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`

**Current observation:** External tools implement an independent local runner with distinct timeout, output, and kill behavior.

**Risk:** Security and portability fixes can land in one runner but not the other.

**Required direction:** Reuse one execution primitive or explicitly prove why a separate boundary is required; share tested low-level process semantics.

**Confidence:** `Verified`

## F-035 — P1: Diagnostics leakage

**Paths:** `src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`; `src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpEnvironmentBinder.cs`

**Current observation:** External process stdout/stderr and environment bindings can flow into diagnostic messages.

**Risk:** Secrets or sensitive local paths can be captured in receipts, CI logs, or agent context.

**Required direction:** Persist only approved environment names, cap/redact output, and route secret values through the runtime secret boundary.

**Confidence:** `Verified`

## F-036 — P1: Docker plugin

**Paths:** `src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerHostToolService.cs`

**Current observation:** The Docker plugin constructs LocalWorkspaceProcessHost directly and maintains its own executable/environment policy.

**Risk:** It bypasses owned process registries, composition-selected adapters, and later portability fixes.

**Required direction:** Inject the authoritative host execution port, capability probe, and environment policy.

**Confidence:** `Verified`

## F-037 — P1: External FileTools package

**Paths:** `src/Integration/CanDoItAll.FileTools.Integration/CanDoItAll.FileTools.Integration.csproj`; `src/Integration/CanDoItAll.FileTools.Integration/ConfiguredDesktopFileLauncher.cs`

**Current observation:** Desktop launch behavior is owned by package 0.1.18 outside this repository.

**Risk:** The main application can claim macOS/Linux support without package/runtime evidence.

**Required direction:** Run a pinned compatibility matrix, record package capabilities, and fail closed or quarantine desktop launch when the package lacks a supported adapter.

**Confidence:** `Verified dependency, behavior requires execution`

## F-038 — P0: Process architecture ownership

**Paths:** `codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`; `src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs`

**Current observation:** The current refactor intentionally moved process semantics and recovery to Processes.

**Risk:** A generic OS-service refactor inside MAF or Infrastructure could reintroduce reverse ownership and duplicate domain policy.

**Required direction:** Expose narrow host capabilities at execution boundaries; let Processes own eligibility, strategy, recovery, evidence, and domain failure semantics.

**Confidence:** `Verified architectural invariant`

## F-039 — P1: Process driver platform layer

**Paths:** `src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs`

**Current observation:** ProcessDriverLayer.Platform exists and could be misread as a generic OS abstraction layer.

**Risk:** Host implementation facts become process-domain truth or vice versa.

**Required direction:** Use the layer only for process strategy packages that require declared host capabilities; do not move filesystem/secrets/process primitives into process drivers.

**Confidence:** `Architecture conclusion`

## F-040 — P1: Cross-platform runtime evidence

**Paths:** `.github/workflows-disabled/ci.yml`; `tests/Playwright/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`

**Current observation:** There is no active Windows/Linux/macOS runtime-node, Manager, MCP, or plugin proof.

**Risk:** A successful neutral build can mask runtime-only failures in process, terminal, desktop, or secret integration.

**Required direction:** Add focused actual-host tests and failure injection after the core portability gate lands.

**Confidence:** `Verified`

## F-041 — P2: Portable foundation

**Paths:** `CanDoItAll.slnx`; `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`

**Current observation:** Projects target neutral net10.0 and the central workspace process host already uses direct typed arguments.

**Risk:** None; this reduces required rewrite scope.

**Required direction:** Preserve and extend these foundations rather than replacing them with a new parallel runtime.

**Confidence:** `Verified`

## F-042 — P2: Current architecture

**Paths:** `src/Processes`; `src/Processes/Drivers`; `src/Foundation/CanDoItAll.Security.Abstractions`

**Current observation:** Recent refactors created clearer process, runtime, and security boundaries.

**Risk:** Portability changes could accidentally collapse these new boundaries.

**Required direction:** Treat the latest architecture as authoritative and add portability through purpose-owned adapters.

**Confidence:** `Verified`
