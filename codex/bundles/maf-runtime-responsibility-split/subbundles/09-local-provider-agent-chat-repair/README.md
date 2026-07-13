# Local Provider Agent Chat Repair

## Status

- `Completed`

## Objective

- Repair the Local Ollama provider path for MAF agent chat and prove local Playwright MCP tools can be discovered and invoked through real agent chat from both API and UI flows.

## Success Criteria

- Agent chat configured with Local Ollama sends requests using the provider-compatible local model instead of a managed-seed OpenAI model.
- Supported/custom local models remain unchanged when they are already provider-compatible.
- Local Playwright MCP setup discovers tools with input schemas.
- Runtime agent chat attaches local Playwright MCP tools and persists real tool receipts for `browser_navigate` and `browser_snapshot`.
- Live UI proof shows the user-facing agents chat can complete a Local Ollama response and a Local Ollama plus Playwright MCP tool run.
- Disposable proof agents are deleted after validation.

## Covered Inputs

- N011-N018
- Requirements R13-R17

## Prerequisites

- SB01-SB08 implemented and proof captured.
- Local app can run at `http://127.0.0.1:5032`.
- Local Ollama provider is configured in the app workspace and can health-check a local model.
- Playwright MCP package can be resolved from local npm cache or installed with npm.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/Seeds/ManagedSeedProviderFallbacks.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs`
- `repo://src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpClientFactory.cs`
- `repo://src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.CatalogDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `repo://Templates/Capabilities/mcps.json`
- `repo://Templates/Capabilities/manifest.json`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ManagedSeedProviderFallbacksTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/CapabilityTemplateSeedMaterializationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`

## Deliverables

- Provider model fallback for managed seed OpenAI model names when the active provider is Local Ollama.
- Cached Playwright MCP launch resolver and newline-delimited JSON framing in the seeded capability descriptor.
- Local stdio MCP runtime path that uses the project-owned `IMcpRuntimeClient`, preserves tool schemas, applies runtime environment, and disposes clients after runs.
- Focused tests for model fallback, capability seed materialization, local MCP descriptor/runtime composition, and Playwright MCP seeding.
- Live app proof artifacts under `proof/SB09/`.

## Dependency Impact

- Agent chat, project-structure chat, capability setup, MCP tool invocation, and UI proof depend on this repair. A weak proof here would allow provider health and workflow success to mask broken agent-chat execution.

## Validation Depth

- `Provider-runtime and browser-tool execution closure`

## Implementation Steps

1. Reproduce and isolate the model mismatch between provider health/workflow execution and MAF agent chat.
2. Add a narrowly scoped fallback from known managed-seed OpenAI models to the Local Ollama provider default.
3. Prove supported/custom local models remain unchanged.
4. Repair Playwright MCP setup/runtime launch and message framing.
5. Preserve MCP input schemas from discovery through `AIFunction` exposure.
6. Route local MCP runtime execution through `IMcpRuntimeClient` for local stdio capabilities.
7. Run focused unit/integration tests.
8. Build the web app and run it locally.
9. Run live API proof for Local Ollama agent chat and Local Ollama plus Playwright MCP tool invocation.
10. Run real browser UI proof for agents chat and project-structure chat with Local Ollama.
11. Run UI-started agent chat that invokes `browser_navigate` and `browser_snapshot`.
12. Capture screenshots, persisted run details, and cleanup proof.
13. Update bundle traceability, proof manifests, execution report, and workbook.

## Scope Exceptions

- Do not redesign provider setup or workflow LLM execution; the reported workflow path already works.
- Do not convert all MCP transports; SB09 is scoped to the local Playwright stdio runtime path used by agent chat.
- Do not hide provider/model errors. The fallback is limited to known managed seed OpenAI defaults on Local Ollama.

## Do Not Do

- Do not satisfy UI proof with API-only runs.
- Do not fake MCP receipts or substitute test tools for live proof.
- Do not leave disposable proof agents in the app workspace.
- Do not use broad catch-all provider fallback logic that silently changes custom model selection.

## Acceptance Checklist

- `ManagedSeedProviderFallbacksTests` covers Local Ollama fallback and preservation cases.
- MCP capability seed tests prove `newlineDelimitedJson`, timeout, and input-schema preservation.
- Runtime composition test proves local MCP uses `IMcpRuntimeClient`, exposes schemas, invokes tools, and disposes the client.
- Web build passes.
- API Local Ollama run detail shows provider `Local Ollama`, model `gemma4-12b-256k`, and expected response marker.
- API Local Ollama plus Playwright MCP run detail shows `browser_navigate` and `browser_snapshot` receipts.
- UI Local Ollama chat screenshot/DOM proof shows expected response marker.
- UI Local Ollama plus Playwright MCP screenshot/DOM proof shows expected response marker and browser tool names.
- Disposable API/UI agents are deleted and cleanup proof shows they no longer exist.

## Proof Required

- `proof/SB09/manifest.md`
- `proof/SB09/semantic-invariants.md`
- Focused build/test transcripts under `proof/SB09/transcripts/`.
- Live API run details:
  - `proof/SB09/api-local-ollama-run-detail.json`
  - `proof/SB09/api-project-structure-local-ollama-run-detail.json`
  - `proof/SB09/api-local-ollama-playwright-mcp-run-detail.json`
- Live capability setup proof:
  - `proof/SB09/live-playwright-capability-after-runtime-repair.json`
  - `proof/SB09/mcp-playwright-setup-test-after-runtime-adapter-repair-full-editor.json`
- Live browser proof:
  - `proof/SB09/browser-ui-local-ollama-chat-run-detail.json`
  - `proof/SB09/browser-ui-local-ollama-playwright-mcp-run-detail.json`
  - `proof/SB09/browser-ui-local-ollama-playwright-mcp-completed.json`
  - `proof/SB09/screenshots/browser-ui-local-ollama-playwright-mcp-completed.png`
- Cleanup proof:
  - `proof/SB09/temp-local-ollama-playwright-agent-cleanup.json`
  - `proof/SB09/temp-ui-local-ollama-playwright-agent-cleanup.json`
- Changed-file hashes and anti-stub audit.

## Browser Validation Logging

- Route `/agents?tab=chat&agentId={tempLocalAgent}`.
- Large desktop viewport first.
- Required UI actions: select the disposable Local Ollama agent, send a simple response-marker prompt, then send a prompt that explicitly asks for `browser_navigate` and `browser_snapshot`.
- Required assertions: expected response markers visible, persisted run details show Local Ollama and `gemma4-12b-256k`, MCP tool receipts include `browser_navigate` and `browser_snapshot`, and screenshot contains the completed thread state.
- Required screenshot: `proof/SB09/screenshots/browser-ui-local-ollama-playwright-mcp-completed.png`.
- Review question: does the visible UI show a real completed chat/tool run rather than only a route load or setup page?

## Progression Gate

- SB09 closes only when focused automated tests, live API proof, live UI proof, MCP tool receipts, cleanup proof, workbook regeneration, and final bundle validation all pass.

## Suggested Agent Prompt

```text
Implement SB09 only. Diagnose the Local Ollama agent-chat path against provider health/workflow behavior, repair model resolution and local Playwright MCP runtime launch, run focused tests, then prove through real API and UI chat runs with persisted run details and screenshots. Do not fake provider or MCP proof.
```
