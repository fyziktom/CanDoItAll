# process-dispatch-projection-model-rule-decoupling-boundary-v1

Status: Prepared for Codex implementation.

## Mission

Continue the `maf-processes-refactor` line by extracting a **module-local projection model and rule boundary** for artifact projection.

The previous bundle split the broad projection host into focused facets, but the facet implementations still depend heavily on nested `ProcessRunAutomationDispatchService.*` model aliases and static helper forwarding. This bundle must replace those transitional dependencies with top-level module-local projection read models, mutable projection state, and rule services, while preserving all existing runtime behavior.

## Non-goals

- Do not create `CanDoItAll.Processes.Core`.
- Do not add production process driver APIs such as `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, or driver packages.
- Do not change process runtime behavior, projection order, artifact identity, storage paths, external reference keys, lineage, trust/sensitivity handling, retry behavior, or validation semantics.
- Do not touch UI/Razor/CSS/JS/TS files.
- Do not create small/medium/mobile/phone/tablet proof artifacts. Browser validation is N/A unless unexpected UI files change; if that happens, stop and reopen scope.

## Key architectural target

After this bundle, artifact projection coordinators and facet implementations should depend on projection-specific module-local models such as:

- `ProcessProjectionCandidateSnapshot`
- `ProcessProjectionRunSnapshot`
- `ProcessProjectionStepSnapshot`
- `ProcessProjectionArtifactExpectation`
- `ProcessProjectionMutableCandidateState`
- `ProcessProjectionLineageInput`
- `ProcessProjectionSessionFileContent`
- `ProcessProjectionProcessMockArtifact`

The only place that should know how to translate from dispatcher nested models is the dispatcher-facing adapter/factory boundary.

## Required proof

- Full solution build.
- Focused unit projection architecture tests.
- Focused integration artifact projection tests.
- Source scans proving no Process Core and no production driver API.
- Source scans proving projection coordinators do not use `ProcessRunAutomationDispatchService.DispatchCandidate` aliases directly after the relevant migration gates.
- Source-family order proof.
- Anti-stub scan.
- Completed-stage bundle validator.

## Bundle contents

- `requirements/` normalized requirements and acceptance gates.
- `analysis/` current-state and risk analysis.
- `architecture/` cutline and future driver-readiness map.
- `inventories/` source hotspot, alias, and static-helper maps.
- `plan/` 96-subbundle phase plan with critical gates.
- `subbundles/` executable subbundle READMEs.
- `evidence/checklists/` XLSX checklist workbook.
