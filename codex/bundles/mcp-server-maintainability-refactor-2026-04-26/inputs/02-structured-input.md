# Structured Input

## Objectives

- Reduce repeated MCP host setup code across server `Program.cs` files.
- Split selected long files around stable responsibility boundaries.
- Improve testability of shared setup and refactored helpers.
- Keep public MCP tool names, request/response contracts, and behavior intact.

## Hard Constraints

- Preserve all existing functions and public tool capabilities.
- Keep refactors conservative and local to MCP implementation quality.
- Do not introduce a new framework, package architecture, or UI behavior.
- Do not mix security advisory remediation or unrelated application module work into this bundle.

## Assumptions

- The intended scope is the MCP server family under `src\CanDoItAll.Mcp.*`.
- The first pass should prioritize low-risk structural improvements with targeted tests over a broad behavioral rewrite.
- `CanDoItAll.Mcp.Core` is the correct home for shared MCP infrastructure that is independent of server-specific tool implementations.

## Validation Expectations

- Prepared-bundle validation must pass before implementation.
- Each subbundle must run an entry and closure gate.
- Targeted MCP tests must pass after relevant subbundles.
- A focused build of the affected MCP projects must pass before final closure.
- Browser validation is not required because this is a non-UI/server-side refactor.
