# Write Run command project nodes

Create or reuse the Run command parent node under the current process run node, then create or update child nodes Run app and Run tests. Each child node must include command, working directory, app type, source evidence, and any required environment or cleanup notes. Use project-structure write tools only through this externally controlled step; do not mutate product files.

## Contract
- Inputs: .NET run command manifest and current process run project-structure node.
- Outputs: Run command parent node with Run app and Run tests child nodes under the process run node.
- Evidence: Project-structure write receipts, node ids, commands, and unresolved blockers.
- Operation target scope: `ExternalActionControlled`
