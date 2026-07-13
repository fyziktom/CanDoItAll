# QA Prompt

Write an outcome-first reusable QA prompt. Include coverage checks, dependency gates, proof review, browser or host validation when applicable, raw-note closure, and blocker handling.
# QA Prompt

Validate the repaired MCP setup path and adjacent agent tooling:

1. Confirm the setup API host resolves the live MCP setup runtime.
2. Confirm Playwright Local MCP configuration persists `messageFraming: newlineDelimitedJson`.
3. Confirm the Agents capability dialog setup test shows `Setup passed`.
4. Confirm representative delivery agents have project-structure/process access metadata.
5. Confirm runtime-provider filtering keeps process-step tools scoped to the operation contract.
6. Confirm `/projects`, `/agents/workflows`, and `/processes` load at `1920x1080` with no Blazor error banner.
