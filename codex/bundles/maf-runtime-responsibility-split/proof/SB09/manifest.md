# SB09 Proof Manifest

## Status

- Completed.

## Root Cause

- Seeded agents could keep a managed OpenAI model name such as `gpt-5.4-mini` after their provider profile was switched to Local Ollama.
- Provider setup health checks and workflow LLM calls used the provider default model, so they loaded Ollama correctly.
- MAF agent chat resolved the agent model directly, so Local Ollama chat could attempt the managed-seed model instead of `gemma4-12b-256k`; the model was not loaded and the UI appeared idle.
- Local Playwright MCP setup/runtime also needed aligned launch/framing. Setup discovery could succeed while runtime agent chat hung or lost schemas if the runtime used the generic `ModelContextProtocol` stdio path instead of the project-owned local MCP runtime client.

## Implementation Proof

- Provider model fallback:
  - `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/Seeds/ManagedSeedProviderFallbacks.cs`
  - `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`
- Local Playwright MCP launch/framing/runtime:
  - `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs`
  - `repo://src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpClientFactory.cs`
  - `repo://src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs`
  - `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
  - `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.CatalogDescriptors.cs`
  - `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
  - `repo://Templates/Capabilities/mcps.json`
  - `repo://Templates/Capabilities/manifest.json`

## Build And Test Proof

- Passing transcript: `proof/SB09/transcripts/focused-unit-tests.txt`, `proof/SB09/transcripts/focused-integration-tests.txt`, and `proof/SB09/transcripts/live-proof-assertions.txt`.
- Failing-first proof: N/A - explicit process/non-production exemption. The failure was reported from a local GPU/Ollama environment before this repair and was not safely replayed as a destructive pre-repair app state. The preserved raw report plus post-repair live API/UI proof closes the regression without reverting source to recreate the failure.
- `proof/SB09/transcripts/mcp-runtime-build.txt` - passed; 3 NU1900 advisory-source warnings, 0 errors.
- `proof/SB09/transcripts/maf-runtime-build.txt` - passed; 20 NU1900 advisory-source warnings, 0 errors.
- `proof/SB09/transcripts/web-build.txt` - passed; 42 warnings, 0 errors. Warnings are NuGet advisory-source warnings plus the existing `Microsoft.OpenApi` NU1903 advisory.
- `proof/SB09/transcripts/focused-unit-tests.txt` - passed; 51 passed, 0 failed, 0 skipped.
- `proof/SB09/transcripts/focused-integration-tests.txt` - passed; 1 passed, 0 failed, 0 skipped.
- `proof/SB09/transcripts/source-assertions.txt` - source assertions find the model fallback, MCP framing, timeout, schema, local runtime client, and local launch code paths.
- `proof/SB09/transcripts/anti-stub-audit.txt` - no placeholder/stub markers and no live-proof response markers in changed source/test files.
- `proof/SB09/transcripts/git-diff-check.txt` - no whitespace errors; only Git LF-to-CRLF normalization warnings for edited files.
- `proof/SB09/transcripts/changed-file-hashes.md` - baseline Git blobs and current SHA-256 hashes for production/template/test changes.
- `proof/SB09/transcripts/workbook-regeneration.txt` - regenerated `bundle-checklists.xlsx` and worksheet previews from the updated workbook generator.
- `proof/SB09/transcripts/bundle-validator-completed.txt` - completed-stage bundle validator passed.

## Changed File Hash Evidence

- SHA-256 hash table: `proof/SB09/transcripts/changed-file-hashes.md`.
- Example SHA-256 values:
  - `src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs`: `0f374812721ba043349fc2eeaa653f817e1123e2c575e25ce75c38bf57a0adda`
  - `src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/Seeds/ManagedSeedProviderFallbacks.cs`: `2ca735dfd5af03cb211c0cfbc34e3314a258cf721344b92b1dd8ebce1edc347e`

## Live API Proof

- `proof/SB09/api-local-ollama-run-detail.json` - Financial Strategist agent chat completed with provider `Local Ollama`, model `gemma4-12b-256k`, result `UI-LOCAL-OK`.
- `proof/SB09/api-project-structure-local-ollama-run-detail.json` - project-structure chat completed with provider `Local Ollama`, model `gemma4-12b-256k`, result `PROJECT-UI-OK`.
- `proof/SB09/mcp-playwright-setup-test-after-runtime-adapter-repair-full-editor.json` - live setup test succeeded and discovered Playwright MCP tools with input schemas.
- `proof/SB09/live-playwright-capability-after-runtime-repair.json` - live capability configuration uses `messageFraming: newlineDelimitedJson`, `timeoutSeconds: 120`, and Playwright allowed tools.
- `proof/SB09/api-local-ollama-playwright-mcp-run-detail.json` - Local Ollama API run completed with result `MCP-LOCAL-OK`; tool receipts include `local_mcp_launch`, `browser_navigate`, and `browser_snapshot`.
- `proof/SB09/api-local-ollama-playwright-mcp-execution-result.json` - API execution result for the MCP proof run.

## Live UI Proof

- `proof/SB09/browser-agent-chat-local-ollama-ui.json` and `proof/SB09/screenshots/agent-chat-local-ollama-runtime-details.png` - agents-page local-provider chat proof.
- `proof/SB09/browser-project-structure-local-ollama-ui.json` and `proof/SB09/screenshots/project-structure-local-ollama-runtime-details.png` - project-structure local-provider chat proof.
- `proof/SB09/browser-ui-local-ollama-chat-run-detail.json` - UI-started simple chat completed with provider `Local Ollama`, model `gemma4-12b-256k`, result `UI-SEND-LOCAL-OK`.
- `proof/SB09/browser-ui-local-ollama-playwright-mcp-run-detail.json` - UI-started MCP run completed with provider `Local Ollama`, model `gemma4-12b-256k`, result `MCP-UI-LOCAL-OK`; persisted receipts include `local_mcp_launch`, `browser_navigate`, and `browser_snapshot`.
- `proof/SB09/browser-ui-local-ollama-playwright-mcp-completed.json` - browser DOM text contains `browser_navigate`, `browser_snapshot`, and `MCP-UI-LOCAL-OK`.
- `proof/SB09/screenshots/browser-ui-local-ollama-playwright-mcp-completed.png` - screenshot of the completed UI thread.
- `proof/SB09/transcripts/live-proof-assertions.txt` - mechanical assertions over API/UI proof artifacts all passed.

## Cleanup Proof

- `proof/SB09/temp-local-ollama-playwright-agent-cleanup.json` - disposable API proof agent no longer exists.
- `proof/SB09/temp-ui-local-ollama-playwright-agent-cleanup.json` - disposable UI proof agent no longer exists.

## Notes

- A UI approval-continuation nuance was observed during proof: a non-auto-approved browser tool run reached waiting-on-tool state and completed after approval/auto-approval was applied. SB09 proof used both the persisted approval history and an auto-approved disposable agent to prove the repaired provider/MCP runtime path.
- No app server is required after proof capture; the final web build was run with the app stopped to avoid Windows output locking.
