# Copy-Paste Prompt For Codex

You are working in `fyziktom/CanDoItAll` on branch `processes-hardening`.

A follow-up bundle has been prepared at:

`codex/bundles/processes-hardening-followup-scope-resilience`

Execute it with the CanDoItAll bundle workflow discipline.

Important context:

- `Processes` are above `Workflows`. Workflows may execute a process role, but process-owned artifact contracts, step boundaries, branch dispositions, and finalizer validation remain in `CanDoItAll.Modules.Processes`.
- The current branch introduced `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`, but follow-up hardening is still required.
- A real red-team failure occurred: in a Blazor app process, the architecture step also started implementation, even though implementation was supposed to happen in the next step with another agent.
- The fix must stay generic. Do not hardcode Blazor/.NET behavior into the process core.
- Do not reintroduce SQLite.

Start with:

1. Read `README.md`.
2. Read `analysis/03-verified-findings.md`.
3. Read `plan/01-phase-plan.md`.
4. Execute `subbundles/01-step-execution-boundary-and-tool-policy`.
5. Do not start a dependent subbundle until the previous critical gate is proven with artifact-backed proof.

The most important goals:

- enforce step operation boundaries through tool policy, not just prompts;
- load process artifact contracts for workflow-backed roles;
- run subprocess parent completion through the finalizer;
- avoid satisfying required artifacts with placeholders;
- route negative findings to repair/no-go branches when available;
- unblock downstream steps after upstream artifact materialization;
- tune artifact validation to avoid false generic-process blocks;
- compress repeated no-progress retries;
- add process definition lint and red-team tests.
