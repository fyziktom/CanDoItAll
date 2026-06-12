# Write Run command project nodes

Create or reuse an organizational runtime command group under the current process run node, then create or update child nodes Run app and Run tests as runtime-capable project-structure nodes. If a grouping node is needed, use a non-runnable operations organizer such as `ProjectBlock` + `operations`; do not create a `ProjectBlock` + `delivery` node named like a runnable command. Run app must be an `Environment` node with subtype `dotnet-runtime`, `dotnet-watch`, or `dotnet-release` when the target is runnable, with environment metadata such as project path, launch profile, protocol, and localhost URL when known. Run tests, build, and utility commands must be `Script` nodes with subtype `console` or `powershell`, with script metadata for command, arguments, and working directory. Do not create runnable command nodes as `ProjectBlock` + `delivery` or any other delivery block. Each child node must include command, working directory, app type, source evidence, and any required environment or cleanup notes. Use project-structure write tools only through this externally controlled step; do not mutate product files.

## Contract
- Inputs: .NET run command manifest and current process run project-structure node.
- Outputs: Runtime-capable Run app and Run tests nodes under the process run node, plus a grouping node only when needed for organization.
- Evidence: Project-structure write receipts, node ids, commands, and unresolved blockers.
- Operation target scope: `ExternalActionControlled`
