# CanDoItAll Architecture Package

This package contains an implementation-ready architecture blueprint for **CanDoItAll** — a local-first, modular software delivery environment built primarily in **C#**, **.NET 10**, **Blazor Web App with Interactive Server rendering**, **EF Core 10**, and **Tailwind CSS**.

All documents are written in English and are organized for a practical handoff to **Codex** or another implementation agent.

This package now assumes the repository also contains:

- the shared Blazor component library in `C:\repositories\CanDoItAll\src\CanDoItAll.Components`
- shared component guidance in `C:\repositories\CanDoItAll\docs\ui-shared-components`
- canvas source-analysis packs in `C:\repositories\CanDoItAll\docs\canvas-playlist-builder` and `C:\repositories\CanDoItAll\docs\canvas-events-calendar`

## Reading order

1. `docs/01-ux-discovery.md`
2. `docs/02-technical-requirements.md`
3. `docs/03-ui-architecture-and-ascii-layouts.md`
4. `docs/03a-workbench-tabs-canvas-and-state.md`
5. `docs/03b-development-manager-watch-capsules-and-tuning.md`
6. `docs/04-solution-architecture.md`
7. `docs/05-requirement-coverage-matrix.md`
8. `docs/06-architecture-review-gap-analysis.md`
9. `docs/07-implementation-plan.md`
10. `docs/08-checklists.md`
11. `docs/09-validation-and-testing-plan.md`
12. `docs/10-executive-qa-review.md`
13. `docs/11-references.md`
14. `prompts/00-codex-starting-prompt.md`
15. `prompts/01-bootstrap-solution.md`
16. `prompts/01a-development-manager-watch-capsules-and-tuning.md`
17. `prompts/02-shared-kernel-and-infrastructure.md` ... `prompts/06-assets-and-connectors-module.md`
18. `prompts/06a-workbench-tabs-project-structure-and-calendar.md`
19. `prompts/07-prompt-library-and-gallery-module.md`
20. `prompts/07a-shared-prompt-blocks-and-flow-orchestration.md`
21. `prompts/08-prompt-factory-and-wizard-module.md` ... `prompts/12-hardening-observability-and-packaging.md`

## Package structure

- `docs/` — architecture, UX, QA, testing, implementation planning
- `prompts/` — sequenced prompts for Codex
- `diagrams/` — Mermaid diagrams for context, modules, deployment, and domain model

## Naming

**CanDoItAll** is the application and environment name.

**PromptStudio** is not the app name. If used at all, it should refer only to the prompt-focused workspace/module inside CanDoItAll, alongside project planning and other workbench areas.

The `PromptStudio_Architecture_Package` folder name can remain as a historical package label for now, but the implementation and runtime product naming should use `CanDoItAll`.

## Product intent

CanDoItAll is designed to help a software architect, developer, reviewer, or technical lead manage projects, planning, prompts, validation, testing, and delivery workflows in one environment.

The prompt-focused area inside that environment can be branded as a `PromptStudio` module or workspace if desired. It is one capability area, not the whole product.

The prompt-focused workspace is designed to help users produce and manage prompts for recurring delivery moments such as:

- UX discovery
- architecture definition
- architecture review
- implementation planning
- first implementation
- prototype validation
- revision planning
- test planning
- test addition and verification
- feature iteration loops

The system must support both:
- **structured human-led work**
- **agent-assisted work** (Codex or other LLMs)

## Core architectural stance

The package recommends a **modular monolith first** approach with clear internal module boundaries, durable events, and sidecar-ready contracts so the solution can evolve into multiple local or remote services later without rewriting the entire application.

## What this package already covers

- UX inputs, roles, actors, stories, and use cases
- generalized option modeling for project stacks and delivery choices
- UI architecture and ASCII layouts
- internal tab-workbench architecture, crash recovery, and sleeping-tab lifecycle
- project structure canvas and project events calendar integration strategy
- shared prompt blocks, prompt-flow templates, and reusable delivery sequences
- development acceleration through a local manager, `dotnet watch`, and a Codex-facing local API
- compressed source capsules that generate Codex-optimized reference artifacts
- dev-only tuning mode for targeted UI/component refinement loops
- modular solution structure
- domain and storage design
- secret handling and provider configuration
- OpenAI and Ollama integration strategy
- project and prompt lifecycle management
- validation and review workflows
- implementation phases and milestone plan
- sequential Codex prompts
- testing, Playwright, screenshot, and validation strategy
- executive QA review and coverage assessment
- integration references to the shared component library and the two canvas source packs

## Recommended execution mode

Use the documents in this order:

1. Approve or adjust the UX assumptions.
2. Lock the technical requirements.
3. Approve the UI structure, navigation model, and internal workbench strategy.
4. Approve the development manager, watch loop, capsule rules, and tuning workflow.
5. Approve the architecture.
6. Review the shared component and canvas source-analysis packs in the main repository.
7. Use the Codex prompts sequentially, including `prompts/01a-development-manager-watch-capsules-and-tuning.md` and `prompts/06a-workbench-tabs-project-structure-and-calendar.md`.
8. Do not skip `prompts/07a-shared-prompt-blocks-and-flow-orchestration.md`; it lays the domain and workbench contracts required before the Prompt Factory wizard is built.
9. Run the validation and testing plan after every milestone.

## Expected implementation philosophy

- Default to **deterministic logic first**.
- Use LLMs for generation, review, summarization, guided heuristics, and productivity improvements.
- Do not let the LLM become the primary source of truth for security, persistence, or execution control.
- Keep all execution of scripts, Docker, SSH, or other external actions behind explicit human approval.
- Optimize for maintainability, auditability, and future extraction of services.

## Important delivery conventions for Codex

- Keep all source code comments in English.
- Prefer small vertical slices with strong module ownership.
- Avoid accidental coupling between modules.
- Keep infrastructure replaceable.
- Never put secrets into logs, diagnostics, or prompt history.
- Prefer one `DbContext` per operation through `IDbContextFactory`.
- Use existing shared UI components whenever possible and create missing components in the same style.
- Treat internal tabs, project structure canvas, and project calendar as core architecture, not optional polish.
- Treat shared prompt blocks and prompt-flow templates as centrally governed assets, not page-local strings.
- Treat the local manager, watch-ready loop, capsule freshness, and dev-only tuning workflow as productivity-critical infrastructure, not loose tooling.
- Keep canvas and calendar JavaScript engines focused on rendering and interaction capture; keep business rules, command handling, validation, and persistence in C#.

## Quick start

Start with `prompts/00-codex-starting-prompt.md`.
