# 02 Database Catalog Compatibility

## Status

- `Completed`

## Objective

Refresh stale managed Playwright MCP records to the current capability model without overwriting non-managed user records.

## Success Criteria

- Playwright Local MCP template contains `messageFraming: newlineDelimitedJson`.
- Managed records refresh through seed version `2026-06-agent-template-teams-v25`.
- Live development workspace contains v25 and message framing in `configurationJson`.

## Covered Inputs

- R002 Current Playwright MCP Model
- R003 Local Stdio MCP Compatibility

## Prerequisites

- `01-mcp-setup-runtime-repair`

## Exact Source References

- `repo://Templates/Capabilities/mcps.json`
- `repo://Templates/Capabilities/manifest.json`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/CapabilityTemplateSeedMaterializer.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Templates/CapabilityTemplateDtos.cs`
- `repo://src/CanDoItAll.AgentFramework.Capabilities.Templates/CapabilityTemplateValidator.cs`
- `repo://tests/CanDoItAll.Tests.Unit/CapabilityTemplateSeedMaterializationTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/CapabilityTemplateSeedHardeningCheckpointTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`

## Deliverables

- Added `messageFraming` to MCP templates and configuration models.
- Updated Playwright Local MCP seed configuration to `newlineDelimitedJson`.
- Bumped managed seed version to `2026-06-agent-template-teams-v25`.
- Ensured managed seed version is persisted into raw configuration.

## Dependency Impact

- The UI setup test depends on the development workspace record having the current framing field.
- Agent assignment proof depends on managed seed refresh not leaving stale records behind.

## Validation Depth

- Critical data compatibility foundation

## Implementation Steps

1. Extend the template DTO and validator with message framing.
2. Persist message framing through capability setup and runtime descriptors.
3. Bump managed seed version.
4. Verify the live development workspace record.

## Scope Exceptions

- Non-managed user-edited capability records are intentionally not overwritten.

## Do Not Do

- Do not mutate user-owned capability records as part of managed seed refresh.
- Do not infer framing from tool names or command arguments.

## Acceptance Checklist

- Seed materialization tests passed.
- Live development workspace record has v25.
- Live development workspace record has `messageFraming: newlineDelimitedJson`.

## Proof Required

- Focused seed test output.
- Live workspace JSON inspection output.

## Browser Validation Logging

- Route: `/agents?tab=capabilities`
- Viewport: `1920x1080`
- Required actions: inspect Playwright Local MCP setup result after seed refresh.
- Screenshot: `agents-playwright-mcp-setup-passed-large.png`

## Progression Gate

- Downstream subbundles may continue only after the live managed development workspace record has the current model fields.

## Suggested Agent Prompt

```text
Implement this subbundle only. Update the managed capability model and seed compatibility path, prove the development workspace refreshed to the new model, and preserve user-owned records.
```
