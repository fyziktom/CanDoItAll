# DotNet Watch MCP Validation

This folder contains the QA validation output for the `CanDoItAll.Mcp.DotNetWatch` server.

Contents:

- `01-findings.md`: proof-backed implementation gaps between the CodexPack and the real server.
- `02-checklists.md`: repair and validation checklists.
- `03-repair-plan.md`: ordered repair plan.
- `04-implementation-prompts.md`: prompts for implementation agents.

Scope:

- Compared `CanDoItAll.Mcp.DotNetWatch.CodexPack` requirements against the current implementation in `src/CanDoItAll.Mcp.DotNetWatch`.
- Reviewed unit and integration coverage in `tests/CanDoItAll.Mcp.DotNetWatch.Tests` and `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests`.
- Verified selected tool behavior with live MCP tool calls.
