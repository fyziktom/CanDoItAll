# Next Phase Readiness

This file is intentionally prepared as a closure target for SB09.

After this bundle completes, the next recommended bundle is:

```text
Process contracts/core extraction foundation
```

Do not start that work inside this bundle.

Recommended next-phase order:

1. Extract `CanDoItAll.Processes.Contracts` for entity-free process request/result DTOs used by tools, APIs, scheduler, and integration layers.
2. Extract small pure process-core policies: transition guard, run status resolver, definition linter, artifact status projection where dependency-safe.
3. Introduce process agent execution gateway to reduce direct Processes -> AgentFramework implementation dependency.
4. Only after that introduce `IProcessDriverPack` and domain driver packs.
