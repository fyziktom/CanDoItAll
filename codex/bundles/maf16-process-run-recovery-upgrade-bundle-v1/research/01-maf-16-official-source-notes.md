# Microsoft Agent Framework 1.6 Official Source Notes

## Package version facts

Official NuGet pages currently show:

- `Microsoft.Agents.AI` version `1.6.2`
- `Microsoft.Agents.AI.OpenAI` version `1.6.2`
- `Microsoft.Agents.AI.Workflows` version `1.6.2`

The current repo references `1.3.0` for the core, OpenAI, and Workflows packages.

## Capabilities relevant to CanDoItAll

Microsoft's Agent Framework docs describe two main capability categories:

- Agents: individual agents that use LLMs, call tools/MCP servers, and generate responses.
- Workflows: graph-based workflows for type-safe multi-step tasks, checkpointing, routing, and human-in-the-loop support.

Docs also describe foundational pieces: model clients, agent sessions, context providers, middleware, MCP clients, and workflows.

## Release notes relevant to upgrade

The GitHub release page for the 1.6 line includes .NET changes such as:

- `IChatMessageInjector` for message injection during function loop.
- Hosted files sample and `AgentSessionFiles`.
- Stream-error input persistence fixes.
- Hosted agents strict URL routing fix.
- Handoff message role mutation fix.
- Workflow evaluation support for expected output / ground truth.
- File store improvements.
- Breaking change: OpenTelemetry agent auto-wires ChatClient with OpenTelemetryChatClient.
- A2A breaking change: migration to A2A SDK v1.0.
- Skills-related breaking changes in the 1.6 line around file skill folder discovery and `SkillFrontmatter`.

## Upgrade implication

Codex must not do a blind package bump. It must inspect compile errors and adapt the CanDoItAll adapter around:

- `ChatClientAgentOptions`
- `AIAgent.RunAsync` / streaming execution
- `AIContextProviders`
- chat history/session persistence
- tool approval mechanics
- function/tool invocation tracing
- finalizer tool capture
- handoff workflow creation
- A2A package/API compatibility
- skills discovery metadata
- OpenTelemetry wrapping
