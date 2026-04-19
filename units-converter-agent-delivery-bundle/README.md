# Units Converter Agent Delivery Bundle

This bundle is a coordination and execution package for `units-converter-agent-delivery-bundle`.

## Profile

- `initiative`

## Mission

- Repair the agent source-of-truth split so AgentFramework is the only editable AI-agent registry, then provision and execute a serious Blazor SSR basic-units-converter project against the requested control-plane database using CanDoItAll-created AI agents, observe the real workflow, harvest every process or architecture weakness exposed by that run, implement the necessary repairs and refactors, and rerun until the end-to-end delivery path is defensible.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-canonical-agentframework-ownership-and-crm-hr-projection`
2. `subbundles/02-openai-agent-capability-and-process-template-hardening`
3. `subbundles/03-units-converter-project-and-process-provisioning`
4. `subbundles/04-live-agent-delivery-run-and-observation`
5. `subbundles/05-execution-driven-architecture-repairs-and-refactor`
6. `subbundles/06-final-rerun-and-closure-audit`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Prepared-stage validator PASS`
- Execution status: `In progress; subbundles 01 and 02 completed with code, test, and browser proof`
- Subbundle gate review: `Subbundles 01 and 02 passed; critical remaining foundation is 03`
- Final closure gate: `Pending post-execution audit`
- Browser validation analytics: `Subbundle 01 recorded on /agents?tab=agents and /crm-hr/agents. Subbundle 02 recorded on /agents?tab=agents with refreshed serious-delivery agents and editable QA detail. Subbundles 03, 04, and 06 still require runtime browser proof.`
