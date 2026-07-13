# Process Runtime Dispatch Flexibility Hardening

This initiative bundle prepares the refactor needed to make process runtime dispatch flexible across enterprise task domains while preserving the behavior introduced in branch commit `6775de820 phase1`.

## Profile

- `initiative`

## Mission

Refactor process runtime and dispatcher integration so generic runtime concepts stay domain-neutral, runtime step dispatch is delegated through driver ports, AgentFramework/MAF execution behavior lives below the Processes dependency boundary, prompt and completion-evidence policies are driver-owned strategies, and project-structure/.NET/software-delivery specifics live in domain contributors instead of the shared runtime path.

## Outcome Contract

- Requested outcome: implement architecture refactoring workstreams for maintainable runtime/dispatcher flexibility while preserving existing behavior.
- Hard constraints: preserve all behavior from the last commit; do not remove process features just because they are awkward; keep UI/Application/Domain/Infrastructure boundaries explicit; keep non-software enterprise process scenarios first-class; avoid stringly-typed new seams where typed contracts can carry the intent; no `src/Processes/*` project may reference MAF, AgentFramework, or module wrapper assemblies.
- Evidence required before closure: prepared-stage bundle validator passes; every raw request item maps to requirements and subbundles; critical subbundles require semantic adequacy proof, artifact-backed manifests, failing-first and passing tests, changed-file hashes, and anti-stub audits during execution.
- Known blockers or explicit scope exceptions: exact project placement for the AgentFramework/MAF process driver must be validated during SB01 against project reference direction; the preferred shape is an MAF-owned driver implementation that references Processes driver abstractions, not a Processes project that references MAF. If composition constraints block that, execution must stop and repair the bundle rather than introducing a reverse dependency.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `inventories/` source inventory and hotspot mapping
- `templates/` execution templates and subbundle README template
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-runtime-driver-boundary-and-inventory`
2. `subbundles/02-agentframework-adapter-decomposition`
3. `subbundles/03-prompt-and-brief-strategy-extraction`
4. `subbundles/04-completion-evidence-policy-extraction`
5. `subbundles/05-domain-launch-context-isolation`
6. `subbundles/06-dispatcher-branch-and-recovery-cleanup`
7. `subbundles/07-regression-proof-and-architecture-hardening`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.
- Critical subbundles must record `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md` before downstream phases rely on their changes.
- Dispatcher and runtime behavior must be proved with agent execution, process launch, driver-dispatched step execution, subprocess, branch, project-structure, and generic non-software process scenarios.
- Dependency direction must be proved by source/project-reference scans: Processes abstractions/application/runtime can define ports and consume abstractions, while MAF/AgentFramework implements those ports below the Processes boundary.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed - validate_bundle.py --stage prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed - SB01-SB07 implementation and proof captured`
- Final closure gate: `Passed - backend-only refactor, no browser proof required`
- Browser validation analytics: `Seeded; required only for dashboard or process UI regressions in SB07`

