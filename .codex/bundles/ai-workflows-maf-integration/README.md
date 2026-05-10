# AI Workflows MAF Integration

This bundle is a planning-only coordination package for adding first-class AI workflows to CanDoItAll using Microsoft Agent Framework workflows. It intentionally does not implement feature code.

## Profile

- `initiative`

## Mission

- Add workflow definitions, workflow execution, workflow testing, workflow UI, workflow canvas editing, and process-role workflow execution while keeping processes above workflows and agents. Workflows become a peer execution option beside AI agents, not a replacement for process orchestration.

## Outcome Contract

- Requested outcome: an implementation-ready, phased bundle that directs agents through MAF wrapper foundations, runtime management, workflow catalog/settings/testing, Agents module UI, canvas editing, process integration, web API integration, and final validation.
- Hard constraints: planning only; use the local MAF source clone at `C:\repositories\agent-framework`; preserve strong typing; avoid silent fallback behavior; keep workflows in the existing AgentFramework module with their own page; keep processes as the higher-level orchestrator.
- Evidence required before closure: each implementation subbundle must capture build/test proof, architecture review findings, API proof where relevant, browser screenshots for UI phases, and execution-report gate updates.
- Known blockers or explicit scope exceptions: no product implementation is included in this prepared bundle; final schema names and route names are implementation decisions, but the bundle constrains the required boundaries and validation.

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

1. `subbundles/01-maf-workflow-wrapper-foundation-and-architecture-review`
2. `subbundles/02-workflow-runtime-core-and-durable-run-management`
3. `subbundles/03-workflow-catalog-settings-api-and-tests`
4. `subbundles/04-agents-module-workflows-page`
5. `subbundles/05-workflow-canvas-editor-and-component-library`
6. `subbundles/06-process-role-workflow-integration`
7. `subbundles/07-web-api-navigation-and-app-integration`
8. `subbundles/08-end-to-end-validation-architecture-review-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Not started`
- Subbundle gate review: `Pending implementation`
- Final closure gate: `Pending implementation`
- Browser validation analytics: `Pending implementation`
