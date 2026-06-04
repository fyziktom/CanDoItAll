# Normalized Requirements

## Requirements

| ID | Requirement | Acceptance signal |
| --- | --- | --- |
| R01 | `/agents?tab=providers` must list the same AgentFramework provider set counted by the shell badge. | Provider count and tree leaf count match `IAgentFrameworkWorkspaceService.ListProvidersAsync()`. |
| R02 | Seeded catalog must include a local Ollama provider. | Clean seed includes `Local Ollama` with local base URL and Ollama tags. |
| R03 | Provider profiles must persist editable tags. | Provider editor exposes `TagEditor`; save/reload preserves tags. |
| R04 | Providers list must be tag-grouped in `TreeView`. | Tag parent nodes show counts; provider child nodes select the editor. |
| R05 | Capabilities panel must use `TreeView` for agent selection. | Agent selection rail renders a `TreeView` and selecting a node changes the selected agent. |
| R06 | Capability inventory must support search, tag, assigned-state, and type filters. | Controls filter the rendered card grid for selected agent assignments and `MCP`/`Skill`/`Tool`. |
| R07 | Capability cards must render multiple per row on desktop with bounded content. | Large viewport shows card grid with no overlap or horizontal overflow. |
| R08 | Capability details dialog must show full metadata and allow allowed edits. | Skill/MCP/tool details open, show JSON/config/details, save tags/name where allowed, and keep built-in tool identity guarded. |
| R09 | MCP details must expose typed parameter fields. | MCP dialog includes command, endpoint/path, arguments, working directory, allowed tools, approval mode, and writes valid configuration JSON. |
| R10 | Capability page must provide new MCP server and new Skill setup wizard. | Wizard launches, advances through steps, saves a new catalog capability, and reloads the inventory. |
| R11 | Wizard UI must be based on imagegen proposals and ASCII layouts. | Architecture file records layouts and implementation follows the same left-step/main/right-review structure. |
| R12 | Chat-time `/skills-tag:*` behavior must not be implemented. | No runtime prompt parser/chat shortcut changes are made. |

## Scope Exceptions

- Smaller mobile layouts are not tuned in this bundle because the request explicitly prioritizes large screen/desktop.
- Live execution of arbitrary new MCP servers is out of scope; the implementation saves and verifies catalog configuration shape.
