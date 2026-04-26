# Implementation Prompt

Execute the selected subbundle only. Keep the work documentation-only unless a validator proves a non-doc change is required.

Rules:

- Read the subbundle README, root bundle README, phase plan, traceability table, and original request before editing.
- Use actual source references as the architecture source of truth.
- Do not invent APIs, background workers, modules, MCP behavior, provider support, or deployment shape.
- Keep project README files concise and source-grounded.
- Do not edit generated output under `bin` or `obj`.
- Update `reviews/01-execution-report.md` with proof before closing the subbundle.
