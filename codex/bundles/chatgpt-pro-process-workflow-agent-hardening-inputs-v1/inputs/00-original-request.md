# Original Request

Prepare an input-information bundle for ChatGPT Pro. ChatGPT Pro will later prepare a detailed complex bundle for refactoring and hardening of:

- processes
- workflows
- agents
- process, workflow, agent templates
- skills
- tools
- MCP integrations

The requested preparation must use live information from the CanDoItAll APIs and repository changes since commit `6e4f6dae9a4b654fde4243a421d72add4074d8cf`.

Explicit user constraints:

- Do not propose architecture.
- Do not propose subbundles.
- Do not do implementation.
- Prepare detailed input information only.
- Use process API and agent API for the last successful Tetris app run.
- Use workflow API for the Office365 summary workflow run if it exists.
- If no workflow run exists, run the Office365 email-summary-by-category workflow. A workflow run did exist, so no rerun was performed.
- Place the bundle in `codex/bundles`.

Assumption used during preparation:

The successful Tetris process run in the development database is the root process run `6724b4c8-c774-4880-becc-940a3d7bf155`, named `Main App / Multi-team software delivery and release governance`, updated on `2026-06-01`.

