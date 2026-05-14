# Current State

## Source-Grounded Findings

- `CanDoItAll.slnx` and tracked project files no longer include `CanDoItAll.Mcp.Processes` or `CanDoItAll.Mcp.ProjectStructure`.
- Active tracked MCP projects are `CodeAnalytics`, `Components`, `DotNetWatch`, `LocalRuntime`, `Mermaid`, and `SshOps`.
- `README.md`, `docs/README.md`, `docs/processes-mcp-setup.md`, `docs/project-structure-mcp-setup.md`, and `docs/architecture-beta.md` still present Processes and ProjectStructure MCP paths as active.
- Repo-managed API skills already state the new direction: use HTTP APIs instead of removed `candoitall_processes` and `candoitall_projectstructure` MCP servers.
- `src/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs` maps `/api`, `/api/access/status`, `/api/access/tokens`, projects, processes, and agents when API access is enabled.
- `src/CanDoItAll.Web/ProjectStructureAgentApi.cs` maps `/api/project-structure` as the current project-structure agent API surface.
- `docs/architecture-beta.md` starts with a Mermaid `architecture-beta` block using service label syntax that fails in GitHub and mermaid.live. Replacing that block with ordinary `flowchart` syntax is the smallest reliable correction.
- The docs have strong technical fragments but lack a customer-facing explanation of CanDoItAll as an operating system for projects.

## Documentation Gap

- Technical docs need an API control-plane page, clearer development guidance, and updated architecture boundaries.
- Less-technical docs need a wiki-style orientation, a process operating model explanation, and enterprise-specific infographic assets.
- Existing setup docs for removed MCPs should not disappear silently because external links may exist; replacing them with retired/suppressed transition pages preserves discoverability while preventing wrong setup work.
