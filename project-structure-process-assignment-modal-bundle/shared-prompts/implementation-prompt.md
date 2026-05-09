# Implementation Prompt

Implement the bundle subbundles in order. Preserve the current process launch state model and required-role gate. Use shared CanDoItAll BaseLib layout components before custom wrappers. Reuse `AgentSwitchDialog` for manual AI-agent selection and only add backend selection support if existing launch candidates cannot cover the selected technical agent.

For each subbundle:

- Read the subbundle README, source references, and traceability rows.
- Make the smallest cohesive code change that satisfies the acceptance checklist.
- Run targeted tests before marking it complete.
- Record changed files, commands, and proof in `reviews/01-execution-report.md`.
