# Subbundle 01: Isolated Environment And Agent Bootstrap

## Status

- Current state: `Completed`

## Objective

- Stand up a clean local host backed by artifacts-owned SQLite state and bootstrap a usable project-structure agent token in that environment.

## Covered Inputs

- Fresh SQLite in artifacts
- Local CanDoItAll app target
- Project-structure MCP token bootstrap

## Prerequisites

- Source bundle references exist
- Local solution builds and can host the web app
- Development database endpoints are available

## Exact Source References

- C:/repositories/CanDoItAll/CanDoItAll_CrmHr_CodexBundle_Final/plan/01-phase-plan.md
- C:/repositories/CanDoItAll/src/CanDoItAll.Web/Program.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Web/ProjectStructureAgentApi.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/ProjectStructureAgentSettingsPanel.razor

## Deliverables

- Running isolated local host
- Fresh managed SQLite profile under artifacts
- Saved agent settings and generated token
- Recorded bootstrap evidence in the execution report

## Dependency Impact

- Every later subbundle depends on the isolated host and token staying valid
- If bootstrap fails, no project-structure creation can proceed honestly

## Validation Depth

- Confirm host readiness
- Confirm database selection points to the new artifacts workspace
- Confirm token-authorized MCP calls succeed

## Implementation Steps

- Start the web app with an artifacts-backed control-plane root
- Create a managed SQLite profile through the dev endpoint
- Switch to that profile if needed
- Use the settings UI to save the local base URL and create an enabled project-structure profile
- Extract the generated token and verify it with a lightweight MCP request

## Do Not Do

- Do not reuse a previous SQLite profile
- Do not assume an old token is valid in the fresh database
- Do not mutate the real application data store

## Acceptance Checklist

- Clean SQLite profile exists under artifacts
- Active database selection points at the new profile
- Project-structure agent token is generated in the isolated environment
- Authorized API request succeeds

## Proof Required

- Dev endpoint response proving the new profile
- Browser proof of the settings page and generated token
- Execution report note with the selected workspace root

## Browser Validation Logging

- Record the settings route, viewport, actions, and screenshot paths in `reviews/01-execution-report.md`

## Progression Gate

- Proceed only if the isolated environment is live and project-structure operations can be authorized successfully

## Suggested Agent Prompt

- Bootstrap a fresh local CanDoItAll environment under artifacts and produce a working project-structure MCP token without reusing existing state.
