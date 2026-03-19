# Codex Prompt 00 — Starting Prompt for the Full Implementation

## Objective
Implement CanDoItAll incrementally according to the architecture package in this directory. In this session, you must first align yourself to the architecture and then start with **Milestone M0**, followed by **M0A** only if M0 is already complete and clean.

## Required reading order
1. `README.md`
2. `docs/01-ux-discovery.md`
3. `docs/02-technical-requirements.md`
4. `docs/03-ui-architecture-and-ascii-layouts.md`
5. `docs/03a-workbench-tabs-canvas-and-state.md`
6. `docs/03b-development-manager-watch-capsules-and-tuning.md`
7. `docs/04-solution-architecture.md`
8. `docs/05-requirement-coverage-matrix.md`
9. `docs/07-implementation-plan.md`
10. `docs/08-checklists.md`
11. `docs/09-validation-and-testing-plan.md`
12. `docs/10-executive-qa-review.md`
13. `C:\repositories\CanDoItAll\docs\ui-shared-components\README.md`
14. `C:\repositories\CanDoItAll\docs\ui-shared-components\recommendations\missing-components.md`
15. `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\README.md`
16. `C:\repositories\CanDoItAll\docs\canvas-events-calendar\README.md`

## Product summary
You are building a local-first modular workstation for software-delivery prompt workflows. The application must support:
- project creation and stack profiling
- typed linked resources
- prompt library and versioning
- shared prompt blocks and reusable prompt-flow templates
- phase-driven prompt factory
- validation workflows
- test planning and evidence
- internal application tabs with restore and sleeping behavior
- project structure canvas and project events calendar
- local development manager with watch-ready signals and capsule generation
- dev-only tuning mode for targeted component refinement
- secure provider and secret handling
- OpenAI and Ollama integration paths
- future-ready sidecar/microservice seams
- a unified Tailwind-based UI

## Operating constraints
- Use **.NET 10** and **C#**.
- The main application is a **Blazor Web App** using **Interactive Server rendering**.
- Styling uses **Tailwind CSS** and the existing component set.
- The shared component baseline is the `CanDoItAll.Components` library already present in this repository.
- Keep all code comments in English.
- Keep the architecture as a **modular monolith**.
- Prefer one `DbContext` per operation via `IDbContextFactory`.
- Do not place domain/application logic directly in pages.
- Do not store or log raw secrets.
- Do not implement dangerous execution flows without explicit approval gates.
- Keep files and naming aligned to the architecture package.
- Add automated tests continuously.
- Treat the internal workbench, project structure, and project calendar as core architecture, not optional polish.
- Treat the development manager, source capsules, and tuning workflow as early productivity infrastructure, not afterthought tooling.

## Session instructions
1. Read the architecture package.
2. Compare it to the current repository state.
3. If the repository is empty or close to empty, implement **M0 — Foundation**.
4. If M0 is already present, validate it against the package and fix deviations before proceeding.
5. If M0 is complete and validated, continue with **M0A - Development acceleration**.
6. Do not jump ahead to later milestones in this session unless M0 and M0A are already fully complete and validated.

## M0 target outcome
- solution and project structure
- Blazor host
- module registration pattern
- SharedKernel baseline
- Infrastructure baseline
- ComponentKit baseline
- shell layout and route placeholders
- internal tab workspace baseline
- Tailwind integration
- test project setup
- scripts/docs needed to start development cleanly

## Required deliverables for this session
- compilable solution structure
- navigable app shell
- placeholder pages for all main areas
- module registration extensions
- basic test project wiring
- wiring that is ready for internal tabs, canvas wrappers, and calendar wrappers
- concise repo-level setup notes

## Files you are likely to create
- solution and project files
- `Program.cs`
- shell layouts and app routes
- module registration extensions
- base shared types
- component kit shell wrappers
- initial test project files
- Tailwind configuration/build files
- repo README or implementation notes

## Required output format
1. Architecture alignment summary
2. M0 implementation plan
3. Created/modified files
4. Test/build commands executed
5. What is complete
6. What remains for the next prompt

## Exit condition
Stop after M0 is in a clean, reviewable state, or after M0A as well if M0 was already complete when the session began.
