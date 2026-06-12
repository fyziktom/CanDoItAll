# .NET runtime command project-structure writeback

**Key:** `dotnet-runtime-command-writeback`
**Criticality:** High
**Autonomy level:** Guarded

Creates durable Run command, Run app, and Run tests project-structure nodes under the current process run node.

## Value
Makes local run and validation commands visible for QA, screenshots, release handoff, and future process replay.

## Permission model
Every step declares explicit operations and target scope so role permissions remain bounded and product mutation cannot leak into planning, review, validation, screenshot, or writeback work.

## Steps
### 1. Resolve .NET run and test commands (`resolve-dotnet-run-commands`)
- Step kind: Start
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: None
- Outputs: Typed manifest for planned Run command, Run app, and Run tests project-structure node payloads. Do not create project-structure nodes in this resolve step.
- Evidence: App type, command strings, working directories, ports, environment notes, and no-run-app rationale when applicable.

### 2. Write Run command project nodes (`write-run-command-nodes`)
- Step kind: Review
- Operation target scope: ExternalActionControlled
- Depends on: resolve-dotnet-run-commands
- Outputs: Run command parent node with Run app and Run tests child nodes under the process run node.
- Evidence: Project-structure write receipts, node ids, commands, and unresolved blockers.

### 3. Hand off runtime command nodes (`runtime-command-handoff`)
- Step kind: End
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: write-run-command-nodes
- Outputs: Parent-ready runtime command handoff.
- Evidence: Node ids, commands, command applicability, receipts, and blockers.
