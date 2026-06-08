# Driver Runtime Deferral

## Decision
Do not implement a generic driver runtime in this bundle.

## Specifically Deferred
- `IProcessDriverRegistry`
- `IProcessDriverSelector`
- `IProcessDriverHost`
- `AddProcessDrivers(...)`
- manager commands
- workflow/scheduler hooks
- runtime driver discovery
- execution-capable lane
- shell/Graph/workspace/storage/process mutation

## Future Approval Prerequisites
- Persistent audit model.
- Runtime owner for allowed side effects.
- Capability policy evaluator.
- Secret masking and output hashing.
- Timeout/cancellation policy.
- Sandbox and network/filesystem isolation.
- Human approval gate for execution-capable mode.
- Production negative tests proving read-only modes cannot mutate state.
