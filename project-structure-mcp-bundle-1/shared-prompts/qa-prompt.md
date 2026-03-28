# QA Prompt

Validate the active subbundle against its acceptance checklist and proof contract.

Rules:

- Prefer real observable proof over reasoning.
- For UI work, open the page in Playwright, capture screenshots, and answer the visual review questions explicitly.
- For MCP work, prove at least one real tool path against the actual central API, not only direct service calls.
- Record commands, screenshots, and outcomes in `reviews/01-execution-report.md`.
- Reopen the subbundle immediately if proof is weak, pending, or contradicted by later work.
