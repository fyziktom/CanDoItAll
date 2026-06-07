# Permission, Audit, And Sandbox Prerequisite Model

## Permission Modes

| Mode | Current bundle status | Allowed | Denied |
| --- | --- | --- | --- |
| Missing/None | Must deny | Nothing | Everything |
| VerificationOnly | Testable prerequisite | Inspect existing evidence and return diagnostics | Mutation, command execution, artifact writes, external calls |
| ManagerReadonly | Testable prerequisite | Read process facts and denial reasons | Claims, transitions, storage, workspace, finalizer, retries |
| ExecutionCapableFuture | Explicitly disabled | Nothing in this bundle | Any runtime execution |

## Audit Facts

Required audit facts for future driver requests:
- caller identity,
- process/run/step ids,
- lane,
- permission mode,
- requested operation,
- inspected evidence ids,
- denial reason,
- diagnostic hash,
- redaction status.

## Sandbox Policy

For now, sandbox policy is denial-only. Any execution-capable future mode must require:
- command allowlist,
- working directory policy,
- timeout,
- output capture hash,
- network policy,
- file-system policy,
- secret masking,
- failure semantics.
