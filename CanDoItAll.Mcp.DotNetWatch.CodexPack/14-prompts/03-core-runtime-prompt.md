# Core runtime prompt

Implement the runtime/session core for the CanDoItAll MCP server.

## Implement now
- `AppSession` model
- `WorkspaceExecutionLock`
- `ProcessSupervisor`
- Windows and Unix process-tree termination abstraction
- `RingLogBuffer` with monotonic cursor/sequence
- `AppRuntimeManager`
- `candoitall_app_start`
- `candoitall_app_stop`
- `candoitall_app_status`
- `candoitall_app_logs`

## Behavioral rules
- `app_start` must support `WatchRun` and `RunOnce`.
- `app_start` must reuse an existing compatible session when `reuseIfCompatible=true`.
- Compatibility must compare at least project path, mode, framework, configuration, launch profile, app args, and relevant env overlay.
- `app_stop` must terminate the full process tree.
- Process stdout/stderr must be captured internally, not printed to host stdout.
- Record observed URLs when they can be detected safely.

## Deliver
- implementation summary
- tests added or updated
- known open items for wait/health integration
