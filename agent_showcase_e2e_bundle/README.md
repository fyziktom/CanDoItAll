# Agent Showcase E2E Bundle

This bundle is a coordination and execution package for `agent_showcase_e2e_bundle`.

## Profile

- `initiative`

## Mission

- Correct the first-wave agent integration regressions, provision a template-driven showcase for a Blazor SSR calculator application against the requested control-plane database, execute the resulting project/process/agent workflow end to end, and keep the bundle open until the agent-driven delivery flow, artifact handoffs, QA checks, and project-structure progress updates all complete with defensible proof.

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

1. `subbundles/01-cross-module-agent-source-alignment`
2. `subbundles/02-processes-workspace-and-database-profile-ux-fixes`
3. `subbundles/03-template-driven-showcase-provisioning-and-agent-capability-wiring`
4. `subbundles/04-live-showcase-execution-bug-harvest-and-closure`
5. Re-run validators and close only after the live showcase passes.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared and validated`
- Execution status: `Completed`
- Subbundle gate review: `01` and `02` closed with code, targeted tests, and live browser proof. `03` and `04` closed with template-driven showcase provisioning, live process execution, imported QA and rollout browser evidence, and successful end-to-end completion of process run aff6699b-5c0f-441b-b484-4fadfad41ab1.`
- Final closure gate: `Completed-stage validator PASS`
- Browser validation analytics: `01`, `02`, and `04` complete. `03` closed as non-UI provisioning work with runtime proof captured through the successful showcase run.`
