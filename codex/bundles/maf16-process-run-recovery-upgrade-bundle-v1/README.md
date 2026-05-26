# MAF 1.6 + Process Run Recovery Upgrade Bundle

## Status

Completed by Codex execution.


## Validation Summary

- Bundle preparation status: Prepared
- Bundle readiness gate: Passed
- Execution status: Completed
- Subbundle gate review: Passed
- Final closure gate: Passed
- Browser validation analytics: Passed
## Mission

Upgrade CanDoItAll from Microsoft Agent Framework 1.3 to 1.6.x first, then repair the concrete process failure observed in live run:

- Process run: `9bbc0667-9d12-4506-ba81-654ef924cad6`
- Failed step: `0610f6d6-5d37-4313-b560-09cc9484f5b8`
- Step title: `Resolve Blazor delivery contract`
- Failure: required artifact validation rejected `Blazor delivery contract` as `StaleOrWrongRun`

## Why upgrade MAF first

The current `CanDoItAll.AgentFramework.Maf` project still references:

- `Microsoft.Agents.AI` `1.3.0`
- `Microsoft.Agents.AI.A2A` `1.3.0-preview.260423.1`
- `Microsoft.Agents.AI.OpenAI` `1.3.0`
- `Microsoft.Agents.AI.Workflows` `1.3.0`

Current NuGet package pages show `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` at `1.6.2`. The official release notes also mention .NET 1.6 line changes around message injection, hosted files, stream-error input persistence, handoff role mutation fixes, workflow evaluation expected output, file store improvements, A2A v1.0, and a breaking OpenTelemetry wrapper change.

This means the upgrade can affect:

- `MafAgentRuntime.AgentFactory`
- provider adapters
- tool approval
- finalizer capture
- structured output repair
- local MCP handling
- hosted/remote A2A
- workflows
- telemetry / OpenTelemetry
- agent skills discovery
- process automation dispatch

## Process failure summary

The first process step failed after the agent execution succeeded. A process artifact record exists and the step view says the required artifact is satisfied, but finalization failed with `StaleOrWrongRun`.

Important facts from the captured evidence:

- artifact record is linked to the current `StepRunId`
- artifact record is linked to the current `ArtifactExpectationId`
- lineage has `sourceExecutionRunId` equal to the current step execution run
- lineage `contentHash` is empty
- managed path is organization-scoped: `artifacts/scopes/organization/.../process-runs/{runId}/...`
- external reference key uses a different path shape: `artifacts/process-runs/{runId}/...`
- run health reports missing artifact count `0`, but the step still failed required artifact validation

## Execution rule

Do not fix the process runtime before the MAF upgrade compile/test gate. Upgrade MAF first, stabilize the adapter, then diagnose process validation with new tests.

