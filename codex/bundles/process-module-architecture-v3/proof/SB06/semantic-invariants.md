# SB06 Semantic Invariants

- Builder compiles an immutable plan; runtime is not responsible for rediscovering composition semantics.
- Builder uses driver catalog contracts and strategy descriptors, not concrete driver implementations.
- Every executable step has an explicit strategy binding or the build fails.
- Driver conflicts and missing capabilities are build failures.
- Branch backward routes require loop budgets before runtime can execute them.
- Subprocess plans are compiled recursively with root-owned depth limits and cycle detection.
- Plan hash excludes volatile runtime IDs and timestamps while including semantic definition, driver, strategy, artifact, branch, budget, subprocess, monitoring, and security inputs.
- Builder exposes a persistence handoff port but does not reference EF, PostgreSQL, UI, modules, runtime execution, or Git.
