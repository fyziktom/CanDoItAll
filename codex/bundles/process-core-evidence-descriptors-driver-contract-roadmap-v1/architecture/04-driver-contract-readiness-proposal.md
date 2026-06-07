# Driver Contract Readiness Proposal

## This bundle may add
- Documentation-only production proposal.
- Test-only sample schemas under bundle docs/tests.
- Negative architecture tests proving production source has no driver APIs.
- Permission/audit/sandbox requirements.

## This bundle must not add
- Production `IProcessDriver*` interfaces.
- Driver registry or DI registration.
- Runtime selectors or manager commands.
- Shell execution, Office/Graph calls, workspace/storage writes, process transitions or retry hooks.

## Future driver modes
| Mode | Description | State mutation |
| --- | --- | --- |
| VerificationOnly | Inspect existing Core/process facts and return diagnostics. | Denied |
| ManagerReadonly | Manager may request readonly checks and receive explanations. | Denied |
| ExecutionCapableFuture | Future explicit gate for bounded command/tool execution. | Not approved |

## Required audit facts for future production
- Caller identity
- Process/run/step ids
- Driver lane and mode
- Capability scope
- Inspected artifact/evidence ids
- Command/tool identity if any
- Denial reason or result reason
- Redacted diagnostics
- Hashes for captured outputs
- Timeout/sandbox policy identifier
