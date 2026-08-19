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

## Testability contract

- Process plans, executable resolution, environment policy, and termination are testable without a UI or shell.
- Each adapter accepts a deterministic capability profile or injected execution port before it can claim cross-platform support.
- Named unit slices are the inner loop; actual-host Windows/Linux characterization is required when host semantics are touched.
- Full solution suites are reused while production source and dependency anchors are unchanged and rerun only for a final gate candidate or unbounded dependency reach.
- Lifecycle tests distinguish normal exit, timeout, caller cancellation, residual child detection, and foreign-process safety.
- Receipts and diagnostics are scanned for secret values and unrelated process command lines.
- External FileTools, Docker, node/npm, native secret, and terminal profiles remain independently unavailable or degraded; absence cannot silently select an insecure or shell-based fallback.
