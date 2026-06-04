# Provider And Capability Catalog UI

This bundle is a coordination and execution package for `provider-capability-catalog-ui-v1`.

## Profile

- `initiative`

## Mission

- Repair the `/agents?tab=providers` and `/agents?tab=capabilities` catalog surfaces so provider counts, provider lists, capability assignment, metadata editing, tags, and new MCP/Skill creation all operate from the AgentFramework runtime catalog with compact desktop-oriented Blazor UI.

## Outcome Contract

- Requested outcome: the Agents shell provider badge and provider tab show the same merged AgentFramework provider set; default catalog includes local Ollama; providers and capabilities have editable tags; capability assignment uses an agent tree, desktop card grid, search/tag/type/assignment filters, details dialogs, and an MCP/Skill setup wizard.
- Hard constraints: use existing Blazor/component primitives (`TreeView`, `TagEditor`, `Steps`, `InputFile`, `ListDetailShell`, `DialogService`); keep Workspace settings provider management intact; do not add chat prompt `/skills-tag:*` behavior; keep changes strongly typed and scoped to AgentFramework catalog surfaces.
- Evidence required before closure: prepared/completed bundle validator output, targeted unit/component tests, module or solution build, source assertions for count parity and tag persistence, and browser proof for `/agents?tab=providers` plus `/agents?tab=capabilities` at a large desktop viewport with dialogs open.
- Known blockers or explicit scope exceptions: components MCP returned `Transport closed`, so component discovery is source-based; generated image proposals are planning-only and do not count as shipped UI proof.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-provider-catalog-parity-and-tags`
2. `subbundles/02-capability-assignment-tree-filters-and-details`
3. `subbundles/03-capability-setup-wizard-and-visual-proof`
4. Final raw-note closure, browser evidence review, and completed-stage validation.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
