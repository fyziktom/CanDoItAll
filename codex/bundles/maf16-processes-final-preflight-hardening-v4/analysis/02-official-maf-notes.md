# Official MAF Notes To Keep In Mind

The Microsoft Agent Framework overview says the framework has two main capability categories:

- Agents: LLM agents that process inputs, call tools/MCP servers, and generate responses.
- Workflows: graph-based workflows for multi-step tasks with type-safe routing, checkpointing, and human-in-the-loop support.

It also lists foundational pieces such as model clients, agent sessions, context providers, middleware, and MCP clients.

The tools docs say Agent Framework supports function tools, hosted/local MCP tools, browser automation, shell, A2A tools, and tool approval. Tool approval can gate function tools, hosted tools, and MCP calls.

The 1.6 release notes mention important areas that must be tracked:

- `IChatMessageInjector`
- `AgentSessionFiles`
- stream-error input persistence fixes
- A2A v1 migration
- MCP tool call metadata forwarding
- SkillFrontmatter / skills discovery changes
- OpenTelemetry wrapper change
- workflow expected output / ground truth

Codex must update local docs if any of these are unavailable in the local package set or intentionally deferred.
