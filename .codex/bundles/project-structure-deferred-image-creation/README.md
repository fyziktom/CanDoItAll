# Project Structure Deferred Image Creation

This bundle coordinates the project-structure change that makes generated image nodes appear immediately, while image generation finishes in the background and updates the same canonical node.

## Profile

- `initiative`

## Mission

Create a reliable generic deferred node completion path for project structure nodes, then use it for generated image assets so the user sees a waiting image node immediately and the completed provider image replaces the placeholder without recreating the graph node.

## Outcome Contract

- Requested outcome: generated image creation from the project structure right-click flow must pass the textarea prompt and all provider fields to the selected image provider, create the image asset node immediately, show a waiting placeholder, and update the same node when image generation completes.
- Hard constraints: keep project object canonicity in `ProjectWorkbenchService`; do not add client-only fake nodes; avoid full graph reload loops for normal create/update flow; do not silently swallow provider failures; do not change existing dropdown mechanics.
- Evidence required before closure: component tests for prompt transfer and deferred node update, service tests for media replacement or deferred completion, clean build, targeted test run, and Playwright proof on `http://localhost:5032/projects/.../structure` through the right-click Generate image path.
- Known blockers or explicit scope exceptions: if local ComfyUI cannot be reached after code changes, stop before browser proof and report it as a blocker instead of masking with mock output.

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

1. `subbundles/01-prompt-contract-and-provider-proof`
2. `subbundles/02-generic-deferred-node-completion`
3. `subbundles/03-generated-image-pending-node-flow`
4. `subbundles/04-validation-and-browser-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared and validated`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed-stage validator passed`
- Browser validation analytics: `Completed`
