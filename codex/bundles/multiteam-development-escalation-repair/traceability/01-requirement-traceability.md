# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| R1 Diagnose current escalation | `analysis/01-current-state.md` | `subbundles/01-live-run-escalation-diagnosis` | SQL/API/artifact excerpts in execution report | Must cite run ids and step contracts. |
| R2 Enforce role separation | `architecture/01-target-solution.md` | `subbundles/02-process-contract-and-template-repair` | Template projection tests | Architect steps remain read-only. |
| R3 Repair operation contracts | `requirements/01-normalized-requirements.md` | `subbundles/02-process-contract-and-template-repair` | Template JSON diff and unit tests | Focus .NET multiteam templates first. |
| R4 HR/readiness catches missing capabilities | `requirements/01-normalized-requirements.md` | `subbundles/03-hr-readiness-capability-guardrails` | Resolver/runtime integration tests | Must produce actionable missing capability diagnostics. |
| R5 Keep subprocesses small | `architecture/01-target-solution.md` | `subbundles/02-process-contract-and-template-repair` | Template tests and preflight plan | No broad new monolithic implementation step. |
| R6 Reload templates in dev DB | `plan/01-phase-plan.md` | `subbundles/04-real-5032-e2e-proof` | 5032 runtime/template hash output | Requires restart after build. |
| R7 Real 5032 run proof | `plan/01-phase-plan.md` | `subbundles/04-real-5032-e2e-proof` | Live process run transcript | Closure requires no repeat false escalation loop. |
