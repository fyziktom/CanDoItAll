# Runtime Execution And Shell Portability

Runtime execution is typed and host-aware. The application resolves an executable,
argument list, working directory, environment, target paths, approval requirement, and
lifetime before starting a process. A shell is used only when the selected runtime type
explicitly declares shell semantics.

## Execution Surfaces

Two script surfaces have intentionally different contracts:

| Surface | Input | Windows | Linux and macOS |
| --- | --- | --- | --- |
| Project Structure PowerShell node | Explicit PowerShell command or `.ps1` path | `pwsh`, then `powershell` | `pwsh` |
| Project Structure POSIX shell node | Explicit shell command or script path | Unsupported | `sh` |
| File-skill Python script | `.py` | `python` | `python` |
| File-skill PowerShell script | `.ps1` | `pwsh`, then `powershell` | `pwsh`, then `powershell` if available |
| File-skill shell script | `.sh` | `bash` when installed | `bash` |
| File-skill JavaScript | `.js` | `node` | `node` |

Project Structure POSIX shell nodes deliberately use the POSIX `sh` contract. They do
not require Bash and are rejected on Windows. File-skill `.sh` execution deliberately
uses `bash` because skill scripts may rely on Bash behavior. Bash is therefore a
capability for `.sh` file skills, not a global fallback for missing runtimes or a wrapper
around ordinary Project Structure nodes.

## Project Structure Runtime Plans

Workbench compiles runnable nodes as follows:

| Node kind | Executable contract | Shell involvement |
| --- | --- | --- |
| .NET runtime, watch, release | `dotnet` plus typed project/launch arguments | None |
| Python environment | `conda run ... python` or the selected environment's `Scripts/python.exe` / `bin/python` | None |
| Docker infrastructure | Explicit executable and tokenized arguments from the Docker runtime metadata | None unless the operator explicitly models a shell node |
| Direct console, EF, Tailwind, or other command | Explicit executable plus tokenized arguments | None |
| PowerShell script | `pwsh`/`powershell` with `-File` or `-Command` | Explicit PowerShell node only |
| POSIX shell script | `sh` with a script path or `-c` command | Explicit POSIX node only |

Direct execution uses the owned workspace process host. The executable is resolved from
the typed candidate list, arguments are passed through `ProcessStartInfo.ArgumentList`,
and the process session owns output bounds, cancellation, stop, descendant cleanup, and
identity checks. Executable discovery never turns an untrusted persisted path into
authority by itself.

Missing `dotnet`, Python, Conda, Docker, PowerShell, `sh`, Bash, Node, or a terminal
adapter produces a typed unavailable/dependency-missing result. There is no implicit
switch to another runtime and no command-string fallback.

## Process Environment And Launch Failures

The process host starts every child from an empty environment. Every launcher (agent
workspace commands, `dotnet run` sessions, Workbench runtime nodes and their terminal
presentation, local MCP servers, Docker, git, external-process capabilities) composes it
through `WorkspaceCommandEnvironmentPolicy`:

- **Allowlist.** The inherited allowlist is `PATH`, `TEMP`, `TMP`, `TMPDIR`, `HOME`, and
  the `USERPROFILE`/`SystemRoot`… family on Windows or `USER`/`SHELL` on Unix. Host
  secrets such as `OPENAI_API_KEY` never pass.
- **Toolchain settings.** The toolchain's own variables are chosen from the program that
  actually runs (`dotnet`, `python`/`py`/`conda`, `pwsh`/`powershell`, `node`/`npm`/`npx`,
  `docker`, `git`), with the tool name as a fallback.
- **Never empty.** An empty `TEMP`, `TMP`, `TMPDIR`, `HOME`, `USERPROFILE` or `PATH` is
  never set: an empty `TEMP` makes Windows fall back to its own folder, and `dotnet run`
  then fails with MSB1025. A null explicit value unsets a variable.
- **Unix home.** On Linux and macOS a host without `HOME` gives children
  `<state root>/toolchain-home` (mode 0700) as `HOME` and `DOTNET_CLI_HOME`.
- **Operator options.** These live under `AgentFramework:ProcessEnvironment` and are off by
  default:
  - `NetworkTrust` passes the host's proxy and certificate variables (`HTTP(S)_PROXY`,
    `NO_PROXY`, `SSL_CERT_FILE`, `NODE_EXTRA_CA_CERTS`, …).
  - `AdditionalInheritedNames` lists further names. Names that look like secrets (`KEY`,
    `TOKEN`, `SECRET`, `PASSWORD`, `CREDENTIAL`) or that inject code (`DOTNET_STARTUP_HOOKS`,
    `MSBUILD*`, `NODE_OPTIONS`, `LD_PRELOAD`, `DYLD_*`, …) are rejected and logged at
    startup; invalid options never stop the host.
  - When a command fails with network-looking output while network trust is off and the
    host has such variables, the result names them (names only) and points to the option.
- **Terminal presentation.** A runtime node opened in a terminal gets the same composed
  environment. On Windows the PowerShell prelude removes every other variable; on Linux
  and macOS the runtime runs through `env -i`.

A process that cannot start carries a typed reason:
- `ProcessStartFailed.ExecutableNotFound`
- `ExecutableNotRunnable`
- `WorkingDirectoryMissing`
- `WorkingDirectoryTooLong`
- `AccessDenied`
- `ProcessOwnershipFailed`
- `Unknown`

Where each surface shows it:
- **Agents** receive the code and a sanitized sentence (program names, never folders).
  The durable trace records the code, and the effect as not committed when nothing
  started.
- **Operators** see the folder, the limit and the remedy on the runtime node. The host
  log has the full operating-system error.
- **Process steps** that fail this way get `process.adapter.agent_tool_launch_failed` with
  a next action.
- **Chats** show failed tool results as a "Tool result" step in the execution stream.

On Windows with long paths disabled no process can start in a folder whose path is longer
than 258 characters. Workspace commands use a temporary `subst` drive for long workspace
paths; if that alias cannot be created, the command fails with `WorkingDirectoryTooLong`
instead of an opaque error.

**Script environment access.** An agent's PowerShell and Python scripts receive only the
basic variables unless the agent has the workspace-tool permission "Scripts may read
environment variables" (`canScriptsReadEnvironment`). The operator's network-trust and
extra names reach script processes only with that permission.

Without the permission, a script is inspected before it runs, and it is refused when it
reads a variable outside the basic set, lists all variables, reads a name computed at run
time, or cannot be inspected. The refusal names the finding and the setting. It happens
before any process starts: immediately with auto-approval, otherwise after the approval of
that call. Inspection is a guard rail, not a sandbox, because the environment itself is
already reduced to the allowlist.

A process that exits with a non-zero code inside the early observation window fails the
launch with its exit code and a bounded stderr tail. A later exit is kept per node as the
last exit record, unless the operator stopped the session, and is reported in the node's
quick actions and action guidance. Agent-facing guidance reports the exit code without
the process output.

For a node whose web link is a loopback URL, the page waits for the application to answer
HTTP before opening the web preview. It stops waiting when the process exits, when the
operator stops the node or leaves the page, or after the readiness timeout. The embedded
browser gives a loopback application on a different origin from the host its own origin
(`allow-same-origin`) so that SPA and WebAssembly apps can render. Same-origin and remote
pages keep the restricted sandbox.

## Read-Only Targets And Build Output

An agent or process step may read a product folder without being allowed to change it,
for example a review or QA step. `workspace_dotnet_restore`, `_build`, `_test`, `_run`
and `workspace_dotnet_publish` on such a target add `--artifacts-path` so that `bin` and
`obj` are written to `<workspace>/.build/<key>` instead of the product folder. The
product stays byte-for-byte unchanged.

- **When.** The project (or the working directory when there is no project) is an
  external target the current execution may read but not write. Writable targets and
  workspace projects keep the ordinary layout.
- **Key.** The key is the first 12 hexadecimal characters of the SHA-256 of the most
  specific read-only root alias. A solution and its projects under that root share one
  folder, so a later `test --no-build` or `run --no-build` finds the earlier build, also
  in a later execution.
- **Persistence.** The folder is never deleted automatically. It holds build products
  that later steps and runtime nodes may still use.
- **Visibility.** The result message ends with "Build output for this read-only target is
  kept in '.build/<key>'; the target folder is not modified." The receipt lists the
  folder as a target.
- **Switch.** `AgentFramework:ProcessEnvironment:RedirectReadOnlyDotnetOutput` (default
  `true`) turns the redirect off for a product whose build cannot use a separate output
  folder. Its builds then write into the product again.

A test project in a writable folder that references a project in a read-only folder is
not redirected, because the decision follows the project being built.

## Path And Approval Boundary

Runnable metadata is validated and canonicalized before launch:

- working directories and file targets must be inside the managed workspace or an
  explicitly authorized external target;
- `.NET` nodes resolve to an exact project file and do not guess among nested projects;
- shell content that cannot be inspected safely, such as encoded PowerShell content, is
  rejected;
- explicit shell nodes require approval;
- agent-created runtime nodes are checked against the execution's audited path authority;
- path traversal, foreign physical syntax, and symlink/reparse traversal fail closed.

The plan remains authoritative when a terminal is used only for presentation. The
terminal does not reinterpret the node into a different runtime contract.

## Terminal Presentation And Elevation

Direct execution does not require a terminal. Interactive terminal presentation is an
optional host capability configured under `Workbench:RuntimePresentation`:

```json
{
  "Workbench": {
    "RuntimePresentation": {
      "EnableWindowsTerminal": true,
      "LinuxTerminalExecutable": "/usr/bin/x-terminal-emulator",
      "LinuxTerminalArgumentPrefix": ["-e"],
      "MacOsTerminalExecutable": "",
      "MacOsTerminalArgumentPrefix": []
    }
  }
}
```

Windows uses PowerShell as its default presentation adapter. Linux and macOS require an
explicit terminal executable and argument prefix; an empty configuration is valid on a
headless host. The prefix must make the terminal treat the following typed runtime
executable and arguments as its child command.

Elevation is separately authorized. Windows supports explicit `runas` only for compatible
plans. Linux and macOS do not add `sudo`, `pkexec`, AppleScript, password prompts, or any
other elevation fallback.

## Host Profiles And Capabilities

The runtime resolves one of `WindowsInteractive`, `WindowsHeadless`, `LinuxInteractive`,
`LinuxHeadless`, `MacOsInteractive`, or `MacOsHeadless`. The chosen profile must match the
actual OS and the configured interactive/headless secret-vault usage. Optional desktop,
terminal, native process-discovery, or local-open capabilities may be unavailable without
blocking the headless Web core; mandatory roots, database, migrations, and selected strong
secret providers block readiness when unavailable.

Service and container profiles must keep a stable `CANDOITALL_HOST_BINDING_ID` and
explicit purpose roots. See [Installing instances](../operations/installing-instances.md)
for platform settings and
[Storage, paths, and host portability](storage-and-path-portability.md) for physical-path
authority.

## Change Checklist

When adding an executable runtime or script type:

1. Add a typed plan kind and exact executable candidates.
2. Pass structured arguments and environment values; do not concatenate a general shell
   command.
3. Declare OS and architecture support explicitly.
4. Declare host capabilities and approval/side-effect policy.
5. Resolve every working directory and target through workspace/external-target guards.
6. Use the shared owned-process host for lifecycle and descendant termination.
7. Add actual-host tests for each supported OS and missing-dependency tests that prove no
   fallback occurs.

The relevant implementation entry points are
[`ProjectStructureRuntimePlan.cs`](../../src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimePlan.cs),
[`ProjectStructureRuntimeAdapters.cs`](../../src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeAdapters.cs),
and
[`WorkspaceCommandPlanBuilder.cs`](../../src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs).
