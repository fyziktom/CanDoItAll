# 01 MCP Setup Runtime Repair

## Status

- `Completed`

## Objective

Replace the missing setup-test runtime implementation with a real local stdio MCP runtime path.

## Success Criteria

- The application host registers a live `IMcpClientFactory`.
- The application host registers `IMcpSetupTestService`.
- Playwright Local MCP setup test starts the MCP server and lists tools.
- The UI no longer reports `ImplementationMissing`.

## Covered Inputs

- R001 MCP Setup Runtime
- R003 Local Stdio MCP Compatibility
- Screenshot failure from the user report

## Prerequisites

- none

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpClientFactory.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs`
- `repo://tests/CanDoItAll.Tests.Unit/McpRuntimeContractsTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/AgentCapabilitySetupApiIntegrationTests.cs`

## Deliverables

- Added `LocalStdioMcpClientFactory`.
- Registered `IMcpClientFactory` and `IMcpSetupTestService` in the application host.
- Added Windows command resolution for bare stdio commands such as `npx`.
- Added `ContentLength` and `NewlineDelimitedJson` JSON-RPC framing support.

## Dependency Impact

- This is the foundation for database compatibility and UI proof.
- Without a real runtime factory, later setup validation would only prove editor behavior, not MCP runtime behavior.

## Validation Depth

- Critical foundation

## Implementation Steps

1. Register live MCP setup services.
2. Implement local stdio MCP runtime startup and cleanup.
3. Implement dual JSON-RPC framing.
4. Cover the framing and setup API contracts with tests.
5. Verify the live UI setup button.

## Scope Exceptions

- No small or medium viewport validation by user request.

## Do Not Do

- Do not add a mock setup success path.
- Do not silently fall back to another framing mode after parse failure.
- Do not restore legacy project/process MCP catalog records.

## Acceptance Checklist

- `McpRuntimeContractsTests` passed.
- `AgentCapabilitySetupApiIntegrationTests` passed.
- Playwright MCP UI setup test showed `Setup passed`.

## Proof Required

- Focused unit and integration test output.
- `agents-playwright-mcp-setup-passed-large.png`
- `agents-playwright-mcp-setup-passed-large.yml`

## Browser Validation Logging

- Route: `/agents?tab=capabilities`
- Viewport: `1920x1080`
- Required actions: open Playwright Local MCP details, open Configuration tab, click `Test setup`, wait for `Setup passed`.
- Screenshot: `agents-playwright-mcp-setup-passed-large.png`

## Progression Gate

- Downstream subbundles may continue only after the live setup path passes and the service registration tests pass.

## Suggested Agent Prompt

```text
Implement this subbundle only. Preserve the existing application host boundaries, add a real MCP setup runtime path, prove it with focused tests and Playwright MCP UI evidence, and stop if the setup result is not genuinely successful.
```
