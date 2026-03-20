# Proposed class map

## Hosting
- `Program`
  - bootstrap only
- `McpServerRegistration`
  - DI registration extension
- `McpServerRuntime`
  - optional high-level runtime service

## Configuration
- `McpServerOptions`
  - root options object
- `McpServerOptionsValidator`
  - fail-fast validation and normalization

## Runtime
- `SessionCoordinator`
  - central orchestration service
- `AppRuntimeManager`
  - app session lifecycle
- `AppSession`
  - current and historical state
- `SessionCompatibilityComparer`
  - idempotent start compatibility

## Operations
- `OperationRegistry`
  - long-running build/test tracking
- `OperationRecord`
  - operation state
- `BuildOperationRunner`
  - build process execution
- `TestOperationRunner`
  - test process execution and summary parsing

## Processes
- `ProcessSupervisor`
  - start/stop/observe processes
- `ManagedProcess`
  - live process wrapper
- `ManagedProcessRecord`
  - persisted metadata
- `IProcessTreeTerminator`
  - cross-platform abstraction
- `WindowsProcessTreeTerminator`
- `UnixProcessTreeTerminator`

## Logging
- `LogEntry`
  - structured log line
- `RingLogBuffer`
  - in-memory cursorable log storage
- `FileLogStore`
  - optional NDJSON persistence
- `LogRedactor`
  - secret masking

## Health & waits
- `HttpHealthProbe`
  - health URL polling
- `HealthSnapshot`
  - structured health state
- `WaitEngine`
  - app and operation waiting

## Diagnostics & security
- `StartFailureDiagnoser`
  - categorization and evidence extraction
- `PathGuard`
  - workspace path enforcement
- `EnvironmentOverlayFilter`
  - whitelist env pass-through
- `StaleProcessRegistry`
  - persisted ownership and cleanup support
