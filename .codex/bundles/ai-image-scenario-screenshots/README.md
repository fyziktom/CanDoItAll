# AI Image Scenario Screenshots

This bundle is a coordination and execution package for `ai-image-scenario-screenshots`.

## Profile

- `initiative`

## Mission

Add image-generation provider profiles and agent preferences, seed screenshot-oriented process templates and agent templates, create project-structure records for the three Dev55 scenario apps, run the first screenshot process end to end, then extend the workflow so stored screenshots can drive AI-generated layout-improvement assets. The process core must remain generic; scenario-specific behavior belongs in process-step descriptions, agent instructions, capabilities, skills, and tools.

## Outcome Contract

- Requested outcome: Image-capable providers and agents can capture, review, store, and reuse scenario app screenshots through generic CanDoItAll projects and processes.
- Hard constraints: Keep provider/tool access strongly typed; do not encode screenshot or image-generation special cases into process core; use OpenAI API as the first image provider with a cheap default model; leave later ComfyUI support as an explicit extension point; store screenshots and generated layouts as file/image asset nodes through project structure.
- Evidence required before closure: prepared and completed bundle validators, targeted build/tests, process-template import/list proof, project-structure readback proof, first scenario run proof with Playwright MCP screenshot artifacts, stored image asset readback, and layout-generation asset readback.
- Known blockers or explicit scope exceptions: live image generation depends on valid `OPENAI_API_KEY`; if the key is unavailable, provider profile/configuration and agent workflow still ship but live image-generation proof must be recorded as blocked.

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

1. `subbundles/01-image-provider-profile-foundation`
2. `subbundles/02-scenario-project-structure-seeding`
3. `subbundles/03-screenshot-process-template-pack`
4. `subbundles/04-screenshot-agent-template-and-asset-storage`
5. `subbundles/05-first-scenario-runtime-proof`
6. `subbundles/06-layout-image-generation-workflow`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Reopened and completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
- Reopened validation report: `reviews/02-reopened-screenshot-validation.md`
