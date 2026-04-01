# CanDoItAll Runtime Database Switching and SQLite Support Bundle

This initiative bundle prepares an implementation agent to turn the current startup-bound database setup into a first-class runtime-selectable data-source system with reliable PostgreSQL and SQLite support, profile-based switching, schema upgrades, storage isolation, cloning, and real proof-driven validation.

## Profile

- `initiative`

## Mission

- Deliver a production-grade database profile architecture for CanDoItAll so the app can start from the last used database, ask the user whether to continue or switch, switch databases during runtime without a process restart, treat SQLite as a first-class provider, support PostgreSQL and SQLite database creation plus optional clone/snapshot flows, and prove the behavior with unit, integration, component, and Playwright coverage.

## Bundle Layout

- `inputs/` preserves the raw request, source artifacts, and structured interpretation of the request.
- `analysis/` captures the actual repository state, architectural blockers, assumptions, and reopen risks.
- `requirements/` turns the request into explicit acceptance-ready requirements and closure rules.
- `architecture/` defines the target control-plane, runtime-switch, migration, storage, and snapshot architecture.
- `inventories/` lists the impacted tables, services, pages, schema bootstraps, and test harnesses.
- `plan/` defines execution order, dependency gates, and the critical foundation sequence.
- `traceability/` maps every requirement and raw note to concrete files, subbundles, and proof.
- `shared-prompts/` contains execution and QA prompts designed to stop fake completion claims.
- `subbundles/` splits the work into eight dependency-aware execution workstreams.
- `templates/` provides proof, stop-the-line, and test-matrix templates for the execution agent.
- `reviews/` records the bundle self-review and the seeded execution report.

## Recommended Execution Order

1. `subbundles/01-foundation-baseline-and-guardrails`
2. `subbundles/02-control-plane-and-profile-catalog`
3. `subbundles/03-dynamic-runtime-db-and-bootstrap`
4. `subbundles/04-migrations-and-legacy-upgrade-path`
5. `subbundles/05-storage-isolation-and-managed-files-serving`
6. `subbundles/06-runtime-reload-and-workbench-isolation`
7. `subbundles/07-startup-modal-global-switcher-and-settings-ui`
8. `subbundles/08-create-clone-snapshot-and-final-validation`

## Dependency And Validation Map

- The operational dependency map, critical-foundation list, and phase gates live in `plan/01-phase-plan.md`.
- The acceptance and reopen boundaries for each workstream live in the individual subbundle README files.
- The anti-fake validation rules live in `shared-prompts/implementation-prompt.md`, `shared-prompts/qa-prompt.md`, and `templates/02-stop-the-line-checklist.md`.

## Validation Summary

- Bundle preparation status: `Prepared and workspace-validated`
- Bundle readiness gate: `Passed by prepared-stage structure validator in the current workspace`
- Execution status: `Implementation complete; subbundles 01-08 completed with real proof`
- Subbundle gate review: `All subbundles 01-08 passed with recorded gate results and browser analytics`
- Final closure gate: `Passed by completed-stage structure validator in the current workspace`
- Browser validation analytics: `Subbundle 05 closed with direct HTTP managed-file proof; subbundle 06 closed with real browser stale-route and cross-tab reload proof; subbundle 07 closed with reviewed startup-modal, top-bar switcher, desktop settings, and responsive locked-mode UI proof; subbundle 08 closed with reviewed create/clone/snapshot/cross-tab evidence and a full 28-test Playwright pass. Real-node IPFS API proof stayed unavailable in this workspace, so fake-server transport proof carried the documented scope exception`

## Preparation Evidence

- Prepared-stage validator target: `python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py C:\repositories\CanDoItAll\candoitall-db-runtime-switch-bundle --profile initiative --stage prepared`
- Readiness audit completed from senior QA, senior C# architect, and delivery-manager perspectives in `reviews/00-bundle-self-review.md`.
- Execution environment confirmation: the current workspace has the .NET 10 SDK available, and subbundles 01-04 have already been proven with real `dotnet build`, `dotnet ef`, and `dotnet test` commands. PostgreSQL-backed proof for subbundles 03-04 was completed against an ephemeral local PostgreSQL 16 cluster after Docker Desktop was found unavailable on this machine.
