# Canonicality invariants

1. The canonical runtime profile is immutable for the lifetime of the process.
2. Persisted active profile changes are pending restart, not runtime truth.
3. Managed files, workspace roots, process execution, cognitive memory runtime, automation, and outbox workers must use the canonical runtime profile.
4. Maintenance/profile-specific DB contexts must not mutate runtime truth unless the operation is an explicit maintenance operation.
5. A claimed work item may only be finalized if the worker still owns the matching lease token and the lease is not expired.
6. Parallel execution must not break per-process-run, per-envelope, per-plugin-account, or per-command canonical ordering where ordering matters.
7. Recovery and retry must be idempotent and observable through audit/attempt records.
