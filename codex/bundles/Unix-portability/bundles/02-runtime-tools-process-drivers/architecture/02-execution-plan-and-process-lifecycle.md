# Execution plan and process lifecycle

## Canonical typed plan

Recommended fields:

```text
PlanId / CorrelationId
CapabilityId / RecipeId
Executable: explicit path or capability-owned command identity
Arguments: ordered immutable list
WorkingDirectory: verified physical path derived from authorized logical scope
EnvironmentBindings:
  - literal non-secret value
  - inherited approved name
  - secret reference resolved at launch
Input mode / bounded stdin
Timeout and cancellation
Stdout/stderr limits and redaction policy
Expected exit policy
Lifecycle: one-shot | kept-alive | supervised
Boundary/isolation facts
Side-effect/approval metadata
Display projection
```

Display text is never parsed back into execution.

## Executable resolution

- resolve explicit paths after path authority and link checks;
- resolve command names through deterministic PATH order;
- Windows honors approved PATHEXT candidates;
- Unix/macOS require an executable regular file or an explicitly supported interpreter/script model;
- compare/authorize the resolved identity using capability rules;
- report missing, not executable, ambiguous, disallowed, and unsupported separately;
- do not silently invoke a shell.

## Environment

Start from an explicit minimal environment.

- common safe names are small and documented;
- OS/tool profiles add required names;
- host key comparison is preserved;
- secret values resolve late and never appear in receipts;
- environment mutation is immutable per plan;
- inherited full environment is not copied by default.

## Lifecycle

One runtime aggregate owns:

- process host;
- launched-process registry;
- kept-alive leases;
- cancellation/shutdown;
- disposal;
- output/receipt completion.

Plugins and tools cannot instantiate a parallel owner.

## Cancellation/kill

Characterize `.Kill(entireProcessTree: true)` first. Required behavior:

1. caller cancellation and timeout are distinguishable;
2. graceful termination is attempted where supported and safe;
3. a bounded force-kill follows;
4. stdout/stderr draining does not deadlock;
5. residual children are detected/reported;
6. PID reuse/races do not authorize another process.

Add native process-group/Job Object behavior only with a recorded failing characterization.
