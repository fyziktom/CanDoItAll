# Process Module Architecture Bundle v2

## Status

Prepared architecture proposal, second iteration. This bundle is architecture-only. It does not implement the new Process module, execute implementation work packages, add runtime code, add migrations, or rewrite tests.

Implementation work packages are intentionally deferred. The `subbundles/` directory contains only a validator-compatible deferred marker so future Codex runs do not mistake this architecture pass for an implementation bundle.

## Objective

Prepare the architecture foundation for a ground-up rewrite of the Process module while preserving the useful current UI/UX direction. The proposal treats the Process module as an operating-system-like platform with a generic process kernel, runtime scheduler, dispatcher, manager, artifact system, drivers, strategies, templates, monitoring projections, and UI-facing read models.

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

## Bundle Map

- [inputs/](inputs/) captures the preserved request, improvement instructions, and structured input extraction.
- [analysis/](analysis/) describes the current Process implementation and why it is insufficient.
- [requirements/](requirements/) normalizes every architectural requirement into stable IDs.
- [architecture/](architecture/) contains the target architecture, state models, builders, drivers, manager, artifacts, monitoring, template/Git model, and governance.
- [inventories/](inventories/) lists current reusable and non-reusable repo surfaces.
- [plan/](plan/) defines the phased rewrite plan and dependency gates.
- [subbundles/](subbundles/) contains only a deferred implementation marker for validator compatibility.
- [traceability/](traceability/) maps requirements and source prompt topics to architecture files, future phases, and acceptance criteria.
- [validation/](validation/) records the architecture checklist and test plan.
- [shared-prompts/](shared-prompts/) gives future implementation and QA agents the right posture after this architecture is accepted.
- [reviews/](reviews/) records the preparation self-review and execution report.

## Repository Change Included

The root `.gitignore` now keeps unrelated transient bundles ignored while explicitly allowing `codex/bundles/process-module-architecture*/**`. This makes this architecture bundle and future Process architecture iterations versionable without exposing older unrelated bundle directories. Exported zips remain ignored through `codex/bundle-exports/**` and `codex/**/*.zip`.

## Validation Performed

This bundle was grounded in the current repo and improvement instruction package through direct inspection of:

- `src/CanDoItAll.Modules.Processes`
- `src/CanDoItAll.Processes.Contracts`
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Processes.Drivers.*`
- `Templates/Processes`
- Process-related unit, component, integration, and Playwright test surfaces
- `codex/bundles/process_module_architecture_bundle_improvement_instructions_v1`

The bundle intentionally does not run product tests because no product behavior was changed.

## Validation Summary

- Bundle preparation status: Prepared architecture bundle v2.
- Bundle readiness gate: Prepared-stage validator must pass before handoff.
- Execution status: Architecture only; rewrite implementation intentionally not started.
- Subbundle gate review: Implementation packages are deferred and not claimed ready.
- Final closure gate: Future implementation closure depends on the Phase 0 and project rebuild gates in `plan/`.
- Browser validation analytics: Architecture-only skip; no browser-facing product surface changed.
- Prepared-stage validation is recorded in `reviews/01-execution-report.md`.
- v1 audit findings were converted into explicit architecture files, traceability, validation checks, and a red-team review.
- Product tests were not run because this task changes documentation and `.gitignore` only.
