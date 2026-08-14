# Workbench runtime nodes and terminal presentation

## Separation

```text
ProjectStructure metadata
    -> RuntimeNodePlanCompiler
    -> typed RuntimeExecutionPlan
    -> direct execution adapter
    -> optional terminal presentation adapter
```

The UI can display a command projection but never stores/executes that projection as the authoritative plan.

## Plan kinds

Prefer explicit plan kinds such as:

- direct executable;
- .NET project/application;
- Python environment;
- Node/npm tool;
- Docker recipe;
- explicit PowerShell script;
- explicit POSIX shell script;
- external application open;
- terminal-only session.

Each kind declares dependencies and policy. Do not make a generic “command string” the preferred model.

## Python

- Windows venv: `Scripts/python.exe`;
- Linux/macOS venv: `bin/python`;
- invoke interpreter directly;
- activation script is presentation only;
- Conda is a separately probed capability.

## Terminal

Terminal presentation may:

- open an interactive session for an already validated plan;
- create a controlled temporary script/launch description if required by the host;
- show unavailable/remediation state.

It may not:

- be required for headless direct execution;
- weaken path/approval/tool policy;
- reconstruct unescaped command text from arbitrary values;
- auto-select a random desktop terminal.

## Elevation

- Windows `runas` remains a separately authorized capability.
- Linux/macOS default to unavailable.
- `sudo`, `pkexec`, AppleScript, or password prompts require a separate explicit design/security review and are not part of the default implementation.

## Legacy metadata

Use bounded compatibility:

- identify known static command shapes;
- migrate simple values to typed fields;
- preserve explicit scripts as scripts;
- mark dynamic/encoded/ambiguous shell content unresolved;
- require operator repair rather than unsafe generic parsing.

## B02 implementation decision

### Current responsibility inventory

`ProjectStructureRuntimeLauncher` currently owns metadata parsing, physical-path authorization, runtime-plan construction, PowerShell command rendering, process creation, terminal presentation, elevation, and user-facing launch diagnostics. Its direct `Process.Start` path makes PowerShell both execution authority and presentation, while its Windows-only availability prevents a valid typed plan from running headlessly on Linux or macOS.

### Target ownership

- `ProjectStructureRuntimePlanCompiler` is a pure Workbench compiler. It accepts typed, already-authorized runtime-node definitions plus explicit host facts and produces executable, argv, environment, working directory, target, policy, and display fields. It performs no filesystem access and starts no process.
- `ProjectStructureRuntimePathResolver` remains the Workbench adapter over the Core logical/physical path authority. It resolves workspace paths and authorized external aliases and rejects link escapes before compilation.
- `ProjectStructureRuntimeExecutionAdapter` resolves the executable with the B01 executable locator and delegates the owned session to `ProjectStructureRuntimeSessionRegistry`. The registry retains the exact B01 session identity until natural completion or an explicit node stop; it never detaches a long-running child.
- `ProjectStructureRuntimeSessionRegistry` is host-lifetime Workbench composition. It owns one explicit dependency-injection child scope and therefore one canonical scoped `IWorkspaceLongRunningProcessHost` for its lifetime. It rejects duplicate starts for the same node, terminates by the exact owned session, preserves ownership after a cancelled/failed operator stop, and runs one idempotent host-shutdown cleanup task. An already-cancelled hosted-stop token cannot skip cleanup: every captured session receives an independent bounded termination/disposal attempt, one failure cannot skip later sessions, and only then is the process-host scope disposed. A new scoped launcher can recover and stop the same host-lifetime node session without introducing a second process authority.
- `ProjectStructureTerminalPresenter` and `ProjectStructureRuntimeElevationAdapter` are optional Workbench presentation adapters. They may start only the selected terminal or the explicitly authorized Windows `runas` process; they are not ordinary command fallbacks.
- `ProjectStructureRuntimeLauncher` becomes a thin facade that validates legacy metadata, asks the path resolver for authorized inputs, invokes the pure compiler, probes capabilities, enforces explicit operator confirmation for every script plan marked `RequiresApproval`, and delegates the selected launch mode.
- Agent-facing capability projections never include resolved command text or physical working directories. Those fields are available only to the operator-selected UI projection; agent execution receives empty physical projection fields and continues to use opaque logical/external-target authority.

All types stay in `CanDoItAll.Modules.Workbench`; no project reference is added. Dependency direction remains `Workbench -> AgentFramework.Core -> SharedKernel/Contracts`. Process-domain semantics remain outside MAF.

### Pattern selection record

The observed force is one class changing for four independent reasons: plan policy, path authority, process execution, and desktop presentation. Extracted classes are sufficient; a new project or general platform service would add dependency surface without independent packaging or SDK needs. The selected patterns are a pure compiler plus narrow execution/presentation adapters, with the existing launcher retained as a compatibility facade. A generic command-string strategy and a second process-host abstraction are rejected.

### Testability and shallow-separation gate

- Compiler tests construct typed definitions directly and do not construct the launcher or touch the filesystem.
- Negative tests prove shell operators, encoded/dynamic wrappers, implicit elevation, and foreign/escaping paths remain rejected.
- Adapter tests use a recording B01 process host and assert exact executable/argv/environment values.
- Lifecycle tests prove exact-session stop, duplicate prevention, recovery through a new scoped adapter, natural completion, host-shutdown cleanup, and no detach.
- Approval tests prove a script plan cannot launch through the compatibility overload or approval-aware overload until explicit operator confirmation is supplied.
- Capability tests prove agent projections do not disclose a private physical-path sentinel while operator projections retain the local display values.
- Host-contract tests cover Windows and Unix Python layouts, Windows-only elevation, headless terminal state, and configured Linux terminal state without pretending that deterministic macOS fixtures are actual-host proof.
- A composition test resolves the Workbench registrations and verifies that the launcher delegates direct execution to the configured B01 host.
- Closure requires `ProjectStructureRuntimeLauncher` to contain no `Process.Start`, PowerShell encoding, terminal selection, or executable-resolution implementation.
