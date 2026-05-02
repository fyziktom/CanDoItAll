# Structured Input

## Objectives

- Provide a generic .NET app delivery capability set for scaffold, restore, build, test, run, and browser proof.
- Keep default process and agent cooperation universal across app, document, spreadsheet, and analysis tasks.
- Add a Blazor specialist agent without moving Blazor-specific rules into base process prompts.
- Replace sample-specific skill text with generic interaction and app-quality rules.
- Validate through real process-run behavior, not by manually fixing the generated app outputs.

## Constraints

- No calculator, converter, unit, or other sample-topic hardcoding in core process or generic skills.
- Technology-specific instructions must live in specialized agents, tools, or skills.
- Blazor guidance must prefer shared component libraries and MCP-backed component discovery when available.
- Generated validation apps must be unrelated random topics and placed under `C:\programovani\dotnet`.

## Validation Expectations

- Source scan proves sample-specific prompt text has been removed or reduced to inert historical test fixture names.
- Unit/integration tests prove seeded tools and agents are available.
- `workspace_dotnet_run` is exposed as a real built-in tool.
- Live web app process runs prove agents can build two apps without manual source repair.
