# Structured Input

## Normalized Notes

| ID | Raw request | Normalized requirement | Owner |
| --- | --- | --- | --- |
| N01 | Provider badge shows 4, list shows 1 | Providers tab must use the same AgentFramework provider catalog as the badge/dashboard count. | SB01 |
| N02 | Add default provider for local ollama | Seed and normalize a local Ollama provider at the local API endpoint without removing the existing remote fallback. | SB01 |
| N03 | Provider list to treeview by tags and add missing tags/UI | Provider profiles must carry tags, merge seeded/default tags, expose `TagEditor`, and render in a tag-grouped `TreeView`. | SB01 |
| N04 | Capabilities tab agent list to treeview | Capability assignment must use `TreeView` for agent selection instead of flat list items. | SB02 |
| N05 | Capability cards compact multi-row desktop grid | Capability inventory must render as a desktop card grid with bounded card content. | SB02 |
| N06 | Search, tags, assigned filter, type filter | Capability detail panel must provide search, `TagEditor`, assignment filter (`All`, `Assigned`, `Not Assigned`), and type filter (`All`, `MCP`, `Skill`, `Tool`). | SB02 |
| N07 | Add MCP or Skill wizard | Capability page must open a wizard for new MCP server or Skill setup using existing step/upload patterns. | SB03 |
| N08 | Use imagegen proposals and ASCII layouts | Wizard implementation must be based on separate image proposals and recorded ASCII layouts. | SB03 |
| N09 | Details dialog for each skill/MCP/tool | Each listed capability must have a details dialog showing full metadata and allowing edits appropriate to the type. | SB02 |
| N10 | MCP detail editing | MCP details must allow editing command/path/arguments and related parameters. | SB02/SB03 |
| N11 | Default tools limited edit but tags editable | Built-in tool capabilities may keep system-managed fields read-only, but tags must be editable. | SB02 |
| N12 | Do not implement `/skills-tag:economy` now | Runtime chat tag shortcut behavior is explicitly out of scope. | SB03 |

## Hard Constraints

- Use existing component wrappers and primitives before adding local markup.
- Use `TreeView`, `TagEditor`, `Steps`, `InputFile`, `ListDetailShell`, and `DialogService`.
- Focus large-screen/desktop layout; smaller screens are not tuned in this bundle.
- Keep Workspace provider settings panel behavior intact.

## Validation Expectations

- Prepared and completed bundle validators.
- Targeted unit/component tests and a build.
- Large-screen browser proof with provider tree, capability grid, details dialogs, and wizard open states.
