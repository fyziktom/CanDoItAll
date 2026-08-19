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
