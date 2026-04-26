# Process Agent Flexibility

This bundle is a coordination and execution package for `process-agent-flexibility-2026-04-26`.

## Profile

- `initiative`

## Mission

Make process-run automation flexible enough for coding and non-coding work by moving technology-specific guidance out of the base execution prompt and into specialized default agents, skills, and process templates. The result must support .NET, JavaScript, business strategy, financial strategy, marketing, spreadsheet, mail, and analysis workflows without the platform prompt assuming a Blazor calculator app.

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
- `inventories/` affected prompt, seed, process-template, and test surfaces
- `templates/` reusable process and prompt-shape notes

## Recommended Execution Order

1. `subbundles/01-base-process-prompt-flexibility`
2. `subbundles/02-specialized-default-agent-catalog`
3. `subbundles/03-scenario-process-templates-and-validation-harness`
4. `subbundles/04-postgresql-process-validation-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- Subbundle 01 is a critical foundation; later process-agent behavior is not trustworthy if the base prompt still contains .NET calculator-specific instructions.

## Validation Summary

- Bundle preparation status: `Prepared - validator passed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed - validator passed`
- Browser validation analytics: `N/A for code-only prompt/catalog work unless real process UI validation becomes necessary`

## Closure Proof

- Base process prompt is domain-neutral and no longer carries global .NET/Blazor/calculator implementation instructions.
- Specialized .NET, JavaScript, business, finance, and marketing agents are seeded with role-specific instructions and tests.
- Business-plan process template is registered, projects to the current process model, and routes approval outcomes to explicit end steps.
- PostgreSQL-backed deterministic process execution passed.
- PostgreSQL-backed live specialist-agent handoff validation passed with `CANDOITALL_RUN_LIVE_AGENT_VALIDATION=true`.
