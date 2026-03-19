# Codex Prompt 00 — Starting Prompt for the Full Implementation

## Objective
Implement PromptStudio incrementally according to the architecture package in this directory. In this session, you must first align yourself to the architecture and then start with **Milestone M0 only** unless the repository already contains M0-level work.

## Required reading order
1. `README.md`
2. `docs/01-ux-discovery.md`
3. `docs/02-technical-requirements.md`
4. `docs/03-ui-architecture-and-ascii-layouts.md`
5. `docs/04-solution-architecture.md`
6. `docs/05-requirement-coverage-matrix.md`
7. `docs/07-implementation-plan.md`
8. `docs/08-checklists.md`
9. `docs/09-validation-and-testing-plan.md`
10. `docs/10-executive-qa-review.md`

## Product summary
You are building a local-first modular workstation for software-delivery prompt workflows. The application must support:
- project creation and stack profiling
- typed linked resources
- prompt library and versioning
- phase-driven prompt factory
- validation workflows
- test planning and evidence
- secure provider and secret handling
- OpenAI and Ollama integration paths
- future-ready sidecar/microservice seams
- a unified Tailwind-based UI

## Operating constraints
- Use **.NET 10** and **C#**.
- The main application is a **Blazor Web App** using **Interactive Server rendering**.
- Styling uses **Tailwind CSS** and the existing component set.
- Keep all code comments in English.
- Keep the architecture as a **modular monolith**.
- Prefer one `DbContext` per operation via `IDbContextFactory`.
- Do not place domain/application logic directly in pages.
- Do not store or log raw secrets.
- Do not implement dangerous execution flows without explicit approval gates.
- Keep files and naming aligned to the architecture package.
- Add automated tests continuously.

## Session instructions
1. Read the architecture package.
2. Compare it to the current repository state.
3. If the repository is empty or close to empty, implement **M0 — Foundation**.
4. If M0 is already present, validate it against the package and fix deviations before proceeding.
5. Do not jump ahead to later milestones in this session unless M0 is already fully complete and validated.

## M0 target outcome
- solution and project structure
- Blazor host
- module registration pattern
- SharedKernel baseline
- Infrastructure baseline
- ComponentKit baseline
- shell layout and route placeholders
- Tailwind integration
- test project setup
- scripts/docs needed to start development cleanly

## Required deliverables for this session
- compilable solution structure
- navigable app shell
- placeholder pages for all main areas
- module registration extensions
- basic test project wiring
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
Stop after M0 is in a clean, reviewable state.