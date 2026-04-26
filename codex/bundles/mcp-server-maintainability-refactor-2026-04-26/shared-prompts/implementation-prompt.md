# Implementation Prompt

You are executing the MCP Server Maintainability Refactor bundle. Read the root `README.md`, `plan/01-phase-plan.md`, `traceability/01-requirement-traceability.md`, and the current subbundle README before editing code.

## Rules

- Preserve every existing public MCP tool function, request type, response type, route, and startup mode.
- Prefer shared helpers in `CanDoItAll.Mcp.Core` only for behavior that is genuinely common and server-agnostic.
- Keep each server's tool registration explicit in its own project.
- Split large files only around clear responsibilities and keep behavior changes out of mechanical moves.
- Add or update targeted tests where the new helper or split creates a reusable seam.
- Do not touch unrelated package updates, UI styling, or application module behavior.

## Proof

- Run the proof listed in the current subbundle README.
- Record commands, test results, gate decisions, and any residual risk in `reviews/01-execution-report.md`.
- If a public behavior changes unintentionally, reopen the current subbundle instead of hiding it as a residual risk.
