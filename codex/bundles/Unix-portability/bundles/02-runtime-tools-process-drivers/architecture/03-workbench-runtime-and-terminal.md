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
