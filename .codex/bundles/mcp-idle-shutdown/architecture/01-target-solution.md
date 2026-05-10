# Target Solution

- Add a shared `McpIdleShutdownOptions` and idle shutdown service under `CanDoItAll.Mcp.Core.Hosting`.
- Register the service through a small hosting extension so each MCP opts in with a strongly typed options projection instead of duplicating lifecycle code.
- Expose an `IMcpIdleActivityTracker` abstraction with explicit operation scopes. Tool wrappers create a scope around each invocation.
- The service requests `IHostApplicationLifetime.StopApplication()` only when all conditions are true:
  - idle shutdown is enabled
  - the configured timeout has elapsed since the latest recorded activity
  - no tool invocation scope is active
- Components and SSH Ops keep their existing settings models and add an `IdleShutdown` options object under `Server`.
- Defaults:
  - Components: enabled, 5 minutes, checked every 15 seconds
  - SSH Ops: enabled, 30 minutes, checked every 30 seconds
- The implementation must log the idle shutdown decision with the timeout and idle duration. It must not swallow exceptions or hide option validation failures.
