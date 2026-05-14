# Target Solution

## Documentation Shape

- Keep `README.md` concise and current: architecture overview, docs entry points, API skills, current MCP sidecars, test commands.
- Use `docs/architecture-beta.md` as the technical architecture page, but replace the fragile `architecture-beta` diagram with ordinary `flowchart`.
- Add `docs/api-control-plane.md` for current HTTP API access, route families, authentication, development workflow, and validation.
- Add `docs/enterprise-operating-system.md` for the customer-facing "Operating System for Projects" story and infographic references.
- Replace stale `docs/processes-mcp-setup.md` and `docs/project-structure-mcp-setup.md` with retired/suppressed transition notes that point to the API pages and API skills.
- Store generated customer documentation images in `docs/images`.

## Boundary Rules

- Product semantics remain in modules and application services.
- HTTP APIs and remaining MCP servers are adapters over the product/runtime semantics.
- Removed Processes and ProjectStructure MCP docs should not describe install commands as active.
- Economy ledger content is directional context unless source exists in this repo.

## Diagram Rules

- Use `flowchart`, `sequenceDiagram`, or simple C4 blocks already present in docs.
- Avoid Mermaid `architecture-beta` until GitHub renders the required syntax reliably.
- Do not put Markdown links, raw HTML, or complex punctuation inside Mermaid labels.
- Prefer short quoted labels and keep explanatory links in normal Markdown text outside the diagram.
