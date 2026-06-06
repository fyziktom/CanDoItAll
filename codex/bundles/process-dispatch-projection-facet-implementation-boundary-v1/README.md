# process-dispatch-projection-facet-implementation-boundary-v1

Status: Prepared for Codex implementation.  
Created: 2026-06-06.  
Profile: initiative.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed - bundle://proof/shared/transcripts/prepared-validator.txt`
- Execution status: `Completed`
- Subbundle gate review: `Passed - reviews/01-execution-report.md`
- Final closure gate: `Passed - bundle://proof/shared/transcripts/completed-validator.txt`
- Browser validation analytics: `N/A - runtime/service refactor only; no UI files changed; source scan passed`
## Mission

Continue the safe `maf-processes-refactor` dispatcher decomposition after `process-dispatch-projection-host-facet-boundary-v1`.

The previous bundle successfully removed the broad `IProcessArtifactProjectionHost` and introduced module-local projection facets. The next unsafe coupling is the single nested dispatcher-backed implementation, `ProcessRunAutomationDispatchService.ProcessArtifactProjectionServices`, which implements every projection facet and forwards most calls back into `ProcessRunAutomationDispatchService`.

This bundle must split that implementation into smaller module-local facet implementations and reduce nested dispatch-type leakage, while preserving every existing projection behavior.

## Non-goals

- Do not create `CanDoItAll.Processes.Core`.
- Do not introduce production process-driver APIs, driver registries, driver packages, or `IProcessDriverPack`.
- Do not move EF entities, DbContext access, public process contracts, or storage writes into a new project.
- Do not touch UI, Razor, CSS, JavaScript, TypeScript, screenshots, Playwright viewport proof, mobile, small-screen, or medium-screen proof paths.
- Do not change projection source-family order.
- Do not remove existing behavior.

## Required outcome

By the end of this bundle:

1. `ProcessArtifactProjectionServices` is deleted or reduced to a tiny factory/shim.
2. There is no single class implementing all projection facets.
3. Each projection facet has a focused module-local implementation or an explicit small adapter.
4. Projection coordinators consume only the facets they need.
5. The projection source-family order remains:
   execution artifacts -> process mock artifacts -> workspace-written artifacts -> existing managed artifacts -> response text artifacts -> provider-native browser artifacts -> completed decision artifacts.
6. Existing focused projection tests, integration projection tests, build, no-core/no-driver scans, no-UI scans, anti-stub scans, and completed-stage validator pass.
7. Driver readiness remains documentation-only.

## Bundle layout

- `inputs/` raw request and branch review.
- `analysis/` current state, assumptions, risk and reopen triggers.
- `requirements/` normalized requirements and constraints.
- `architecture/` target design and no-core cutline.
- `inventories/` source/facet/model/test inventories.
- `plan/` subbundle dependency map and gates.
- `subbundles/` SB01-SB84 implementation instructions.
- `traceability/` coverage matrix.
- `shared-prompts/` implementation and QA prompts.
- `evidence/checklists/` XLSX checklist.
- `reviews/` seeded execution report and self-review prompts.

## Validation

Codex must run the prepared-stage validator before implementation and must not continue past critical gates without passing proof artifacts.

