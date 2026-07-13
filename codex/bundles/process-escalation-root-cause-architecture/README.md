# Process Escalation Root Cause Architecture

This bundle is a coordination and execution package for `process-escalation-root-cause-architecture`.

## Profile

- `initiative`

## Mission

Analyze and stage the next refactoring pass for process escalations in the Multi-team software delivery and release governance process. The implementation must separate generic process runtime responsibilities from process-driver and .NET delivery responsibilities, make blocked runs explain their root cause, and prevent tool, MCP, skill, or instruction-scope problems from being hidden as generic manager escalations.

## Outcome Contract

- Requested outcome: prepare an implementation-ready bundle only. Do not change runtime/process source as part of this bundle preparation.
- Hard constraints: keep generic process runtime and dispatcher domain-neutral; do not add .NET, Blazor, Calculator, Tetris, screenshot, or Playwright-specific logic to generic runtime/projection/dispatcher code; rollback-aware analysis must treat run `b5b2e2df-f952-4fb9-913d-3cb22f9f231e` as contaminated by the reverted patch.
- Evidence required before closure: current-run escalation analysis, C# boundary inventory, dependency direction plan, pattern selection records, unit-testable subbundle contracts, and a validation pass with the bundle validator.
- Known blockers or explicit scope exceptions: exact MAF provider transcript text for the blocked steps was not durably exposed through current process read models; this absence is part of the root cause and is owned by SB01.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input
- `analysis/` current run findings, assumptions, and risks
- `requirements/` normalized requirements
- `architecture/` C# boundary, dependency, pattern, and testability plan
- `inventories/` scoped code and runtime evidence inventory
- `plan/` phase order, gates, and architecture checkpoints
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-runtime-diagnostics-lineage`
2. `subbundles/02-capability-readiness-policy-model`
3. `subbundles/03-driver-owned-recovery-classification`
4. `subbundles/04-dotnet-delivery-driver-isolation`
5. `subbundles/05-template-and-process-hardening`
6. `subbundles/06-e2e-replay-and-regression-suite`

## Dependency And Validation Map

- SB01 and SB02 are critical foundations. No domain driver/template refactor should start until blocked-run diagnostics and step capability readiness are observable and test-covered.
- SB03 depends on SB01 because recovery classification needs typed failure facts, not prompt text.
- SB04 and SB05 depend on SB02 and SB03 because .NET delivery behavior must be isolated behind explicit process-driver contracts and capability policy, not generic runtime heuristics.
- SB06 closes the bundle with replay/regression proof across simple .NET delivery, non-UI management-only steps, and browser-proof steps.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Not started`
- Subbundle gate review: `Ready for execution gate`
- Final closure gate: `Not started`
- Browser validation analytics: `Defined for SB06`
