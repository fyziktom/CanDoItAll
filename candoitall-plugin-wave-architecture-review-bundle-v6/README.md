# CanDoItAll Plugin-Wave Architecture Review Bundle V6

This bundle reviewed the **post-phase-5 refactor** codebase with one decision in mind:

> **Is the current architecture finally strong enough to continue into the large external plugin wave (email, LinkedIn, custom APIs, and similar connectors)?**

## Verdict

- **Contained feature work / bugfixes:** `GO`
- **Major external plugin wave:** `GO with guarded rollout`

Phase 5 already landed the largest structural moves:

- persisted Workbench sync was replaced by projection assembly contributors
- foreign bindings moved out of the carrier and metadata leak checks were added
- node lifecycle history exists for reclassification
- provider/resource extensibility now goes through connector manifests and registries
- cross-module mutation tracking and compensation records exist

Phase 6 closed the remaining plugin-wave blockers:

- editable hierarchy now assembles from canonical `ParentNodeKey` instead of persisting duplicate generic hierarchy links
- node-scoped assignment semantics now flow through one explicit registry-driven policy path
- projection-only nodes are rejected through the shared assignment boundary
- kind descriptors now govern the role-to-node capability matrix used by assignment flows
- final plugin-wave readiness was rerun with real .NET build, test, and browser proof

## Mission

- Preserve the product direction that **node remains the universal carrier**.
- Keep **X/Y coordinates and semantic markers canonical**, not cosmetic.
- Remove the remaining canonical-model weaknesses that would make email / LinkedIn / custom API plugins expensive, fragile, or semantically wrong.
- Give Codex an **execution-grade phase-6 refactor bundle** with a clean order of operations.

## Bundle Layout

- `inputs/` request, artifacts, and normalized execution scope
- `analysis/` findings, risks, readiness verdict, and phase-5 strengths
- `requirements/` normalized architectural requirements
- `architecture/` target direction for carrier/facets, lifecycle, assignment semantics, and plugin platform
- `plan/` ordered refactor plan
- `traceability/` requirement → finding → subbundle mapping
- `subbundles/` execution-ready tasks for Codex
- `reviews/` bundle self-review, execution notes, and senior QA inspection
- `spreadsheets/` findings workbook plus rendered previews
- `inventories/` evidence and hotspot inventory
- `templates/` placeholder for initiative profile completeness
- `scripts/` bundle validator

## Recommended Execution Order

1. `subbundles/01-remove-persisted-sync-and-assemble-projections`
2. `subbundles/02-stabilize-node-carrier-bindings-and-canonical-hierarchy`
3. `subbundles/03-centralize-node-kind-registry-lifecycle-and-role-capabilities`
4. `subbundles/04-harden-node-scope-and-assignment-boundaries`
5. `subbundles/05-build-plugin-platform-and-cross-module-orchestration`
6. `subbundles/06-service-decomposition-guardrail-tests-and-plugin-gate-review`

## Validation Summary

- Bundle preparation status: `Prepared-stage validation passed after bundle repair`
- Bundle readiness gate: `Passed after bundle repair`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Completed`
- Browser validation analytics: `Captured with targeted Playwright proof`
- Proof summary:
  `10/10` targeted unit tests passed
  `33/33` targeted integration tests passed
  `6/6` targeted component tests passed
  `1/1` targeted Playwright flow passed
