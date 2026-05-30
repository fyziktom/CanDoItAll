# 02 Run Cost Analytics

## Status

- `Completed`

## Objective

Use provider model pricing to calculate run cost from token usage and propagate it into process and workflow analytics where usage metrics exist.

## Covered Inputs

- User note: prices are needed to correctly calculate process run and workflow run cost.
- User note: analytics surfaces include live processes and process run history.

## Prerequisites

- SB01 provides typed price rows, private-provider metadata, and override validation.
- Runtime metric creation path has access to provider and model information.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`

## Deliverables

- Agent run metrics carry calculated cost.
- Process run actual cost synchronizes from execution metrics when token usage exists.
- Live process analytics calculate observed cost from usage metrics when available.
- Workflow cost coverage is wired through the same pricing helper where workflow usage metrics are present; otherwise the implementation documents the exact existing limitation.

## Dependency Impact

- Process run history and live process analytics consume new cost values.
- Tests must avoid assuming the old target-hours placeholder is the only cost source.

## Validation Depth

- Unit or service tests for cost math and process cost propagation.
- Source-backed proof for workflow coverage or explicit limitation.

## Implementation Steps

1. Add cost fields to run metric records without breaking existing construction.
2. Calculate metric cost in the execution path using provider pricing.
3. Update process cost aggregation from execution metrics.
4. Wire live analytics to prefer usage-cost data when metrics are available.

## Do Not Do

- Do not double-count execution metrics across retries.
- Do not replace estimated cost semantics with actual cost semantics.

## Acceptance Checklist

- Token usage produces a non-zero actual cost when provider pricing exists.
- Cached-input tokens are priced separately from uncached input.
- Live process analytics are not stuck on placeholder lead-hour cost when usage metrics exist.

## Proof Required

- Passing focused tests or command transcripts for cost calculation paths.
- Source proof that workflow cost handling was inspected.

## Browser Validation Logging

- Browser proof is optional unless live process UI layout changes.

## Progression Gate

- SB03 may run in parallel only after SB01 metadata is available; SB02 must finish before final analytics closure.

## Suggested Agent Prompt

Use the shared implementation prompt and implement only the runtime and analytics cost path.
