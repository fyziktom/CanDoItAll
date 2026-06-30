# Scope Inventory

## Capability Surfaces

| Area | Files | Migration concern |
| --- | --- | --- |
| MAF composition | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities*.cs` | Extract builders/config DTOs into dedicated projects, keep MAF as adapter. |
| Tool policy | `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`, `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs` | Preserve names and policy behavior while moving declarations to templates/typed registry. |
| Capability proof | `repo://src/CanDoItAll.AgentFramework.Core/Capabilities/CapabilityProofService*.cs` | Replace structural proof with dedicated setup services and live test results. |
| Seed catalog | `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs` | Replace hardcoded definitions with template materialization. |
| Seed assets | `repo://src/CanDoItAll.AgentFramework.Persistence/SeedAssets` | Move skill/tool/MCP metadata to `Templates/Capabilities`. |
| Agent templates | `repo://Templates/Agents` | Preserve capability key assignments. |
| Processes/workflows | `repo://Templates/Processes`, `repo://Templates/Workflows` | Regression proof must show process/workflow tool usage still functions. |
| UI setup | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor.cs` | Add Tool setup and MCP list-tools test path. |
| API | `repo://src/CanDoItAll.Web/Api/AgentsApi.cs` | Add setup/test endpoints without overloading generic verify. |

## Existing Test Surfaces

| Test layer | Current examples | Required expansion |
| --- | --- | --- |
| Unit | `MafAgentRuntimeToolProviderCompositionTests`, `AgentToolInvocationPolicyTests`, `BrowserMcpArtifactPathServiceTests` | Add loader, schema, invoker, lifecycle, setup-test, and adapter tests with mocks. |
| Integration | `AgentFrameworkWorkspaceSeedIntegrationTests`, `AgentFrameworkExecutionCapabilityFilteringIntegrationTests` | Add template-backed seed tests and runtime composition through new adapters. |
| Component | `tests/CanDoItAll.Tests.Components` | Add setup wizard/editor tests for Tool, MCP list-tools result, and validation states. |
| E2E | `tests/CanDoItAll.Tests.Playwright` | Add capability setup flows, test MCP server, external tool dry run, and existing process/workflow smoke. |

## Compatibility Inventory

- Existing capability keys: preserve all keys created from `CreateStableGuid("capabilities/...")`.
- Existing runtime tool names: preserve all names from `ToolContractCatalog.KnownToolNames` and seed builder `CreateToolCapability`.
- Existing MCP capability keys: preserve `playwright-local-mcp`, `candoitall-codeanalytics-mcp`, and `candoitall-components-mcp`.
- Existing file skill keys: preserve current skill keys assigned by agent `skills.json`.
- Existing inline skill names: preserve endpoint identity and managed seed migration semantics until template migration proves parity.
