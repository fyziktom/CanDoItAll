# Structured Input

## Objectives

- Repair stale repository documentation so it matches the current .NET 10 Blazor, modular service, process runtime, AgentFramework, persistence, MCP, and component-library architecture.
- Add a detailed architecture-beta document with Mermaid `architecture-beta`, C4, and sequence diagrams.
- Explain process execution with AI agents in enough detail to show how process runs, step dependencies, CRM/HR AI parties, AgentFramework technical agents, tool execution, recovery, and artifact projection fit together.
- Improve the root README with a clear architecture overview and an overview diagram of CanDoItAll parts.
- Ensure every tracked project or library directory under `src`, `tests`, and `tools` has a README.

## Hard Constraints

- Use the requested `candoitall-bundle-workflow` skill and keep bundle proof synchronized.
- Do not invent architecture. Documentation must cite or reflect actual code paths.
- Keep changes to docs and project README files; do not change product behavior.
- Keep Markdown ASCII-only unless existing file content requires otherwise.
- Do not generate XML documentation comments.

## Source Artifacts

- See `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- `N001`: out-of-date docs.
- `N002`: repair all docs to match actual architecture.
- `N003`: add architecture-beta, C4, and sequence diagrams.
- `N004`: explain running processes with AI agents.
- `N005`: improve README and include an overview diagram of CanDoItAll parts.
- `N006`: ensure README coverage for each project/library.

## Dependency And Sequencing Signals

- Architecture inventory must finish before architecture docs are rewritten.
- The detailed architecture page must exist before the root README can link to it.
- Project README generation should happen after the project inventory is validated.

## Validation Expectations

- Bundle readiness gate passes before docs execution.
- New architecture document contains at least one `architecture-beta` block, C4 diagrams, and sequence diagrams.
- Root README includes a current overview and links to the detailed architecture doc.
- Project README coverage script reports no missing README for tracked `src`, `tests`, or `tools` `.csproj` directories.
- Final execution report maps the raw user request to changed files and proof.

## UI Validation Strategy

- N/A. Markdown documentation changes do not affect runtime UI behavior.

## Browser Validation Analytics

- N/A. Browser proof is not required for docs-only changes.

## Working Assumptions

- "architecture-beta" means a new detailed Markdown architecture page, not a product branch or build configuration.
- "sequential diagrams" means sequence diagrams.
- "all readmes for each project and library" covers all tracked `.csproj` directories under `src`, `tests`, and `tools`, including projects not listed in `CanDoItAll.slnx`.
- Build/test validation for a docs-only change can be limited to lightweight file/readme checks unless markdown tooling or link checks reveal a stronger need.

## Primary Risks

- Summarizing the process/agent runtime too loosely would create new stale architecture docs.
- Generated project READMEs must stay concise enough not to overclaim project behavior.
- Mermaid dialects are renderer-dependent; validation must at least prove that the requested diagram block types are present.
