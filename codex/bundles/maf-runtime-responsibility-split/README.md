# MAF Runtime Responsibility Split

This initiative bundle tracks the staged refactor of `MafAgentRuntime` from a large partial-class runtime into smaller, testable collaborators with explicit responsibilities, plus the follow-up local-provider/MCP agent-chat repair found during real app validation.

## Profile

- `initiative`

## Mission

Split MAF runtime responsibilities without changing intended runtime behavior. The implementation moves broadly reusable helpers into shared foundation code, moves MAF-only helper logic into focused MAF collaborators, extracts session/model/context builders, isolates required-finalizer behavior behind a driver boundary, and leaves `MafAgentRuntime` as orchestration rather than a catch-all implementation class. The SB09 addendum repairs the local Ollama provider path where agent chat failed to send the configured local model even though provider health/workflows succeeded, and proves local Playwright MCP tools execute through agent chat.

## Outcome Contract

- Original outcome: prepare an implementation-ready bundle before the larger refactor.
- Executed outcome: SB01-SB08 refactor implemented with proof; SB09 provider/MCP repair implemented after the local-provider regression report.
- Hard constraints: preserve public contracts unless a subbundle explicitly proves a compatible migration; keep strongly typed boundaries; do not hide errors behind silent fallback mechanisms; do not add abstractions unless they remove real responsibility or enable a real runtime/test boundary.
- Evidence required before implementation closure: bundle validation passes; every raw input is traceable to a subbundle; the `.xlsx` checklist is generated and visually checked; implementation proof includes build, unit, integration, static-scan, live API proof, and real browser/UI proof.
- Known scope limits: this bundle does not rewrite unrelated provider/UI areas beyond the SB09 agent-chat local-provider and Playwright MCP runtime repair.

## Bundle Layout

- `inputs/` preserves the raw user request and structured interpretation.
- `analysis/` records current repo observations, assumptions, risks, and reopen triggers.
- `requirements/` normalizes the requested refactor into testable requirements.
- `architecture/` defines the target responsibility boundaries and helper placement decisions.
- `inventories/` records source files, responsibilities, tests, and UI proof surfaces.
- `plan/` defines execution order, dependency map, critical subbundles, and gates.
- `traceability/` maps raw inputs and normalized requirements to owning subbundles and planned proof.
- `shared-prompts/` gives reusable implementation and QA handoff prompts.
- `subbundles/` contains numbered execution workstreams.
- `templates/` contains reusable proof and subbundle templates.
- `reviews/` seeds execution reporting, browser analytics, and self-review.

## Recommended Execution Order

1. `subbundles/01-inventory-and-refactor-boundaries`
2. `subbundles/02-shared-helpers-and-argument-formatting`
3. `subbundles/03-session-builder-extraction`
4. `subbundles/04-model-parameters-builder-extraction`
5. `subbundles/05-context-manifest-builder-extraction`
6. `subbundles/06-finalizer-driver-isolation`
7. `subbundles/07-runtime-orchestration-slimming`
8. `subbundles/08-regression-and-ui-proof`
9. `subbundles/09-local-provider-agent-chat-repair`

## Planning Workbook

- Workbook: `bundle-checklists.xlsx`
- Workbook purpose: detailed execution checklist, test plan, UI proof plan, risk register, and traceability matrix for the implementation agent.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Implemented`
- Runtime refactor execution status: `Implemented`
- Local provider/MCP repair status: `Implemented`
- Subbundle gate review: `SB01-SB09 proof captured`
- Final closure gate: `Implemented with focused tests, live API proof, and real UI proof`
- Browser validation analytics: `Passed on live app http://127.0.0.1:5032 for agent chat, project-structure chat, capability setup, and local Playwright MCP tool invocation through UI chat`
