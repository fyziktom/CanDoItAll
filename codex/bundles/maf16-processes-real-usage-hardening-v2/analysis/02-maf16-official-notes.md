# MAF 1.6 Official Notes To Apply

Use official Microsoft/NuGet sources during execution and update this file with exact references.

Known relevant 1.6-line facts:

- NuGet shows `Microsoft.Agents.AI` version `1.6.2`.
- NuGet shows `Microsoft.Agents.AI.OpenAI` version `1.6.2`.
- NuGet shows `Microsoft.Agents.AI.Workflows` version `1.6.2`.
- Release notes mention `IChatMessageInjector` for message injection during the function loop.
- Release notes mention hosted files and `AgentSessionFiles`.
- Release notes mention stream-error input persistence fixes.
- Release notes mention a handoff message role mutation fix.
- Release notes mention workflow evaluation expected output / ground truth support.
- Release notes mention file store improvements.
- Release notes mention a breaking OpenTelemetry wrapper change.
- Release notes mention A2A v1.0 migration.
- Tools documentation says tool approval can gate function tools, hosted tools, and MCP tool calls.
- Tools documentation distinguishes provider support for function tools, hosted MCP, local MCP, browser automation, shell, and A2A.

Codex must translate these into CanDoItAll design decisions, not just record them.
