# Shared Implementation Prompt

You are implementing one subbundle from `process-artifact-reliability-hardening` in `fyziktom/CanDoItAll` on branch `development`.

Primary rule: Processes are above workflows. Workflows may execute roles, but Processes own process artifact expectations, finalization, recovery, and transition. Do not move process semantics into the Agents workflow module.

Database rule: the branch is PostgreSQL-only. Do not add SQLite migrations, SQLite snapshots, SQLite tests, or provider-switch branches.

Implementation approach:

1. Read the subbundle README fully.
2. Read the exact source references.
3. Add failing-first tests where the subbundle requires them.
4. Implement the smallest durable change that satisfies the semantic requirement.
5. Prefer explicit result objects and PostgreSQL ledger reloads over hidden mutable in-memory state.
6. Persist diagnostics for required artifact failures; do not hide them in logs only.
7. Revalidate recovered/projected artifacts before completing process steps.
8. Update proof files under `proof/SBxx/`.
9. Run the subbundle closure gate before continuing.

Stop conditions:

- If an executor path can still transition a process step without finalizer validation.
- If an artifact expectation can be satisfied by a placeholder, stale file, invalid format, or unsupported producer.
- If manager recovery can invent evidence or use an unrelated manager.
- If SQLite work becomes necessary; instead record why that contradicts current branch scope.
