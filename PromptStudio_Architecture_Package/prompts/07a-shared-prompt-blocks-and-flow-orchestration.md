# Codex Prompt 07A — Shared Prompt Blocks and Flow Orchestration

## Objective
Implement the reusable prompt-building foundation: shared prompt blocks, prompt-flow templates, prompt runs, branch-aware node state, and workbench integration for those flows.

## Required reading
1. `README.md`
2. `docs/01-ux-discovery.md`
3. `docs/02-technical-requirements.md`
4. `docs/03-ui-architecture-and-ascii-layouts.md`
5. `docs/03a-workbench-tabs-canvas-and-state.md`
6. `docs/04-solution-architecture.md`
7. `docs/06-architecture-review-gap-analysis.md`
8. `docs/07-implementation-plan.md`
9. `docs/08-checklists.md`
10. `docs/09-validation-and-testing-plan.md`

## Constraints
- Use .NET 10 and C#.
- Preserve the modular monolith boundaries from the architecture package.
- Keep business logic out of page-only code.
- Reusable prompt instructions must not be hardcoded inside pages, Razor components, or one-off handlers.
- Treat shared prompt blocks and prompt-flow templates as centrally governed domain assets.
- Keep prompt-flow state and branching logic authoritative in C# so it is unit-testable without the canvas renderer.
- Add or update tests for the touched behavior.

## Scope
This prompt covers M5A: the domain and orchestration layer that must exist before the Prompt Factory wizard is allowed to compose prompts from it.

## Tasks
1. Implement the Factory module domain for `PromptBlockDefinition`, `PromptFlowTemplate`, `PromptRun`, and `PromptRunNode`.
2. Implement persistence and application services for creating flows, auto-applying recommended blocks, branching runs, changing node state, and recording lineage.
3. Support node states such as pending, prepared, running, used, skipped, failed, validated, and superseded.
4. Support multiple concurrent prompt branches for the same project or feature without losing traceability.
5. Integrate prompt-run projections with the Workbench module so the structure canvas can display flow-template nodes and prompt-run nodes.
6. Add CRUD or management surfaces needed to maintain the shared prompt block catalog and flow template catalog.
7. Add tests for reusable-block composition, auto-application rules, branch lineage, parallel branch handling, and state transitions.

## Required deliverables
- shared prompt block domain and persistence
- prompt-flow template domain and persistence
- prompt-run orchestration services
- workbench projections for prompt-flow nodes
- automated tests

## Acceptance criteria
- repeated delivery instructions are managed from one shared place instead of being copied into multiple prompts
- recommended shared blocks can be auto-applied by prompt type before user customization
- a project can initialize a prompt flow from a reusable template
- prompt-run nodes persist their state and lineage
- multiple prompt branches can run in parallel for one project without ambiguity
- the workbench can display prompt-flow nodes credibly before the wizard UI is built
- tests cover the critical orchestration rules

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the reusable prompt-block and prompt-flow foundation is implemented and test-covered enough for the Prompt Factory wizard to consume it cleanly.
