# MCP DotNetWatch Backend Improvements 1

This package captures the second improvement pass for the persistent `CanDoItAll.Mcp.DotNetWatch` backend.

Scope:
- Global backend visibility in the manager UI.
- Basic manager controls for live sessions and build/rebuild actions.
- Verified non-interactive `dotnet watch` rude-edit handling.
- Agent-oriented log reduction that preserves diagnostic value while shrinking context usage.
- Quantified savings analysis after implementation.
- Validation guidance for browser-side static asset caching and MCP self-build shadow artifacts.

Files:
- `01-request-clarified.md`
- `02-gap-analysis.md`
- `03-architecture-improvements.md`
- `04-implementation-plan.md`
- `05-checklists.md`
- `06-agent-prompts.md`
- `07-validation-rules.md`
- `08-measurement-method.md`
- `09-validation-evidence.md`

Execution status:
- Implemented in `src/CanDoItAll.Mcp.DotNetWatch`.
- Validated live against `CanDoItAll` and `pveinvoicing`.
- Aggregate manager screenshot: `backend-manager-aggregate.png`.
