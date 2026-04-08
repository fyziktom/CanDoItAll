# Target Solution

## End State

- `CanDoItAll.Mcp.CodeAnalytics` stays a thin host wrapper over sibling application services.
- The sibling CodeAnalytics application surface gains explicit queries for the missing SharpTools-style analysis questions that the Zyphonote benchmark exposed.
- The host MCP exposes those queries as first-class MCP tools with the existing `code_analytics_` naming convention and envelope semantics.
- The repo skill pack documents when to start with direct project navigation, when to use symbol/document inspection, and when focused context is still the right higher-level tool.

## Boundary Rules

- Keep core analysis logic in `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.*`.
- Keep MCP transport, settings, and envelope concerns in `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics`.
- Extend reinstall, Codex config, and skill-pack docs only in the host repo.
- Prefer snapshot-backed query surfaces over ad hoc host-only parsing unless a missing capability cannot be answered from snapshot facts plus workspace source access.

## Planned Tool Additions

- A direct project and solution inventory surface for clean project-reference questions.
- A document and source inspection surface that reduces the need to fall back to raw shell file reads.
- A stable method-behavior path, either by fixing focused context for member seeds or by adding a deterministic behavior-oriented inspection tool.

## Non-Goals

- Do not build a general-purpose editing MCP to replace SharpTools mutator operations in this pass.
- Do not fork or copy host runtime support into the sibling repo.
- Do not weaken the rerun by changing the Zyphonote scenarios or answer keys.
