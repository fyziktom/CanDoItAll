# A2A Agent Registry And Hosting

## Status

- `Completed`

## Objective

Add typed A2A endpoint/catalog support so CanDoItAll can call remote A2A agents and explicitly publish selected local agents through A2A hosting.

## Covered Inputs

- `NOTE-03`
- `NOTE-09`
- `REQ-03`
- `REQ-04`
- `REQ-05`

## Prerequisites

- Subbundle 01 build state is known.
- Preview A2A package versions are documented.
- Architecture target says preview SDK concrete types stay behind adapter boundaries.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.AgentFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`
- `C:\repositories\agent-framework\dotnet\samples\02-agents\A2A\A2AAgent_AsFunctionTools\Program.cs`
- `C:\repositories\agent-framework\dotnet\samples\05-end-to-end\A2AClientServer\A2AServer\Program.cs`
- `C:\repositories\agent-framework\dotnet\samples\05-end-to-end\A2AClientServer\A2AServer\HostAgentFactory.cs`

## Deliverables

- CanDoItAll model types for A2A remote endpoints, protocol binding preference, auth reference, agent-card metadata, and skill-tool exposure.
- A MAF adapter service that resolves A2A cards and creates `AIAgent` instances with explicit error handling.
- Optional skill-as-function tool creation using sanitized names and agent-card skill metadata.
- Hosting registration path for explicitly published local agents and well-known agent cards.
- Tests for serialization, validation, disabled endpoints, and invalid URL/auth cases.

## Dependency Impact

- Handoff runtime can include remote A2A agents only after this subbundle creates the adapter boundary.
- Process integration must not directly depend on preview A2A SDK types.

## Validation Depth

- Critical foundation.
- Unit tests for model validation plus adapter tests with local stubs/mocks.

## Implementation Steps

1. Add typed model/configuration records without A2A SDK references in Core contracts.
2. Add Maf adapter code that uses `A2ACardResolver`, `AgentCard.AsAIAgent`, or direct `A2AClient` creation.
3. Add function-tool wrapping for A2A skills only when enabled.
4. Add hosting registration hooks in the Hosting project, disabled by default.
5. Add tests for settings round-trip and adapter failure modes.

## Scope Exceptions

- Do not build a public A2A marketplace UI in this phase.
- Do not persist raw bearer/API tokens in agent configuration JSON.

## Do Not Do

- Do not expose every local agent over A2A by default.
- Do not retry remote A2A calls indefinitely.
- Do not treat remote agent cards as trusted instructions; they are metadata only.

## Acceptance Checklist

- Remote A2A endpoint configuration is typed and validated.
- Disabled endpoints do not attach tools or agents.
- Invalid remote discovery fails with actionable diagnostics.
- Local A2A hosting is opt-in and endpoint paths are explicit.
- Preview SDK types are isolated to adapter/hosting projects.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "AgentA2AMetadataTests|A2ARemoteAgentToolFactoryTests|AgentA2AHostCardFactoryTests" --no-restore -m:1`: passed; 9 tests.
- `dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore -m:1`: passed with existing NU1902/NU1904 warnings.
- `dotnet build src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj --no-restore -m:1`: passed with existing NU1902/NU1904 warnings.

## Browser Validation Logging

- N/A unless an A2A configuration UI is added.

## Progression Gate

- Handoff subbundle may use remote A2A agents only after adapter isolation and failure-mode tests are complete.

## Suggested Agent Prompt

```text
Implement subbundle 03 only: add typed A2A endpoint/hosting support and the MAF adapter boundary. Keep preview A2A SDK types out of Core and persistence contracts.
```
