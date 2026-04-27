# 07 - Provider Capability Matrix and Runtime Gating

## Objective

Create a single source of truth for provider/model capabilities and use it to gate structured output, tools, approvals, hosted tools, background responses, sessions, vision, and compaction.

## Primary files to inspect


- `src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs`
- `src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- Existing provider health/check services.


## Required implementation tasks


1. Define `ProviderCapabilityProfile` or equivalent with at least:
   - supports streaming
   - supports function tools
   - supports structured output / response format
   - supports tool approval wrappers
   - supports hosted provider-native tools
   - supports hosted MCP
   - supports local MCP bridge
   - supports service-managed history
   - supports background responses
   - supports vision
   - supports compaction or compatible context providers
2. Implement a resolver based on provider kind, transport, model, configured adapter, and installed MAF provider behavior.
3. Replace scattered structured-output decisions such as `model.Transport == Responses` with resolver calls where appropriate.
4. Update managed provider defaults so they reflect the actual capability profile or document why they intentionally disable a feature.
5. Use the capability profile before attaching tools or starting runs.
6. If a requested capability is unsupported, fail early with a clear remediation message.
7. Add UI/API metadata if the current app surfaces provider capabilities.


## Required tests


Unit tests:
- OpenAI Responses provider resolves expected capabilities.
- Azure OpenAI Responses provider resolves expected capabilities.
- Chat-completions provider resolves only capabilities actually supported by installed MAF adapter.
- Ollama/local provider does not receive unsupported hosted tools or structured-output assumptions.
- Unsupported capability request fails before runtime execution.

Integration tests:
- A structured-output process step refuses to run with a provider that cannot enforce structured output unless a validated fallback path is configured.
- Hosted tools attach only for supported providers.


## Risks and constraints


- Some providers may support JSON schema-like behavior without full MAF `ResponseFormat` support. Do not overstate capability; tests and docs must reflect reality.

