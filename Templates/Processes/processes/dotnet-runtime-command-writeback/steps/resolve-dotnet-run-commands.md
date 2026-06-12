# Resolve .NET run and test commands

Read current project structure, architecture handoff, implementation evidence, and QA proof to determine the product root, app type, runtime project, test project, working directory, environment variables, ports, and stop behavior. Produce a manifest with planned Run command, Run app, and Run tests node payloads. Do not call project-structure mutation tools in this resolve step. If the delivery target is a class library or otherwise non-runnable, the planned Run app payload must still carry a not-applicable reason rather than being omitted.

## Contract
- Inputs: Architecture handoff, implementation evidence, QA evidence, and project-structure run node context.
- Outputs: Typed manifest for planned Run command, Run app, and Run tests project-structure node payloads.
- Evidence: App type, command strings, working directories, ports, environment notes, and no-run-app rationale when applicable.
- Operation target scope: `ExternalProductTargetReadOnly`
