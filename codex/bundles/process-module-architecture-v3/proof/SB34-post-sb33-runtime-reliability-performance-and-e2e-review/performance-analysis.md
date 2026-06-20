# SB34 Performance Analysis

## Findings

- CodeAnalytics snapshot `snap-20260620150010-362fab32` found no blocking scoped architecture errors for the Process runtime/application/adapter surfaces inspected.
- `ProcessRuntimeDispatchQueueServices.cs` used unbounded channels for immediate and recovery dispatch. That is a real dispatcher hot-path risk because backpressure was not explicit.
- The queue inserted dedupe state before writing. Cancellation or write failure could leave a queued marker without a queued item, which is a stuck-run risk.
- `ProcessRuntimeIntegrationServices.cs` remains a large integration service and still carries broader LINQ/list/dictionary pressure. SB34 only changed the measured high-signal issue in this pass: compiled regex fields in the adapter.
- `ProcessRuntimeDispatchApplicationService` still contains the existing strategy-isolation `Task.Run`. This is intentionally preserved because the focused timeout test proves it prevents a strategy that blocks before returning a task from freezing the dispatcher.

## Changes

- Replaced unbounded dispatch channels with bounded channels and capacity options.
- Added failure/cancellation cleanup for queue dedupe state.
- Split queue/options classes out of dispatch worker orchestration.
- Replaced adapter compiled regex fields with `[GeneratedRegex]` methods.
- Added queue tests for cancellation cleanup, pending-run dedupe, and invalid capacity.

## Remaining Risk

`ProcessRuntimeIntegrationServices.cs` and several Blazor/workbench files remain too large for comfortable ownership. The next architecture slice should split domain-specific integration behavior into smaller drivers/strategies and keep generic runtime files free of project/workbench scenario rules. SB34 records this risk but does not broad-refactor it because the fresh e2e path was already passing and the safe fix was narrower.
