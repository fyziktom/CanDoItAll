# Process Runtime Host Code-First Integration Gate

## Status

This is now a historical gate record.

The earlier runtime-host verification plan referenced removed `Automation/Dispatch`
source files and should not be used as the source of truth for current process
execution.

Current source of truth:

- [Processes, MAF, and Providers Implementation Map](../processes-maf-providers-implementation-map.md)

## Current Position

The active process runtime is owned by the process module application services,
runtime engine, Agent Framework adapter layer, project-structure bridge, and HTTP
API surface.

Current runtime entry points:

- Launch: `ProcessLaunchApplicationService`
- Dispatch: `ProcessRuntimeDispatchApplicationService`
- Runtime progression: `ProcessRuntimeEngine`
- Agent execution: `AgentFrameworkProcessExecutionAdapter`
- Project-structure bridge: `ProjectStructureProcessNodeService`
- HTTP control plane: `ProcessesApi`

The current source tree does not include an execution-capable process driver
runtime host and does not register direct `processes_*` agent runtime tools.

## Future Gate

Before reintroducing a process runtime-host or direct process agent-tool surface,
the implementation should pass a new hardening gate that covers:

- Runtime lifecycle ownership and cancellation semantics.
- Durable audit and replay behavior.
- Authorization, revocation, and emergency stop behavior.
- Tool and driver allow-listing.
- Typed request and response models for every operation.
- Approval classifications for effectful operations.
- Provider and process access metadata in logs without leaking secrets.
- API and tool contract snapshots.
- Negative tests for denied operations and missing authorization.
- Updated docs and skills that name only source-backed behavior.

Until that gate exists, this document should be treated as historical context and
not as an implementation checklist.
