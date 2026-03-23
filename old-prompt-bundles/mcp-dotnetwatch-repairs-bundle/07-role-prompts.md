# Role Prompts

## Senior QA Inspector Prompt

Review the MCP DotNetWatch repair package as a reliability contract, not a feature wish list. Challenge anything that still depends on manual memory, hides bootstrap failures, or leaves Codex and Playwright on different startup paths. The package is acceptable only if it turns `Transport closed` from a vague symptom into a diagnosable and repairable workflow.

## Senior C# And MCP Implementation Prompt

Implement the repair bundle with the smallest defensible change set that materially improves reliability. Prioritize:
- self-repairing wrapper startup
- persistent bootstrap diagnostics
- wrapper-path regression coverage
- explicit config policy that transport failure triggers repair work

Do not introduce fallback paths that silently bypass the MCP server. If repair cannot succeed, fail with strong evidence.

## Validation Prompt

Validate the repaired MCP system the same way Codex uses it:
- wrapper-based stdio launch
- live backend registration
- `workspace_info`
- managed app lifecycle readiness
- compatibility with Playwright-driven browser validation

Capture exact failures and exact evidence, not summaries from memory.
