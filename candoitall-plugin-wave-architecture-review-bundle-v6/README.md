# CanDoItAll Plugin-Wave Architecture Review Bundle V6

This bundle reviews the **post-phase-5 refactor** codebase with one decision in mind:

> **Is the current architecture finally strong enough to continue into the large external plugin wave (email, LinkedIn, custom APIs, and similar connectors)?**

## Verdict

- **Contained feature work / bugfixes:** `GO with caution`
- **Major external plugin wave:** `NO-GO until the subbundles in this bundle are completed`

Phase 5 clearly improved several seams, especially around typed node references, CRM/HR ownership direction, hierarchy-cycle protection, and compensation coverage. However, the current codebase is **still not the correct base** for the next plugin wave because the deepest canonical-model blockers are still open:

- persisted Workbench sync still creates a parallel truth
- the universal node carrier is still too broad
- node-kind semantics are still fragmented
- node lifecycle/reclassification is still too shallow
- provider/resource/connector extensibility is still enum/switch driven
- assignment and node-scope semantics are still not strong enough for the next wave

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

- Bundle preparation status: `Prepared-stage validation passed`
- Bundle readiness gate: `Passed`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Blocked in this environment because dotnet SDK/runtime is unavailable; runtime proof must be produced by Codex in a real .NET environment`
