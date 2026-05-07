# Structured Input

## Raw Notes

- Remove ProjectStructure MCP and Processes MCP servers from code and install surfaces.
- Preserve MCP tool usage guidance in the bundle and new API skills.
- Close missing API coverage discovered from the MCP tools instead of deleting useful behavior.
- Keep logic unified through existing services and helpers.
- Remove MCP-specific Settings UI.
- Install new skills locally and make them repo-managed for other machines.

## Non-Goals

- Do not remove the application project-structure domain, process domain, agents module, or internal agent tools.
- Do not remove CodeAnalytics, Components, DotNetWatch, SshOps, Playwright, Tailwind, or other remaining MCP integrations.
- Do not edit historical bundle records unless they affect current build/install behavior.
