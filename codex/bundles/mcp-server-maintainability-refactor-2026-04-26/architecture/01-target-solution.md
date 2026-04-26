# Target Solution

## Shared Helper Boundary

- Put generic MCP host setup in `CanDoItAll.Mcp.Core`.
- Keep `ModelContextProtocol` server registration in each MCP server project, because tool classes and server-specific SDK wiring belong with the host that owns those tools.
- Shared helpers may cover JSON settings loading, `CanDoItAllMcp_` environment variable loading, stdio logging, backend logging, and validated options registration.
- Shared helpers must not know about CodeAnalytics, Components, Processes, ProjectStructure, SshOps, or DotNetWatch tool classes.

## File Split Boundary

- Component catalog split: keep public `ComponentCatalogService` behavior intact while moving static catalog metadata into a separate partial file or dedicated helper that is easier to review.
- DotNetWatch host split: keep `Program.Main` launch flow intact while moving backend route mapping and replay wrapper code out of the primary host file.
- Avoid cosmetic splits that only shuffle code without improving navigation or testability.

## Testability Boundary

- Add tests for shared host setup helpers so future server additions can reuse the behavior confidently.
- Reuse existing MCP test projects for targeted proof unless creating a new Core test project becomes necessary.
- Tests should assert observable behavior such as configuration source ordering, logging provider setup, options binding, catalog output, or route helper compilation.

## Non-Goals

- Do not alter MCP tool descriptions, names, request schema types, or response envelopes.
- Do not rewrite business logic inside DotNetWatch runtime, SshOps target coordination, or Processes orchestration unless needed to complete a scoped split.
- Do not add new UI validation or frontend behavior.
