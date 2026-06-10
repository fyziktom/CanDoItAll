# Gap Analysis Toward Generic Process Driver Runtime Host

## Ready now

- Process runtime is usable again in deterministic tests.
- Live process-run OpenAI smoke exists and has passed.
- Read-only verification host beta exists.
- Exact lane registry/selector exists.
- Manager read-only facade exists.
- EF audit store exists and is now intended as production default.
- Execution-capable future gate model exists.

## Not ready yet

- The verification host is still process-module-internal and mostly synchronous around the orchestrator implementation, even though the public host method is async.
- Durable audit needs stronger lifecycle proof and schema/index/retention governance.
- Manager/operator readback needs a richer production path and UI/API parity.
- Scheduler/workflow read-only verification job execution needs real service execution, not just job model/readiness.
- The dry-run execution-capable gate is a model, not a runtime contract.
- There is no generic runtime host contract with lifecycle, sandbox, authorization, cancellation, timeout, health, and audit behavior.
- Execution-capable drivers must remain blocked until a separate approval gate passes.

## Next strategic step

Do not jump directly to execution-capable drivers. Build a **code-heavy dry-run runtime host readiness layer**:

1. finish durable audit productionization,
2. make host health/readiness operator-visible,
3. execute scheduler/workflow read-only verification jobs,
4. create dry-run execution-host contracts and deny-by-default sandbox,
5. add concrete negative tests proving no side effects,
6. define next approval gate for execution-capable drivers.
