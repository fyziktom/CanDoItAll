# Naming And Compatibility Standards

## External Standards Read

- MCP draft Tools specification says tools are uniquely identified by name, support `tools/list` and `tools/call`, and should use 1-128 case-sensitive names composed of ASCII letters, digits, underscore, hyphen, and dot, without spaces or special characters.
- OpenAI function calling documentation models tools as callable functions with `name`, `description`, JSON Schema `parameters`, and optional strict schema enforcement.
- OpenAI Codex skill documentation defines a skill as a directory with a required `SKILL.md`, and `SKILL.md` must include `name` and `description`.
- Anthropic tool-use documentation separates client tools from server tools, expects tool schemas, and supports strict mode for schema conformance.

## Project Convention

| Concept | Convention | Rationale |
| --- | --- | --- |
| Capability key | lower kebab-case, for example `workspace-dotnet-build` | Existing agent templates already assign kebab-case capability keys. Preserve them. |
| Runtime tool/function name | lower snake_case, for example `workspace_dotnet_build` | Existing runtime tools use snake_case and this is compatible with MCP/OpenAI/Anthropic examples. |
| MCP server key | lower kebab-case, for example `playwright-local-mcp` | Server/catalog identity is a template key, not a runtime function name. |
| MCP tool name | preserve server-provided name, but validate against MCP-compatible ASCII set | MCP names may contain `_`, `-`, or `.`, and clients must handle collisions. |
| Skill key/folder | lower kebab-case, for example `aspnet-core-skill` or `aspnet-core` | Aligns with existing skills, Codex explicit `$skill` invocation, and filesystem readability. |
| C# identifiers | strongly typed IDs/constants, no open string switches | Prevents future drift between templates, policies, UI, and runtime wiring. |
| Template schema IDs | stable explicit IDs with version fields | Avoids accidental ID churn when descriptions or display names change. |

## Compatibility Rules

- Do not rename existing runtime tool names without a versioned compatibility alias and migration note.
- Do not rename existing capability keys referenced by `Templates/Agents/**/skills.json`.
- Do not infer side-effect or approval behavior from name prefixes alone in new code; templates should declare it and validators should enforce it.
- Keep name normalization one-way and explicit: kebab-case capability key to snake_case runtime tool name may be declared in template, not guessed by replacing characters at runtime.
- External command/http tool names must still be declared as runtime tool names with JSON schemas and setup tests.
