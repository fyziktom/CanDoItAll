# Project Structure Page Modular Refactoring

This initiative bundle coordinates a behavior-preserving responsibility extraction from the `ProjectStructurePage` partial-class cluster.

## Mission

Reduce real responsibility concentration rather than merely moving methods between partial files. The first bounded change will consolidate duplicated process-launch context policy behind one top-level, directly tested builder and move project-hierarchy candidate rules behind one top-level policy, leaving the Blazor page as an orchestrating caller.

## Outcome Contract

- Requested outcome: improve at least part of the architecture around Project Structure and add meaningful unit coverage.
- Hard constraints: preserve every existing capability; add no page partial or nested architecture boundary; introduce no silent fallback, service locator, project reference, or source-of-truth duplication.
- Evidence required before closure: direct positive, negative, boundary, and architecture tests; affected Workbench build; targeted Unit/Component tests; existing process-launch integration proof when the environment permits; source assertions proving both production callers use the extracted owner; final architecture and bundle gates.
- Explicit scope boundary: this bundle does not attempt a big-bang decomposition of all 23 page source parts, alter rendered composition, or redesign responsive behavior.
- Tooling gap: CodeAnalytics MCP was not exposed after two targeted discovery attempts. Exact source, project files, compiler results, tests, and source assertions are the recorded fallback evidence.

## Compatibility Decision

- The completed `.codex/bundles/project-structure-assignment-pricing-architecture` bundle is preserved in place. It explicitly excluded whole-page decomposition and therefore is not reused for this request.
- This new canonical bundle lives under the repository-majority `codex/bundles` convention.

## Execution Order

1. `subbundles/01-01-baseline-inventory-and-characterization`
2. `subbundles/02-02-process-context-summary-boundary`
3. `subbundles/03-03-project-hierarchy-selection-policy`
4. `subbundles/04-04-regression-and-architecture-closure`

## Validation Summary

- Bundle preparation status: `Prepared — canonical validator passed 2026-07-23`
- Execution status: `Completed — SB01-SB04`
- Subbundle gate review: `Pass — every entry, closure, and progression gate passed`
- Final closure gate: `Pass — behavioral, C# architecture, and canonical completed validator passed 2026-07-23`
- Browser validation analytics: `N/A — no markup, styling, layout, or interaction contract changes are planned`

The durable command and gate record is `reviews/01-execution-report.md`.
