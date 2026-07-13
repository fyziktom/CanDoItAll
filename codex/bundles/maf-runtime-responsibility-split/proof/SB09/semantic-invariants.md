# SB09 Semantic Invariants

## SB09-I01 Local Provider Model Resolution

- Invariant ID: SB09-I01
- Source raw note: N012, N014, N015, and N016 report Local Ollama agent chat not sending/loading the local model while the agent is configured for Local Ollama.
- Expected behavior: An agent configured for Local Ollama that still carries a known managed-seed OpenAI model name resolves the runtime chat model to the Local Ollama provider default.
- Disallowed shallow implementation: Always ignore `agent.Model`, or always replace any unknown/custom local model with the provider default.
- Failing-first test: Explicit process/non-production exemption; the failure was a pre-repair live GPU/Ollama state reported by the user and was not recreated by reverting source. The preserved report is the failing-first evidence.
- Passing test: `proof/SB09/transcripts/focused-unit-tests.txt` and `proof/SB09/transcripts/live-proof-assertions.txt`.
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/Seeds/ManagedSeedProviderFallbacks.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`.
- Production assertions: `proof/SB09/api-local-ollama-run-detail.json`, `proof/SB09/api-project-structure-local-ollama-run-detail.json`, `proof/SB09/browser-ui-local-ollama-chat-run-detail.json`.
- Red-team negative case: Supported/custom local model names must not be overwritten; covered by `ManagedSeedProviderFallbacksTests`.
- Downstream dependency check: Agent chat, project-structure chat, and workflow provider behavior remain separated; only agent-chat runtime model resolution changed.

## SB09-I02 Local Playwright MCP Runtime

- Invariant ID: SB09-I02
- Source raw note: N018 requires Playwright MCP to work through UI chat and forbids fake tests.
- Expected behavior: Local Playwright MCP tools are discovered, preserve input schemas, attach to MAF agent chat, invoke browser tools, and persist receipts.
- Disallowed shallow implementation: Pass only setup discovery, expose schema-less fake tools, or record synthetic tool receipts without invoking local MCP.
- Failing-first test: Explicit process/non-production exemption; runtime MCP hang was diagnosed during live repair, and the final proof uses real runtime receipts instead of reverting to recreate the hang.
- Passing test: `proof/SB09/transcripts/focused-unit-tests.txt`, `proof/SB09/transcripts/focused-integration-tests.txt`, and `proof/SB09/transcripts/live-proof-assertions.txt`.
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs`, `repo://src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpClientFactory.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`, `repo://src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs`.
- Production assertions: `proof/SB09/mcp-playwright-setup-test-after-runtime-adapter-repair-full-editor.json`, `proof/SB09/api-local-ollama-playwright-mcp-run-detail.json`, `proof/SB09/browser-ui-local-ollama-playwright-mcp-run-detail.json`.
- Red-team negative case: Setup-only success without runtime `browser_navigate` and `browser_snapshot` receipts does not satisfy SB09; live proof asserts the receipts.
- Downstream dependency check: UI-capable agents can use browser tools through local MCP without depending on `ModelContextProtocol` stdio framing.

## SB09-I03 Capability Descriptor Policy

- Invariant ID: SB09-I03
- Source raw note: N018 requires MCP tools to be usable in real agent chat.
- Expected behavior: The seeded Playwright capability carries newline-delimited JSON framing and a bounded timeout into runtime descriptors.
- Disallowed shallow implementation: Change only the template file without proving seeded/live capability and runtime descriptor behavior.
- Failing-first test: Explicit process/non-production exemption; the old capability/runtime combination hung in live repair before final changes.
- Passing test: `proof/SB09/transcripts/focused-integration-tests.txt` and `proof/SB09/transcripts/live-proof-assertions.txt`.
- Changed source files: `repo://Templates/Capabilities/mcps.json`, `repo://Templates/Capabilities/manifest.json`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.CatalogDescriptors.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`.
- Production assertions: `proof/SB09/live-playwright-capability-after-runtime-repair.json`.
- Red-team negative case: A descriptor missing `newlineDelimitedJson`, timeout, or browser allowed tools fails `live-proof-assertions.txt`.
- Downstream dependency check: Capability setup and runtime execution use the same framing/timeout contract.

## SB09-I04 Real UI Proof And Cleanup

- Invariant ID: SB09-I04
- Source raw note: N018 says to truly test UI chat with agents and tools, not fake tests.
- Expected behavior: UI-started Local Ollama chat and UI-started Local Ollama plus Playwright MCP chat complete in the real app and disposable proof agents are cleaned up.
- Disallowed shallow implementation: API-only proof, screenshots without persisted run details, or leaving disposable agents in the workspace.
- Failing-first test: Explicit process/non-production exemption; the live UI proof validates the repaired behavior without replaying the pre-repair failure.
- Passing test: `proof/SB09/transcripts/live-proof-assertions.txt`.
- Changed source files: Same as SB09-I01 through SB09-I03.
- Production assertions: `proof/SB09/browser-ui-local-ollama-playwright-mcp-completed.json`, `proof/SB09/screenshots/browser-ui-local-ollama-playwright-mcp-completed.png`, `proof/SB09/temp-local-ollama-playwright-agent-cleanup.json`, `proof/SB09/temp-ui-local-ollama-playwright-agent-cleanup.json`.
- Red-team negative case: A completed screenshot without `browser_navigate`, `browser_snapshot`, and `MCP-UI-LOCAL-OK` text fails the proof assertion.
- Downstream dependency check: User-facing agents chat and contextual project-structure chat are both covered by live artifacts.

## Production Behavior Artifact Matrix

| Invariant ID | Producer | Consumer | Lifecycle Proof | Negative/Red-Team Proof |
| --- | --- | --- | --- | --- |
| SB09-I01 | `MafModelParametersBuilder` via `ManagedSeedProviderFallbacks.ResolveModel` | MAF agent chat execution | `proof/SB09/api-local-ollama-run-detail.json` and `proof/SB09/browser-ui-local-ollama-chat-run-detail.json` | `ManagedSeedProviderFallbacksTests` preserves supported/custom local models. |
| SB09-I02 | `LocalStdioMcpClientFactory` and MAF local MCP runtime wrapper | MAF agent chat tool list/invocation | `proof/SB09/api-local-ollama-playwright-mcp-run-detail.json` and `proof/SB09/browser-ui-local-ollama-playwright-mcp-run-detail.json` | `MafAgentRuntimeToolProviderCompositionTests.Local_mcp_capability_uses_runtime_client_factory_and_exposes_invocable_schema_tools`. |
| SB09-I03 | Capability seed/materialization and catalog descriptor builder | Capability setup test and runtime descriptor creation | `proof/SB09/live-playwright-capability-after-runtime-repair.json` | `proof/SB09/transcripts/live-proof-assertions.txt` validates framing, timeout, and allowed tools. |
| SB09-I04 | Real browser UI chat flow | Persisted execution details and visible thread | `proof/SB09/browser-ui-local-ollama-playwright-mcp-completed.json` and screenshot | Cleanup proof verifies disposable agents no longer exist. |
