# Current State

## Reported Failure

The user-provided screenshot showed the Agents capability details dialog for `Playwright Local MCP` on `http://localhost:5032/agents?tab=capabilities`. Clicking `Test setup` returned:

```text
ImplementationMissing
No MCP setup-test runtime client factory is registered for this application host.
Register IMcpSetupTestService with an IMcpClientFactory before running live MCP start/list-tools tests.
```

## Root Cause Findings

- The application host did not register a live MCP setup-test runtime client factory.
- After adding the service path, local stdio startup also needed Windows-safe bare-command resolution for `npx`.
- The current `@playwright/mcp@latest` stdio server speaks newline-delimited JSON, while the runtime assumed only `Content-Length` JSON-RPC framing.
- The development workspace had a managed v24 `playwright-local-mcp` capability record, so model additions would not refresh without a seed version bump.

## Product Changes Made

- Registered `IMcpClientFactory` as `LocalStdioMcpClientFactory` and `IMcpSetupTestService` as `McpSetupTestService` in the AgentFramework module service registrations.
- Added local stdio MCP runtime support for process start, initialize, tools/list, tools/call, cleanup, command resolution, and dual framing.
- Added `McpStdioMessageFraming` and carried `messageFraming` through descriptors, templates, editor models, setup validation, and MAF runtime catalog descriptors.
- Updated Playwright Local MCP seed configuration to `messageFraming: newlineDelimitedJson`.
- Bumped the capability pack seed version to `2026-06-agent-template-teams-v25`.
- Hardened seed materialization so managed seed version is persisted into raw configuration dictionaries.

## Runtime Evidence

- Live development workspace now stores `playwright-local-mcp` with `managedSeedVersion: 2026-06-agent-template-teams-v25`.
- Live development workspace now stores `messageFraming: newlineDelimitedJson`.
- Playwright MCP UI setup test passes in the capability dialog.
- Large-screen smoke checks passed for `/projects`, `/agents/workflows`, and `/processes`.
