# Target Solution

Describe the intended end state, important boundaries, and allowed side effects.
# Target Solution

## Runtime Boundary

The setup-test path should be a real runtime path, not a UI-only mock. The application host registers:

- `IMcpClientFactory` -> `LocalStdioMcpClientFactory`
- `IMcpSetupTestService` -> `McpSetupTestService`

The setup flow builds a typed MCP descriptor from the editor model and asks the runtime client to initialize and list tools. Failures are returned as typed diagnostics.

## MCP Descriptor Model

Local stdio MCP descriptors include a strongly typed `McpStdioMessageFraming` value:

- `ContentLength`
- `NewlineDelimitedJson`

The template, setup editor, capability setup service, MAF runtime descriptor factory, and persisted configuration all use this model. This avoids stringly typed branching at the runtime boundary while still accepting JSON aliases from templates.

## Seed Compatibility

Managed seed records are refreshed by seed version. The Playwright Local MCP seed is now versioned as `2026-06-agent-template-teams-v25`, and the materializer writes `managedSeedVersion` into configuration when enabled.

## Agent Tool Access

Project-structure and process tooling remain internal runtime-provider capabilities. The target architecture does not restore old project/process MCP server records. Runtime access is controlled by:

- `AgentProjectStructureAccessMetadata`
- `AgentProcessAccessMetadata`
- `AgentWorkspaceToolAccessMetadata`
- MAF runtime-provider filtering against process operation contracts

This keeps UI, application services, domain access metadata, and infrastructure runtime tooling separated.
