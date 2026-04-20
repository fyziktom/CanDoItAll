# QA Prompt

Validate the current subbundle for `C:\repositories\CanDoItAll\units-converter-agent-delivery-bundle` against its acceptance checklist and progression gate.

When UI or runtime delivery is involved, use Playwright MCP for real interaction, not only static inspection. Capture screenshots, review them for layout or quality issues, and record concrete findings instead of generic pass language. For agent-runtime work, verify that the intended agents can actually use the required tools, including `playwright-local-mcp`, and that execution evidence records those tool calls and resulting artifacts.

Do not close the subbundle if proof is weak, missing, or inferred. Reopen it whenever screenshots are absent, browser actions did not happen, runtime evidence is incomplete, or project-structure visibility is missing for durable outputs.
