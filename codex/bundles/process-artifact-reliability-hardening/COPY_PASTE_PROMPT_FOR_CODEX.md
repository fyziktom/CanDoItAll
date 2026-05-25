# Copy-paste prompt for Codex

You are working in `fyziktom/CanDoItAll` on branch `development`.

Use the CanDoItAll bundle workflow skill and execute this prepared bundle:

`codex/bundles/process-artifact-reliability-hardening`

The user problem is that CanDoItAll Processes often get stuck because required process artifacts are missing or in the wrong format. The step may retry the same executor several times while the underlying missing/invalid artifact condition does not change. The goal is to harden `CanDoItAll.Modules.Processes` so artifact contracts are validated, diagnosed, recovered, or blocked deterministically.

Important boundary rule:

- Do not confuse Processes and Workflows.
- Workflows belong to the Agents/AgentFramework side.
- Processes can assign a workflow-backed role/executor, but Processes are above that and own process step finalization, process artifact expectations, process transition decisions, process recovery, and process audit records.
- Do not move process artifact semantics into the workflow module.

Database rule:

- The development branch is now PostgreSQL-only after SQLite removal.
- Do not add SQLite migrations, SQLite tests, SQLite provider-switching code, or provider compatibility branches.

Execution rule:

1. Read `README.md`, `analysis/`, `requirements/`, `architecture/`, and `plan/01-phase-plan.md`.
2. Execute subbundles in order.
3. Before each subbundle, run its entry gate.
4. After each subbundle, write proof under `proof/SBxx/`, update `reviews/01-execution-report.md`, and run the closure gate.
5. Do not proceed past a critical foundation if its semantic adequacy gate fails.
6. If implementation observations invalidate this bundle, repair the bundle and rerun the prepared-stage validator before continuing.

Minimum final proof:

- focused integration tests proving direct agent, workflow-backed role, stranded step, missing artifact, invalid format, and manager recovery behavior
- source assertions showing all executor paths enter the process-owned finalizer
- artifact proof manifests with changed-file hashes
- PostgreSQL-only migration/model validation if data model changes are made
- final solution build or explicit blocker
