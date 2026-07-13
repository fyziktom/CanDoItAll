# SB06 Semantic Invariants

## Invariants

- Finalizer/process recovery must preserve existing success, failed, blocked, and branch-outcome semantics.
- Tool invocation guard must still block repeated risky mutation/validation loops.

## Shallow-Pass Trap

- Extracting only string parsing helpers without testing realistic recovered artifacts would miss behavior regressions.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Finalizer recovery result | Recovery service | Execution coordinator | Produced after finalizer/provider failure paths | Missing/invalid artifact tests |
| Session persistence decision | Session persistence service | Execution coordinator | Produced per run/session | Request-scoped attachment tests |
| Tool invocation guard decision | Tool invocation guard | Execution coordinator | Applied per tool call | Repeated mutation/validation tool tests |
