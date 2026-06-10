# Gap Analysis Toward Generic Process Driver Runtime Host

## Ready Now
- Generic Process Core is cleaner and should stay deterministic and dependency-light.
- Process runtime has been restored enough to run deterministic scenarios and a live process-run OpenAI smoke.
- Verification-only host beta exists with async API, structured denials, options, exact selector, manager facade, audit boundary, and dry-run future gate.
- Domain verification drivers exist as supplied-evidence read-only packages.

## Not Ready Yet
- Runtime-host contracts are not yet rich enough for a future generic host package.
- Dry-run execution host is still module-local and not decomposed into a reusable pipeline.
- Capability catalog is static but not yet a formal capability-provider boundary.
- Scheduler/workflow read-only jobs are not a robust persisted job lifecycle.
- Durable audit needs configuration/index/retention/readback hardening as production behavior, not only a proof claim.
- Manager/operator readback is not yet a fully user-facing runtime-host diagnostic flow.
- Execution-capable driver runtime remains blocked until sandbox, allowlist, authorization, audit, cancellation, timeout, failure handoff, revocation, and emergency-stop are all source-backed.

## Next Implementation Focus
The next bundle must implement the runtime-host foundation in larger slices:

1. contracts,
2. host pipeline,
3. audit/governance,
4. capability registry/catalog,
5. scheduler/workflow job lifecycle,
6. manager/operator readback,
7. sandbox/authorization dry-run evaluator,
8. live/deterministic release matrix.
