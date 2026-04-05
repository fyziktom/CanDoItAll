# CanDoItAll Plugin-Wave Architecture Review Bundle V5

This bundle reviews the **post-refactor** codebase with one specific decision in mind: **is the current architecture strong enough to start the next plugin wave (email, LinkedIn, custom APIs, and similar integrations)?**

## Verdict

- **Small feature work / bugfixes:** `GO with normal caution`
- **Major external plugin wave:** `NO-GO until the critical subbundles in this bundle are complete`

The refactor clearly improved several important seams, especially around CRM/HR canonical party ownership and node-scoped assignment flow. However, the codebase is **not yet a stable base for the next integration/plugin wave** because the deepest canonical-model issues were not fully removed:

- persisted Workbench sync still creates a parallel truth
- the universal node record is still too overloaded
- kind semantics are still fragmented and UI-centric
- node lifecycle/reclassification is still too shallow
- plugin architecture is still mostly enum/switch/DI-registration based

## Mission

- Preserve the product direction that **node remains the universal carrier**.
- Keep **X/Y coordinates and semantic markers as canonical project data**, not mere UI cosmetics.
- Remove the remaining architectural weaknesses that would make the next plugin wave expensive, fragile, or semantically unclear.
- Provide Codex with an execution-grade refactor bundle in the same style already used inside the repository.

## Bundle Layout

- `inputs/` current request, source artifacts, and structured execution scope
- `analysis/` current-state findings, risks, plugin-wave readiness, and fixed areas
- `requirements/` normalized requirements
- `architecture/` target direction for carrier/facets, lifecycle, and plugin platform
- `plan/` phase plan and dependency order
- `traceability/` requirement-to-finding and subbundle mapping
- `subbundles/` execution-ready phases for Codex
- `reviews/` bundle self-review, execution notes, and senior QA review
- `spreadsheets/` findings workbook plus rendered previews
- `scripts/` bundle validator

## Recommended Execution Order

1. `subbundles/01-remove-persisted-workbench-sync-as-parallel-truth`
2. `subbundles/02-stabilize-node-carrier-facets-and-bindings`
3. `subbundles/03-centralize-node-kind-registry-and-lifecycle`
4. `subbundles/04-plugin-platform-and-cross-module-seams`
5. `subbundles/05-service-decomposition-guardrail-tests-and-final-review`

## Validation Summary

- Bundle preparation status: `Prepared-stage validation passed`
- Bundle readiness gate: `Passed`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Blocked in this environment because dotnet SDK/runtime is unavailable; runtime proof must be produced by Codex in a real .NET environment`
