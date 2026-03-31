# Implementation Prompt

Implement only the named subbundle from `solution-style-unification-bundle-v1`.

Rules:

- Read the root `README.md`, `plan/01-phase-plan.md`, `traceability/01-requirement-traceability.md`, and the target subbundle README before editing anything.
- Respect the exclusion boundary around CanvasLib and canvas-host surfaces.
- Prefer BaseLib primitives first, semantic Tailwind component-layer classes second, and raw repeated utility strings last.
- Keep changes small, strongly typed, and maintainable. Do not invent trivial abstraction layers.
- Update bundle status, execution-report rows, and proof paths while the work is fresh.
- If later proof exposes a weak earlier foundation, reopen the earlier subbundle instead of forcing forward progress.

Expected proof:

- Tailwind build when shared CSS changes
- `dotnet build` for affected projects
- Playwright MCP route proof and screenshots for any UI-affecting work
- Progress metrics for replaced occurrences, unified families, and CSS/code reduction
