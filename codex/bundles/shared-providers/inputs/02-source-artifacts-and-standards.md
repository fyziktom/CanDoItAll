# Source artifacts and standards

## CanDoItAll source anchors

The preparation review read the current implementation around these anchors:

- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Common/Enums.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderCapabilityContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderRequestContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Registration/AgentProviderDriverRegistry.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Runtime/ProviderRuntimeContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderAgentFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor`
- `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceAgentProviderProfileMapper.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderMetadata.cs`
- `src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs`
- `src/App/CanDoItAll.Web/Api/ApiAuthorizationPolicies.cs`
- `src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccessScopeNames.cs`
- `src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs`
- `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations`
- `docs/testing.md`
- `compose.yaml`

## SharedInfo source anchors

- `codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `codex/skills/bundles/candoitall-bundle-execution/SKILL.md`
- `codex/skills/bundles/candoitall-bundle-validator/SKILL.md`
- `codex/skills/bundles/candoitall-csharp-architecture-bundle-guard/SKILL.md`
- `codex/skills/csharp-architecture-governor/SKILL.md`
- `codex/skills/csharp-architecture-review-gate/SKILL.md`
- `codex/skills/csharp-dependency-graph-audit/SKILL.md`
- `codex/skills/csharp-provider-tool-plugin-isolation/SKILL.md`
- `codex/skills/_csharp-architecture-shared/references/bundle-architecture-sections.md`
- `codex/skills/_candoitall-api-shared/README.md`
- `codex/skills/_candoitall-api-shared/manifest.json`
- `codex/skills/candoitall-api-memory-providers/SKILL.md`

## Primary external standards

Codex must verify the current versions before freezing implementation:

- OpenAI Responses API:
  `https://platform.openai.com/docs/api-reference/responses`
- OpenAI Chat Completions API:
  `https://platform.openai.com/docs/api-reference/chat`
- OpenAI Models API:
  `https://platform.openai.com/docs/api-reference/models/list`
- OpenAI Images API:
  `https://platform.openai.com/docs/api-reference/images/create`
- Ollama OpenAI compatibility:
  `https://docs.ollama.com/openai`
- W3C Trace Context:
  `https://www.w3.org/TR/trace-context/`
- W3C Baggage:
  `https://www.w3.org/TR/baggage/`
- RFC 9457 Problem Details:
  `https://www.rfc-editor.org/rfc/rfc9457`
- RFC 6648 deprecation of `X-` header prefixes:
  `https://www.rfc-editor.org/rfc/rfc6648`
- RFC 9110 conditional requests and ETags:
  `https://www.rfc-editor.org/rfc/rfc9110`

## Interpretation guard

Standards inform compatibility; they do not justify exposing every upstream feature. The
implemented compatibility claim is the intersection of:

1. the public route and wire contract;
2. the central relay adapter;
3. the selected upstream connector;
4. the publication capability snapshot;
5. passing positive and meaningful negative contract tests.
