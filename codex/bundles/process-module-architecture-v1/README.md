# Process Module Architecture Bundle v1

## Status

Prepared architecture proposal. This bundle is not an implementation bundle for the rewrite. It defines the target architecture, decomposition, rewrite order, validation strategy, and first repository change needed to make future bundle iterations versionable.

## Objective

Prepare the architecture foundation for a ground-up rewrite of the Process module while preserving the useful UI/UX direction. The proposal treats the Process module as an operating-system-like platform with a generic process kernel, runtime scheduler, dispatcher, manager, artifact system, drivers, strategies, templates, monitoring projections, and UI-facing read models.

## Key Decisions

- The generic process core must not contain software-development, Blazor, Office, marketing, or other domain vocabulary.
- Step execution is strategy-based and selected during process instance composition.
- Domain drivers are layered and selected through capability descriptors, not through hardcoded runtime branches.
- Process templates use JSON as source of truth. Markdown and Mermaid are generated projections, not canonical data.
- Template modularization uses component references, local overrides, versioned bases, and three-way conflict detection.
- Git is the versioning substrate for text-based configuration and template data. The module must use a typed Git wrapper, not a homegrown VCS.
- Runtime monitoring is event-first and snapshot-projected. UI live/history views read snapshots and projections without blocking runtime execution.
- Later implementation starts on a new branch by copying the old Process implementation into bundle/reference material, then removing the old module and rebuilding in dependency order.

## Bundle Map

- [inputs/](inputs/) captures the raw request and structured input extraction.
- [analysis/](analysis/) describes the current Process implementation and why it is insufficient.
- [requirements/](requirements/) normalizes every architectural requirement into stable IDs.
- [architecture/](architecture/) contains the target architecture.
- [inventories/](inventories/) lists current reusable and non-reusable repo surfaces.
- [plan/](plan/) defines the phased rewrite plan and dependency gates.
- [subbundles/](subbundles/) prepares implementation-phase work packages for a later rewrite.
- [traceability/](traceability/) maps requirements to architecture files and subbundles.
- [shared-prompts/](shared-prompts/) gives future implementation and QA agents the right posture.
- [reviews/](reviews/) records the preparation self-review and execution report.

## Repository Change Included

The root `.gitignore` now keeps unrelated transient bundles ignored while explicitly allowing `codex/bundles/process-module-architecture*/**`. This makes this architecture bundle and future Process architecture iterations versionable without exposing older unrelated bundle directories. Exported zips remain ignored through `codex/bundle-exports/**` and `codex/**/*.zip`.

## Validation Performed

This bundle was grounded in the current repo through direct inspection of:

- `src/CanDoItAll.Modules.Processes`
- `src/CanDoItAll.Processes.Contracts`
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Processes.Drivers.*`
- `Templates/Processes`
- Process-related unit, component, integration, and Playwright test surfaces

The bundle intentionally does not run product tests because no product behavior was changed.

## Validation Summary

- Bundle preparation status: Prepared architecture bundle.
- Bundle readiness gate: Prepared-stage validator must pass before handoff.
- Execution status: Architecture only; rewrite implementation not started.
- Subbundle gate review: SB01 through SB10 are planned and not executed.
- Final closure gate: Deferred to later implementation bundle execution.
- Browser validation analytics: N/A for architecture-only documentation and `.gitignore` change.
- Prepared-stage validation was run with `validate_bundle.py`.
- Initial validation findings were repaired in this bundle version.
- Product tests were not run because this task changes documentation and `.gitignore` only.
