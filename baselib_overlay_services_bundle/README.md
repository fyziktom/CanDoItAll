# BaseLib Overlay Services

This bundle coordinates the implementation of first-class BaseLib overlay services for dialogs, tooltips, and notifications.

## Profile

- `initiative`

## Mission

- Add Radzen-inspired but CanDoItAll-native `DialogService`, `TooltipService`, and improved `NotificationService` APIs in BaseLib, with Tailwind-only rendered chrome, host components, sandbox examples, docs, component tests, and Playwright MCP validation for real interactive behavior.

## Bundle Layout

- `inputs/` raw request, Radzen reference inventory, and structured input.
- `analysis/` current state, assumptions, risks, and reopen triggers.
- `requirements/` normalized, testable requirements.
- `architecture/` target solution and service/host boundaries.
- `plan/` execution order, dependency map, critical foundations, and phase gates.
- `traceability/` raw-note and requirement ownership.
- `shared-prompts/` implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `reviews/` self-review, execution report, gate results, browser analytics, and raw-note closure.
- `inventories/` source, test, sandbox, docs, and Radzen reference inventory.
- `templates/` reusable subbundle template.

## Recommended Execution Order

1. `subbundles/01-01-service-contracts-and-hosts`
2. `subbundles/02-02-dialog-service-behavior`
3. `subbundles/03-03-tooltip-notification-services`
4. `subbundles/04-04-sandbox-docs-and-browser-proof`

## Dependency And Validation Map

- Service contracts and centrally mounted hosts are the critical foundation.
- Dialog behavior is the riskiest user-facing phase because it must prove size variants, closure paths, and returned objects.
- Tooltip and notification services depend on the shared host pattern but can validate in parallel with sandbox usage after the foundation is complete.
- Final closure requires build/test proof plus Playwright MCP screenshots and open-overlay state assertions.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Prepared validator passed`
- Execution status: `Implemented and validated`
- Subbundle gate review: `Passed`
- Final closure gate: `Completed validator passed`
- Browser validation analytics: `Passed`
