# API MCP Cleanup And Skills

This bundle coordinates removal of the ProjectStructure and Processes MCP servers after the API control plane became the preferred development access path.

## Profile

- `initiative`

## Mission

Remove the ProjectStructure and Processes MCP server projects, installers, local config entries, and MCP-specific UI settings. Preserve the useful MCP operating guidance in repo-managed Codex skills for the new project-structure, process, and agent APIs, and close the API gaps found during MCP surface review.

## Bundle Layout

- `inputs/` raw request and normalized task framing
- `analysis/` MCP surface review, cleanup impact, assumptions, and risks
- `requirements/` testable requirements plus user-story workbook
- `architecture/` target cleanup and API skill direction
- `plan/` subbundle execution order and gates
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable execution and QA prompts
- `subbundles/` execution-ready workstreams
- `reviews/` self-review and live execution report

## Recommended Execution Order

1. `subbundles/01-01-mcp-surface-review-and-api-gap-closure`
2. `subbundles/02-02-remove-projectstructure-processes-mcp-code`
3. `subbundles/03-03-reinstall-config-and-settings-ui-cleanup`
4. `subbundles/04-04-api-skills-author-install`
5. `subbundles/05-05-validation-architecture-review-and-closure`

## Validation Summary

- Bundle preparation status: `Prepared and validated`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Documented blocker; app browser proof was not captured, with source/build/integration proof recorded instead`
