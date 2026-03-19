# PromptStudio Architecture Package

This package contains an implementation-ready architecture blueprint for **PromptStudio** — a local-first, modular prompt engineering workbench for software delivery workflows, built primarily in **C#**, **.NET 10**, **Blazor Web App with Interactive Server rendering**, **EF Core 10**, and **Tailwind CSS**.

All documents are written in English and are organized for a practical handoff to **Codex** or another implementation agent.

## Reading order

1. `docs/01-ux-discovery.md`
2. `docs/02-technical-requirements.md`
3. `docs/03-ui-architecture-and-ascii-layouts.md`
4. `docs/04-solution-architecture.md`
5. `docs/05-requirement-coverage-matrix.md`
6. `docs/06-architecture-review-gap-analysis.md`
7. `docs/07-implementation-plan.md`
8. `docs/08-checklists.md`
9. `docs/09-validation-and-testing-plan.md`
10. `docs/10-executive-qa-review.md`
11. `docs/11-references.md`
12. `prompts/00-codex-starting-prompt.md`
13. `prompts/01-bootstrap-solution.md` ... `prompts/12-hardening-observability-and-packaging.md`

## Package structure

- `docs/` — architecture, UX, QA, testing, implementation planning
- `prompts/` — sequenced prompts for Codex
- `diagrams/` — Mermaid diagrams for context, modules, deployment, and domain model

## Working title

**PromptStudio** is only a working name. The architecture is intentionally neutral and can be rebranded without structural changes.

## Product intent

PromptStudio is designed to help a software architect, developer, reviewer, or technical lead produce and manage prompts for recurring delivery moments such as:

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

## Recommended execution mode

Use the documents in this order:

1. Approve or adjust the UX assumptions.
2. Lock the technical requirements.
3. Approve the UI structure and navigation model.
4. Approve the architecture.
5. Use the Codex prompts sequentially.
6. Run the validation and testing plan after every milestone.

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

## Quick start

Start with `prompts/00-codex-starting-prompt.md`.