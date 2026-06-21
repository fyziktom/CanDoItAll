# v3 Architecture Review

## Result

Pass for architecture and future roadmap preparation.

## Improvements Over v2

- Corrected project dependency order and added `Processes.Projections`.
- Made runtime persistence/event-store/outbox decisions concrete.
- Replaced high-level branch guidance with typed branch/switch/loop contract.
- Added operational manager control loop and anti-dispatcher rules.
- Added UI/UX surface inventory and projection contract plan.
- Added execution adapter boundaries for workflows, agents, agent groups, handoffs, scheduler starts, project/workbench integration, and plugins.
- Added runtime history migration/read-only compatibility plan.
- Replaced the deferred subbundle marker with SB01-SB28 future packages after adding user-story coverage.
- Added current implementation user-story map, architecture coverage model, user-story traceability, and user-story validation.
- Added .NET performance antipattern review, performance guardrail architecture, and implementation validation checklist.
- Added role candidate readiness architecture so launch candidate scoring, missing tools, missing rights, approvals, provisioning, and execution blockers are first-class and testable.
- Added Process API/Codex skill architecture and final E2E source scenarios grounded in the live `TetrisGame` project structure plus three additional app scenarios.

## Architectural Risks Remaining

- The future implementation is large and must not combine too many subbundles into one unreviewable change.
- Persistence and projection implementation will be operationally complex.
- Template migration and runtime history compatibility still depend on real data inventory.
- The UI rebuild can regress workflows if projection contracts are incomplete.
- User-story coverage can regress into checklist-only reporting if future agents do not attach source/test/browser proof.
- Future agents can reintroduce slow Process behavior under a cleaner architecture by using sync-over-async, unbounded queues, LINQ-heavy projectors, uncached JSON options, per-call clients, or load-all UI queries.
- Future launch work can regress into score-only candidate selection if readiness findings are not modeled and shown separately from HR ranking.
- Final E2E validation can become scenario-shaped if `TetrisGame` or other app-specific terms leak into generic Process code.

## Required Future Discipline

- Execute SB01 and SB02 first.
- Do not skip hardening gates.
- Record execution reports per subbundle.
- Record story coverage per owned US-### row.
- Record .NET performance scan counts for C# hot-path code.
- Treat missing candidate tools/rights as deterministic launch blockers unless an explicit audited policy override allows otherwise.
- Load final E2E scenarios through public APIs and run scenario vocabulary leak scans before closure.
- Stop rather than patch around boundary violations.
