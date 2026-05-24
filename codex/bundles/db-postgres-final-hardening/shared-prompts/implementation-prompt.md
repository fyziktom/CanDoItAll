# Implementation prompt

You are a senior C#/.NET architect working in the CanDoItAll repository.

Target branch: `db-remove-sqlite`.

Use the repository-local bundle execution skill:
- `codex/skills/bundles/candoitall-bundle-execution/SKILL.md`

Execute `candoitall-db-postgres-final-hardening-followup-bundle-v4` in subbundle order.

Primary goal:
Finish PostgreSQL-only canonical runtime hardening by closing leased-work stale finalization risks, heartbeat/lease-loss semantics, throughput defaults, benchmark proof, and final merge readiness.

Hard constraints:
- Do not reintroduce SQLite runtime provider, migrations, UI, or tests.
- Do not make pending profile activation look like active runtime state.
- Do not allow stale workers to commit final canonical state after lease loss.
- Do not rely on focused tests only for final merge readiness without documenting broad-suite caveats.
- All code comments must be English.

Critical acceptance:
- Process outbox and connector command finalization must be conditional on still owning the lease.
- Lost lease must prevent completion/retry/dead-letter finalization by stale worker.
- Parallel processing must preserve partitioned canonicality.
- Benchmark report must include real numbers or clearly documented environmental limitation.
