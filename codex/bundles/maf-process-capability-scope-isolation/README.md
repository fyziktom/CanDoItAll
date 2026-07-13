# MAF Process Capability Scope Isolation

This initiative bundle prepares a staged architecture refactor for the MAF wrapper and process runtime cooperation model. The immediate problem is a domain leak in common MAF workspace image tools, but the underlying issue is broader: process steps need a typed way to add scoped instructions and suppress or require tools, skills, MCP servers, and runtime tool providers without editing the agent's default profile.

## Profile

- `initiative`

## Mission

Keep common MAF workspace tooling domain-neutral, move development-specific analysis behavior into a development-owned package or process-owned instruction channel, and add a typed process-to-MAF capability scope contract that can suppress or require runtime capabilities before they enter agent context.

## Outcome Contract

- Requested outcome: prepare an implementation-ready bundle only. Do not implement production changes in this run.
- Hard constraints: no software-development or UI-design prompt text remains in common workspace tools; process scope must be strongly typed; suppression must remove skills/tools/MCPs from assembled context rather than only warning the model; required capabilities must fail predictably when absent or denied; process contracts must remain runtime-neutral and be translated by the AgentFramework process adapter.
- Evidence required before closure: source-backed domain-leak inventory, typed contract design, dependency-direction proof, process-template and assignment migration plan, MAF access-policy tests, process-to-MAF metadata tests, end-to-end context manifest proof, and architecture gate approval.
- Known blockers or scope exceptions: production implementation, schema migrations, package creation, and test execution are deferred to the execution phase. The bundle names expected edits and validation but does not apply them.

## Bundle Layout

- `inputs/` raw user request and evidence source list.
- `analysis/` current-state findings, requirements decomposition, assumptions, and risks.
- `requirements/` normalized requirements and raw-input coverage.
- `architecture/` C# architecture inventory, boundary map, dependency direction, pattern decisions, testability plan, and target solution.
- `inventories/` source inventory and capability-surface inventory.
- `plan/` subbundle sequence, dependency map, gates, and architecture checkpoints.
- `traceability/` requirement-to-subbundle coverage.
- `shared-prompts/` implementation, QA, and architecture-review prompts for future execution agents.
- `subbundles/` numbered execution-ready workstreams.
- `templates/` proof and policy examples.
- `reviews/` self-review, execution report seed, and C# architecture gate.

## Recommended Execution Order

1. `subbundles/01-sb01-maf-workspace-domain-leak-isolation`
2. `subbundles/02-sb02-maf-scoped-capability-policy-contract`
3. `subbundles/03-sb03-process-step-capability-and-instruction-contract`
4. `subbundles/04-sb04-process-to-maf-runtime-handoff`
5. `subbundles/05-sb05-development-tool-package-migration`
6. `subbundles/06-sb06-end-to-end-proof-and-architecture-closure`

## Dependency And Validation Map

- Keep `plan/01-phase-plan.md` as the source of truth for sequencing and gates.
- Critical foundation work is intentionally MAF-first: remove the common wrapper leak and strengthen capability access before process templates start relying on new scope semantics.
- Every subbundle is architecture-relevant and must include semantic adequacy proof under `proof/SBxx/` during execution.
- Browser proof is only required if execution adds UI-visible diagnostics or changes process UI behavior.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Execution note: implementation completed after the follow-up execution request.
- CodeAnalytics snapshot: `snap-20260707140004-71deb81c`
- Subbundle gate review: `SB01-SB06 completed`
- Final closure gate: `Approved`
- Build validation: targeted isolated builds passed for `CanDoItAll.AgentFramework.Maf`, `CanDoItAll.Modules.Processes`, and `CanDoItAll.Migrations.PostgreSql`.
- Test validation: focused capability/process/MAF tests passed (`37` tests); full unit suite passed (`1838` tests).
- Browser validation analytics: `N/A; no UI-visible authoring or diagnostics were added.`
