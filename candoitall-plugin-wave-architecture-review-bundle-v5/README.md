# CanDoItAll Plugin-Wave Architecture Review Bundle V5

This bundle reviewed the **post-refactor** codebase with one specific decision in mind: **is the architecture strong enough to start the next plugin wave (email, LinkedIn, custom APIs, and similar integrations)?** The initial review answer was `NO-GO`. SB01 through SB05 are now implemented and validated, so the verdict below reflects the post-execution state.

## Verdict

- **Initial review before execution:** `NO-GO`
- **Post-execution small feature work / bugfixes:** `GO`
- **Post-execution major external plugin wave:** `GO with guarded rollout`

The initial review identified five real blockers: persisted Workbench parallel truth, an overloaded carrier row, fragmented kind semantics, shallow lifecycle tracking, and static plugin seams. SB01 through SB05 now close or harden those areas:

- persisted Workbench projections are no longer stored as canonical truth
- carrier data is split from bindings and foreign references while preserving canonical X/Y and markers
- node kinds, labels, palette hints, and reclassification rules are registry-driven with lifecycle history
- connector/provider/resource integration is manifest- and registry-driven with durable recovery tracking
- the Workbench hotspot is decomposed and protected by architecture, integration, component, and Playwright proof

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
- Execution status: `Completed`
- Subbundle gate review: `SB01-SB05 passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Targeted Playwright proof captured for structure catalog, mutation, subtree transfer, and project-assignment sync flows`
