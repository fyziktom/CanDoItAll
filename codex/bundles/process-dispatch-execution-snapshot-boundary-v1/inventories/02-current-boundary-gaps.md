# Current Boundary Gaps

| Gap | Current state | Desired next state |
| --- | --- | --- |
| Result type | Client returns `ExecutionRunResult` | Client returns neutral `ProcessAutomationExecutionResult` |
| Detail type | Client returns `ExecutionRunDetail` | Client returns neutral `ProcessAutomationExecutionDetail` |
| List type | Client returns `ExecutionRunRecord` | Client returns neutral `ProcessAutomationExecutionRecord` where used by dispatcher |
| Failure type | Dispatcher catches AgentFramework exceptions | Client normalizes failures into process-owned result/exception |
| Receipt access | Dispatcher inspects AgentFramework receipts | Dispatcher uses neutral receipt snapshots/helper |
| Required-tool family | Tool observation spread across dispatcher | Small helper centralizes snapshot-based observation |
| Contracts | Request-only contracts | Request + result/detail/failure snapshots |
