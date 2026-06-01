# Resolve .NET run and test commands

Read current project structure, architecture handoff, implementation evidence, and QA proof to determine the product root, app type, runtime project, test project, working directory, environment variables, ports, and stop behavior. Produce commands for Run app and Run tests. If the delivery target is a class library or otherwise non-runnable, the Run app node must still be created with a not-applicable reason rather than being omitted.

## Contract
- Inputs: Architecture handoff, implementation evidence, QA evidence, and project-structure run node context.
- Outputs: Typed manifest for Run command, Run app, and Run tests project-structure nodes.
- Evidence: App type, command strings, working directories, ports, environment notes, and no-run-app rationale when applicable.
- Operation target scope: `ExternalProductTargetReadOnly`
