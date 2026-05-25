You are Codex working in `fyziktom/CanDoItAll` on branch `processes-hardening`.

Execute the bundle at:

`codex/bundles/processes-hardening-followup-runtime-integrity-v4`

Important context:

- The previous phase3 implementation improved process boundaries, tool policy, finalizer routing, materialization, and linter behavior.
- Do not confuse `Processes` with `Workflows`. Workflows are executor/runtime artifacts inside AgentFramework. Processes own process runs, step contracts, roles, artifacts, finalization, recovery, and governance.
- The process runtime must remain generic. Do not hard-code only Blazor/.NET/software-development behavior.
- PostgreSQL is now canonical. Do not add SQLite migrations, SQLite provider paths, or SQLite validation requirements.

Execution rules:

1. Read `README.md`, `analysis/02-verified-findings.md`, `architecture/01-target-runtime-integrity.md`, and `plan/01-phase-plan.md`.
2. Execute subbundles in order. Do not skip SB01 because downstream unblock reliability depends on it.
3. For every critical subbundle, capture proof under `proof/SBxx/` before moving on.
4. Use failing-first/red-team tests where possible. Avoid source-assertion-only proof.
5. Run focused tests after each subbundle and full build/unit/integration confirmation before final closure.
6. If implementation reality changes the plan, update bundle files and rerun prepared-stage validation before continuing.

Final closure requires:

- focused process runtime tests passing
- focused tool-policy tests passing
- process linter tests passing
- solution build passing
- PostgreSQL-only audit passing
- bundle validator passing
- execution report updated with raw note closure and residual risks
