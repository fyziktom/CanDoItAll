# Shared Implementation Prompt

You are implementing the next CanDoItAll process runtime hardening bundle.

Rules:

- Keep process runtime generic; avoid Blazor/.NET-only behavior unless a test fixture is specifically software-related.
- Do not confuse workflows with processes. Workflow runs are executor/runtime outputs; process finalizer and process artifacts are process-owned.
- Do not add SQLite support, migrations, or validation.
- Prefer typed fields, records, and explicit source kinds over string parsing.
- Prefer runtime tests that exercise production emitters/consumers over source-only assertions.
- When a subbundle creates new durable state, add proof for producer, consumer, lifecycle, and negative behavior.
- Keep comments in source code in English.
