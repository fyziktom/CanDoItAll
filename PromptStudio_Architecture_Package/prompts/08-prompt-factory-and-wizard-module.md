# Codex Prompt 08 — Prompt Factory and Phase Wizard

## Objective
Implement the guided prompt factory, blueprint catalog, context assembly pipeline, prompt preview/editing, validation warnings, and send/export flows.

## Required reading
1. `docs/01-ux-discovery.md`
2. `docs/03-ui-architecture-and-ascii-layouts.md`
3. `docs/03a-workbench-tabs-canvas-and-state.md`
4. `docs/04-solution-architecture.md`
5. `docs/06-architecture-review-gap-analysis.md`
6. `docs/07-implementation-plan.md`
7. `docs/09-validation-and-testing-plan.md`

## Constraints
- Use .NET 10 and C#.
- Use Blazor Web App with Interactive Server rendering.
- Use Tailwind CSS and the shared component strategy.
- Keep code comments in English.
- Preserve the modular monolith boundaries from the architecture package.
- Prefer one `DbContext` per operation via `IDbContextFactory`.
- Keep business logic out of page-only code.
- Do not log or expose secrets.
- Add or update tests for the touched behavior.
- Keep naming and file structure aligned with the package documents.

## Scope
This prompt covers M6: the core prompt-creation workflow that turns project context into a usable prompt for Codex or another model.

## Tasks
1. Implement the Factory module domain: blueprints, build sessions, context assemblies, validation results.
2. Build the wizard stepper and main Prompt Factory UI.
3. Implement project phase selection and blueprint recommendation.
4. Implement context assembly from project metadata, options, selected resources, and prior records where appropriate.
5. Implement generated prompt rendering and editing.
6. Implement validation warnings for missing context, provider mismatch, or sensitive content.
7. Implement save-as-draft, save-as-final, copy, export, and provider-send actions.
8. Ensure prompt factory output integrates with the Prompts module instead of bypassing it.
9. Integrate prompt sessions and prompt steps with the project structure workbench model so follow-up prompts can branch from a prior step.
10. Add tests for context assembly, blueprint rendering, validation warnings, and wizard flow.

## Required deliverables
- Factory module domain and services
- Prompt Factory UI
- blueprint selection
- context assembly pipeline
- generated prompt preview/editor
- save/export/send flows
- build session persistence
- automated tests

## Acceptance criteria
- a user can choose project + phase + blueprint and get a generated prompt
- selected resources and stack options appear in the context assembly
- warnings appear before send/export where required
- save-as-draft/save-as-final integrate with the prompt library correctly
- provider-send path goes through the provider abstraction
- tests cover the main wizard and context rules

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the Prompt Factory is usable end-to-end and traceable.
