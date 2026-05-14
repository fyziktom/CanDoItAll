# Plugin Runtime Package Install

This bundle coordinates the plugin implementation split, runtime package installation path, plugin package UI, and restart proof for the CanDoItAll plugin runtime.

## Profile

- `initiative`

## Mission

Move concrete plugin implementations out of `CanDoItAll.Modules.Plugins` into first-class projects under `src/plugins`, then add a runtime package path that lets users install plugin zip packages from a configured catalogue or by upload without compiling the application again. Packages that add assemblies or executable registrations may require a clean app restart, so the UI must offer an explicit restart request instead of leaving users to kill the process in Task Manager.

## Outcome Contract

- Requested outcome: plugin implementations live under `src/plugins` and remain part of `CanDoItAll.slnx`; the plugin module owns runtime/catalog/governance concerns; users can add plugin packages from a catalogue or uploaded zip; restart-required package installs are surfaced with a restart action.
- Hard constraints: keep grant-based plugin governance intact; do not weaken installation versus enablement versus grant separation; do not add silent fallback loading; do not require recompilation for user-installed package zips; keep zip extraction path-safe; keep Blazor UI inside existing shared component patterns.
- Evidence required before closure: prepared-stage bundle validation, build, targeted plugin/catalog/package tests, component UI tests, API proof for package/restart endpoints, and browser proof for `/plugins` package install and restart-required state.
- Known blockers or explicit scope exceptions: this bundle supports loading runtime package assemblies at startup; already-running app service registrations cannot be mutated in place, so packages containing assemblies are installed immediately but require restart before their executor/service types participate in DI.

## Bundle Layout

- `inputs/` raw request, source artifact inventory, and structured input
- `analysis/` current state, assumptions, risks, and reopen triggers
- `requirements/` normalized requirements and coverage
- `architecture/` target solution and plugin package boundaries
- `inventories/` scoped code inventory
- `plan/` dependency order, critical foundations, and phase gates
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-plugin-implementation-project-split`
2. `subbundles/02-02-runtime-package-catalog-and-loader`
3. `subbundles/03-03-plugins-ui-package-install-and-restart`
4. `subbundles/04-04-validation-and-closure`

## Dependency And Validation Map

- The project split is a critical foundation because the runtime package work depends on the plugin module no longer owning concrete Docker/Gmail/Office365 types.
- The package loader is a critical foundation because the UI and restart flow must call real package install services, not local-only UI state.
- UI proof is required for `/plugins` because the user specifically asked for a usable add-package and restart path.
- Keep gate results and evidence paths current in `reviews/01-execution-report.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `SB01-SB04 passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed for /plugins package panel at desktop and mobile widths`
