# Implementation Prompt

Implement the active subbundle only. Keep changes scoped to workflow API command parity, the workflow API skill, install sync proof, and bundle status updates.

Hard constraints:

- Use typed workflow DTOs and existing services.
- Do not introduce generic string command dispatch.
- Do not create a workflow MCP server.
- Keep skill docs concise and aligned with existing API skills.
- Record proof in `reviews/01-execution-report.md` before closing a subbundle.

Stop conditions:

- Stop and repair the bundle if the API review reveals a larger workflow domain gap than lifecycle/import/export.
- Stop and record a blocker if the reinstall script cannot sync local skills.
