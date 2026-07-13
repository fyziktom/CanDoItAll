# Normalized Requirements

## R001 MCP Setup Runtime

The Agents capability setup test for Playwright Local MCP must use a real registered runtime client factory and setup-test service in the application host.

Success criteria:

- The setup-test API resolves `IMcpClientFactory` and `IMcpSetupTestService`.
- The Agents capability dialog no longer reports `ImplementationMissing`.
- The live UI shows `Setup passed` after clicking `Test setup`.

## R002 Current Playwright MCP Model

Development workspace records for `playwright-local-mcp` must use the current capability configuration model.

Success criteria:

- Existing managed records refresh via a new seed version.
- `configurationJson` contains `managedSeedVersion: 2026-06-agent-template-teams-v25`.
- `configurationJson` contains `messageFraming: newlineDelimitedJson`.

## R003 Local Stdio MCP Compatibility

The local stdio MCP runtime must start Windows commands predictably and support the framing used by the current Playwright MCP package.

Success criteria:

- Bare `npx` resolves to a runnable Windows command path.
- `ContentLength` and `NewlineDelimitedJson` framing both have unit coverage.
- Playwright MCP list-tools succeeds and returns browser tools.

## R004 Agent Project/Process/Workflow Tool Access

Agents used by project-structure, workflows, and processes must rely on internal runtime providers and typed metadata, not retired process/project MCP catalog records.

Success criteria:

- Seeded integrated agents do not attach legacy project-structure/process MCP capabilities.
- Representative delivery agents have project-structure and process access metadata.
- MAF runtime-provider filtering keeps only tools allowed for the current process step contract.
- Process launch integration tests pass for project-structure launch paths.

## R005 Large-Screen UI Verification

Browser verification must use large-screen dimensions only.

Success criteria:

- Playwright MCP runs at `1920x1080`.
- `/agents?tab=capabilities`, `/projects`, `/agents/workflows`, and `/processes` load without Blazor error banners.
- Screenshots and snapshots are captured for closure evidence.

## R006 Bundle Closure

The verification work must be documented in this bundle with traceability to implementation and proof.

Success criteria:

- Subbundle status and execution report are updated.
- Bundle validator passes after documentation is complete.
