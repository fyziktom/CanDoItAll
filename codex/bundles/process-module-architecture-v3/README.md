# Process Module Architecture Bundle v3

## Status

Prepared architecture proposal and future implementation roadmap, third iteration. This bundle is architecture/planning-only. It does not implement the new Process module, execute the rewrite, add runtime code, add migrations, or rewrite product tests.

Unlike v2, v3 now includes real future implementation subbundles. They are detailed work packages prepared for later execution after user approval. They are not executed in v3.

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

## Bundle Map

- [inputs/](inputs/) captures the preserved request, improvement instructions, and structured input extraction.
- [analysis/](analysis/) describes the current Process implementation and why it is insufficient.
- [requirements/](requirements/) normalizes every architectural requirement into stable IDs.
- [architecture/](architecture/) contains the target architecture, state models, builders, drivers, manager, artifacts, monitoring, template/Git model, governance, persistence/event stores, branch contracts, adapters, UI projection inventory, and runtime history compatibility.
- [inventories/](inventories/) lists current reusable and non-reusable repo surfaces.
- [plan/](plan/) defines the phased rewrite plan, hardening gates, project order, and future subbundle roadmap.
- [subbundles/](subbundles/) contains SB01-SB14 future implementation packages. They are prepared, not executed.
- [traceability/](traceability/) maps requirements and source prompt topics to architecture files, future phases, and acceptance criteria.
- [validation/](validation/) records the architecture checklist, test plan, and subbundle readiness checklist.
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

The bundle intentionally does not run product tests because no product behavior was changed.

## Validation Summary

- Bundle preparation status: Prepared architecture bundle v3 with future subbundle roadmap.
- Bundle readiness gate: Prepared-stage validator must pass before handoff.
- Execution status: Architecture/planning only; rewrite implementation intentionally not started.
- Subbundle gate review: SB01-SB14 are prepared for later execution after user approval; none were executed in v3.
- Final closure gate: Future implementation closure depends on the Phase 0 and project rebuild gates in `plan/`.
- Browser validation analytics: Architecture-only skip; no browser-facing product surface changed.
- Prepared-stage validation is recorded in `reviews/01-execution-report.md`.
- v2 architecture gaps were converted into new architecture files, roadmap updates, real future subbundles, traceability, validation checks, and reviews.
- Product tests were not run because this task changes documentation and `.gitignore` only.
