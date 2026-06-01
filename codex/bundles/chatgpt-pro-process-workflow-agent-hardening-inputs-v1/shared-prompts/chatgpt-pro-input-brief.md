# ChatGPT Pro Input Brief

Use this packet to prepare a separate detailed complex bundle for refactoring and hardening CanDoItAll processes, workflows, agents, templates, skills, tools, and MCP integrations.

Do not treat this packet as a design decision. It is an evidence packet.

Required starting points:

1. Read `README.md` and `inputs/00-original-request.md` to preserve the user constraints.
2. Read `inputs/01-live-runtime-evidence.md` and the raw API files under `inputs/api-captures`.
3. Read `inputs/02-repository-delta-since-6e4f6dae.md`.
4. Read `inputs/03-agent-tools-skills-mcp-evidence.md`.
5. Read `analysis/02-observed-weak-spots.md`.
6. Read `inventories/01-hotspot-files-and-apis.md`.

Important interpretation:

- The Tetris process run was successful and should be treated as positive evidence for the current direction.
- The later bundle should improve implementation quality and hardening, not discard the working model without evidence.
- Focus on canonicity, maintainability, performance, threading, parallelism, evidence freshness, external side effects, and tool-policy consistency.
- Pay special attention to large responsibility centers and cross-surface string-key drift.
- Use the API evidence before drawing conclusions from templates or code alone.

Hard constraints from the input-preparation request:

- This input packet intentionally contains no architecture proposal.
- This input packet intentionally contains no subbundle decomposition.
- This input packet intentionally contains no implementation.
