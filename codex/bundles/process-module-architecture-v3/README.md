# Process Module Architecture Bundle v3

## Status

Prepared architecture proposal and future implementation roadmap, third iteration. Execution started on 2026-06-15 after user approval. SB01 through SB19 are complete: legacy reference archive, active removal/skeleton boundaries, generic core contracts, Git/template foundations, driver abstraction contracts, immutable instance builder/compiler contracts, runtime scheduler/dispatcher/event ports, persistence/event/outbox/ledger/projection stores, manager/incidents/recovery/typed branch/subprocess control, monitoring projections/live-history contracts, execution adapter/layered driver foundations, template/runtime-history compatibility reporting, the projection-first Process UI shell, the definition catalog/search/scope/Feed Defaults flow, the projection-backed definition editor with governance/contracts/simulation/lint/save/publish/archive/delete behavior, the typed role editor with role templates, executor/staffing fields, fallback/approval settings, override metadata, and step-role binding foundations, the projection-backed definition canvas with explicit selection, toolbox commands, typed receipts, and deterministic recomposition, the typed step editor with operation contracts, route loop budgets, artifact expectations, role bindings, and subprocess mapping, and the JSON-backed template library with preview tabs, typed selective import commands, target-step artifact validation, and source-hash import metadata are implemented and validated. SB20 is next.

Unlike v2, v3 now includes real future implementation subbundles. They are detailed work packages executed in dependency order after user approval. This update expands the roadmap from SB01-SB14 to SB01-SB28 and adds a current-implementation user-story map.

## Objective

Prepare the architecture foundation and future implementation roadmap for a ground-up rewrite of the Process module while preserving the useful current UI/UX direction. The proposal treats the Process module as an operating-system-like platform with a generic process kernel, runtime scheduler, dispatcher, manager, artifact system, drivers, strategies, templates, monitoring projections, UI-facing read models, adapters, persistence ports, and compatibility gates.

## Key Decisions

- The generic process core must not contain software-development, Blazor, Office, marketing, or other domain vocabulary.
- Step execution is strategy-based and selected during process instance composition.
- Domain drivers are layered and selected through capability descriptors, not through hardcoded runtime branches.
- Process templates use JSON as source of truth. Markdown and Mermaid are generated projections, not canonical data.
- Template modularization uses component references, local overrides, versioned bases, and three-way conflict detection.
- Git is the versioning substrate for text-based configuration and template data. The module must use a typed Git wrapper, not a homegrown VCS.
- Runtime monitoring is event-first and snapshot-projected. UI live/history views read snapshots and projections without blocking runtime execution.
- The old UI/UX direction is an anchor. The old runtime/dispatcher is not a foundation to wrap.
- Future implementation starts on a new branch by copying the old Process implementation into reference material, then actively removing the old module before rebuilding in dependency order.
- Project ordering is corrected: driver abstractions and projection contracts exist before builder/runtime/UI layers that consume them.
- Runtime persistence is port-based; EF/PostgreSQL lives in persistence implementation, not runtime.
- Branch/switch routing is typed. Free-text token routing is explicitly rejected.
- Manager behavior runs through a control loop and cannot become a hidden dispatcher.
- Execution integrations for workflows, agents, agent groups, handoffs, scheduler starts, and project/workbench integrations are adapters/strategies, not core concepts.
- Runtime history compatibility must be proven through migration/archive/read-only projection decisions; old runtime code is not kept alive only for history.
- Current Process user stories US-001 through US-056 are explicit coverage requirements for future implementation and final regression.
- Browser-facing story proof must be captured in the owning UI subbundle with Playwright MCP evidence and screenshots, not deferred to final closure.
- Runtime, dispatcher, manager, projection, template, Git, adapter, and UI implementation must follow explicit .NET performance guardrails: async end-to-end, bounded queues, source-generated JSON, cached serializers, bounded UI queries, and no allocation-heavy hot paths without proof.
- Process launch candidate selection must not be score-only. HR recommendations are advisory; deterministic readiness assessment must expose missing required tools, rights, capabilities, approvals, bindings, and provisioning blockers.
- Final E2E source scenarios must be loaded through typed Process/project-structure APIs and documented in a Codex Process API skill. `TetrisGame` is scenario data only; generic Process layers and broad software/.NET drivers must not contain Tetris-specific or other scenario-specific rules.

## Bundle Map

- [inputs/](inputs/) captures the preserved request, improvement instructions, and structured input extraction.
- [analysis/](analysis/) describes the current Process implementation, why it is insufficient, the current user-story map, .NET performance risk signals, current role-candidate readiness gaps, and final E2E project-structure source scenarios.
- [requirements/](requirements/) normalizes every architectural requirement into stable IDs.
- [architecture/](architecture/) contains the target architecture, state models, builders, drivers, manager, artifacts, monitoring, template/Git model, governance, persistence/event stores, branch contracts, adapters, UI projection inventory, runtime history compatibility, user-story coverage model, .NET performance guardrails, role-candidate readiness model, Process API/Codex skill contract, and final E2E source scenario strategy.
- [inventories/](inventories/) lists current reusable and non-reusable repo surfaces.
- [plan/](plan/) defines the phased rewrite plan, hardening gates, project order, and future subbundle roadmap.
- [subbundles/](subbundles/) contains SB01-SB28 implementation packages. Execution proof is recorded under [proof/](proof/) as each package closes.
- [traceability/](traceability/) maps requirements and source prompt topics to architecture files, future phases, and acceptance criteria.
- [validation/](validation/) records the architecture checklist, test plan, subbundle readiness checklist, user-story coverage validation, .NET performance antipattern checklist, role-candidate readiness validation, and final E2E source scenario validation.
- [shared-prompts/](shared-prompts/) gives future implementation and QA agents the right posture after this architecture is accepted.
- [reviews/](reviews/) records the preparation self-review and execution report.

## Repository Change Included

The root `.gitignore` now keeps unrelated transient bundles ignored while explicitly allowing `codex/bundles/process-module-architecture*/**`. This makes this architecture bundle and future Process architecture iterations versionable without exposing older unrelated bundle directories. Exported zips remain ignored through `codex/bundle-exports/**` and `codex/**/*.zip`.

## Validation Performed

This bundle was grounded in the current repo, v2 bundle, and v3 planning instruction package through direct inspection of:

- `src/CanDoItAll.Modules.Processes`
- `src/CanDoItAll.Processes.Contracts`
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Processes.Drivers.*`
- `Templates/Processes`
- Process-related unit, component, integration, and Playwright test surfaces
- `codex/bundles/process_module_architecture_bundle_improvement_instructions_v1`
- `codex/bundles/process_module_architecture_v3_subbundle_planning_instructions`
- current running Process UI at `http://localhost:5032/processes` and `http://localhost:5032/processes/live`

The original preparation pass intentionally did not run product tests because no product behavior was changed. Execution proof for SB01-SB19 is recorded under [proof/](proof/), including focused unit tests, solution builds, static scans, browser validation, and CodeAnalytics MCP snapshots where required.

## Validation Summary

- Bundle preparation status: Prepared architecture bundle v3 with future subbundle roadmap.
- Bundle readiness gate: Prepared-stage validator passed before execution.
- Execution status: SB01-SB19 completed in dependency order.
- Subbundle gate review: SB01-SB19 closure proof is recorded in `proof/SB01/` through `proof/SB19/`; SB20-SB28 remain pending execution.
- Final closure gate: Future implementation closure depends on the Phase 0 and project rebuild gates in `plan/`.
- Browser validation analytics: Current UI was inspected for story-map evidence; SB13 captured route-level Playwright and Browser MCP proof for the rebuilt Process shell; SB14 captured route, search, selection, Feed Defaults, scope empty-state, and project route proof; SB15 captured route, search, selected definition, identity/governance edits including manager override, save/publish receipt, lint state, desktop/narrow screenshots, console, and network proof; SB16 captured role selection, role field edit/save, template apply, step binding visibility, Playwright screenshots, and Browser state proof; SB17 captured definition canvas load, node selection, toolbox add command, recomposition command, command receipt assertions, and Playwright screenshots; SB18 captured typed step operation save, branch route loop budget save, artifact add, subprocess mapping, screenshots, and browser console/network summary; SB19 captured template library search/category selection, Markdown/diagram/JSON/structure preview tabs, process/role/artifact import receipts, artifact target-step selection, screenshots, and browser console/network summary.
- Prepared-stage validation is recorded in `reviews/01-execution-report.md`.
- v2/v3 architecture gaps were converted into new architecture files, roadmap updates, real future subbundles, story traceability, validation checks, and reviews.
- The performance guardrail review was added using the `analyzing-dotnet-performance` skill against current Process code signals and translated into architecture constraints for future implementation.
- Role candidate readiness was expanded so launch planning can show missing tools/rights and block launch execution until required readiness blockers are resolved or explicitly overridden by policy.
- The `TetrisGame` project structure from the running instance on port `5032` was captured as final E2E source evidence, with three additional app scenarios required to prove the implementation remains generic. A draft JSON scenario-source pack is stored at `evidence/e2e-source-project-structures/final-e2e-scenario-source-packs.json` for future API-loading implementation.
- Prepared-stage product tests were not run because that pass changed documentation and `.gitignore` only; execution proof now records product builds and tests per completed subbundle.
