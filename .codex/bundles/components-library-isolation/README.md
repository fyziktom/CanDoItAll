# Components Library Isolation

This bundle coordinates the split of stable shared component libraries from the main CanDoItAll repository into `C:/repositories/CanDoItAll.Components`, with the main repository consuming built local NuGet packages.

## Profile

- `initiative`

## Mission

- Isolate the component library build graph, preserve the two main-solution component projects that still depend on app code, split Tailwind ownership, and keep the main solution building from local packages.

## Outcome Contract

- Requested outcome: moved component libraries live and build in the components repo; main repo restores them from `ExternalPackages`; Space3D no longer slows the main slnx.
- Hard constraints: no cross-repo project references to moved components; version `0.1.0`; READMEs/package metadata for all moved packages; `CanDoItAll.Components` and `CanDoItAll.Components.WebGlSandbox` stay in main.
- Evidence required before closure: component solution build/pack, package inventory, project-reference audit, Tailwind build outputs, main solution build/test proof, slnx source assertion, docs source assertion.
- Known blockers or explicit scope exceptions: Browser proof may be recorded as blocked if local runtime startup is not available after build-level proof succeeds.

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

1. `subbundles/01-components-repo-foundation`
2. `subbundles/02-main-repo-nuget-consumption`
3. `subbundles/03-tailwind-and-documentation`
4. `subbundles/04-solution-validation`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed with in-memory startup`
