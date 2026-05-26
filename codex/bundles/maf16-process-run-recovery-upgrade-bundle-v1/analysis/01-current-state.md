# Current Repo State

## MAF packages

`src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` currently references:

```xml
<PackageReference Include="Microsoft.Agents.AI" Version="1.3.0" />
<PackageReference Include="Microsoft.Agents.AI.A2A" Version="1.3.0-preview.260423.1" />
<PackageReference Include="Microsoft.Agents.AI.Mem0" Version="1.0.0-preview.251028.1" />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.3.0" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.3.0" />
```

## Current MAF adapter hotspots

The current adapter uses:

- `Microsoft.Agents.AI`
- `ChatClientAgentOptions`
- `AIAgent`
- `AsAIAgent`
- `AIContextProviders`
- `ChatHistoryProvider`
- `RequirePerServiceCallChatHistoryPersistence`
- custom finalizer capture tools
- custom tool invocation tracing
- handoff workflow factory
- process tools
- local workspace tool access and policy
- OpenAI Chat Completions and Responses transport paths
- Ollama adapter path

These must be considered public seams during the upgrade.
