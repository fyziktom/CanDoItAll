# 01 Provider Pricing Foundation

## Status

- `Completed`

## Objective

Add typed provider pricing and private-provider metadata with defaults, persistence, and validation for manual model overrides.

## Covered Inputs

- User note: providers must allow a price table for each model.
- User note: manual model overrides must also fill price.
- User note: OpenAI prices must follow the official pricing page.
- User note: Ollama/private models need realistic editable defaults.

## Prerequisites

- Official OpenAI pricing page checked on 2026-05-30.
- Existing provider metadata JSON flow understood.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Editors/EditorModels.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderMetadata.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs`

## Deliverables

- Typed pricing records and calculation primitives.
- Default pricing rows for OpenAI and Ollama/private-style providers.
- Provider metadata JSON read/write support for price rows and private flag.
- Agent save validation that rejects manual overrides without a matching price row.

## Dependency Impact

- SB02 consumes typed prices to calculate run costs.
- SB03 consumes the private-provider flag to render badges.

## Validation Depth

- Unit tests for default price normalization and token cost math.
- Negative test for agent override without provider price.

## Implementation Steps

1. Add pricing and private-provider properties to provider/editor models.
2. Extend provider metadata parsing and serialization.
3. Normalize pricing defaults during provider load/save.
4. Enforce explicit override price validation in the agent save path.

## Do Not Do

- Do not silently assign zero cost for missing models.
- Do not introduce a separate billing database model.

## Acceptance Checklist

- Provider pricing rows include input, cached-input, and output prices.
- OpenAI defaults reflect the checked official pricing page.
- Ollama/private defaults are non-zero and editable.
- Missing override pricing fails predictably.

## Proof Required

- Passing focused tests for pricing defaults and override validation.
- Source proof showing provider metadata persistence.

## Browser Validation Logging

- No mandatory browser proof for SB01 unless provider editor UI is changed.

## Progression Gate

- SB02 and SB03 must not start until typed pricing and private-provider metadata are available.

## Suggested Agent Prompt

Use the shared implementation prompt and implement only the provider pricing foundation.
