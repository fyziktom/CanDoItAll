# Target Solution

## Before

```text
ProcessRunAutomationDispatchService
  -> IProcessAutomationExecutionClient
      -> AgentFramework workspace service
  <- ExecutionRunResult / ExecutionRunDetail / ExecutionRunRecord / AgentFramework exceptions
```

## After This Bundle

```text
ProcessRunAutomationDispatchService
  -> IProcessAutomationExecutionClient
      -> AgentFramework workspace service
      -> maps AgentFramework runtime details
  <- ProcessAutomationExecutionResult
  <- ProcessAutomationExecutionDetail
  <- ProcessAutomationExecutionRecord
  <- ProcessAutomationExecutionFailure
```

The client remains the adapter and may reference AgentFramework runtime types. Dispatcher partials should consume process-owned snapshots only.

## Explicit Non-Goal

This is not the final process core. It is an execution-boundary hardening step that makes later core extraction safer.
