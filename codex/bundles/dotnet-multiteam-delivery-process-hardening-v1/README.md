# .NET multi-team delivery process hardening

This bundle coordinates the .NET-only refresh of the governed multi-team software delivery process and its child subprocesses.

## Profile

- `initiative`

## Mission

Make the default multi-team delivery template usable for .NET delivery runs: classify the requested .NET app shape, keep architecture design separate from review, route implementation through bounded subprocesses, restrict product mutation to implementation/repair lanes, and require project-structure writeback for runtime commands and UI screenshots.

## Outcome Contract

- Requested outcome: a .NET-oriented `software-delivery` process that can drive backend-only, Blazor SSR, Blazor WASM, and Blazor WASM PWA work without letting planning, architecture, review, QA, screenshot, or writeback steps mutate product code.
- Hard constraints: do not run the delivery process; keep changes template-driven; preserve typed process operation contracts; write screenshot assets under a `Screenshots` parent below the process run node; write .NET runtime command nodes under a `Run command` parent below the process run node.
- Evidence required before closure: prepared and completed bundle validation, source assertions for every changed process template, targeted process-template governance tests, and a build or explicit test gap.
- Known blockers or explicit scope exceptions: this bundle tunes .NET delivery only. JavaScript-specific process separation is intentionally out of scope.

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

1. `subbundles/01-process-inventory-and-bundle-readiness`
2. `subbundles/02-dotnet-delivery-subprocess-architecture-and-permissions`
3. `subbundles/03-project-structure-runtime-and-screenshot-writeback`
4. `subbundles/04-validation-and-handoff`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Not required for template-only implementation; app must remain running for user-led process test`
