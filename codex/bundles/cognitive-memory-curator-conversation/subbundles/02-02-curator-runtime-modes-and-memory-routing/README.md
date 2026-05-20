# 02 Curator Runtime Modes And Memory Routing

## Status

- State: `Completed`
- Critical foundation: `Yes`

## Objective

Wire curator turns through both runtime modes, `Agent` and `Direct LLM`, while sharing one recall/capture result contract.

## Covered Inputs

- `R-002`, `R-008`
- Raw notes: two modes, standard LLM chat mode, agent mode, conversation input flows into dreaming/clustering.

## Prerequisites

- Subbundle 01 closure gate passed.
- Curator capture service contract exists.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryMafIntegration.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemorySettingsContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderTestingModels.cs`

## Deliverables

- Shared send-turn implementation that performs recall before the curator answer.
- Direct provider LLM mode using configured provider/default profile.
- Agent mode using configured/default agent or explicit request agent.
- Common result with response text, mode, provider/agent metadata, recall trace id, context pack id, included memory ids, captured improvements, and warnings.
- Failure messages for missing project scope, provider, agent, or empty response.

## Dependency Impact

- Subbundle 03 must call this service without caring which runtime mode was used.
- Subbundle 04 must validate both modes.

## Validation Depth

- Deep unit validation.
- Use fake workspace/provider/runtime dependencies where possible.

## Implementation Steps

1. Resolve default provider/default agent from Cognitive Memory settings.
2. Build the curator system/context prompt from recall context pack sections and source refs.
3. Implement direct LLM mode.
4. Implement agent mode through existing workspace service.
5. Feed user turns through capture pipeline after the answer.
6. Add tests for both mode paths and missing configuration failures.

## Scope Exceptions

- No streaming token UI.
- No new provider transport abstraction unless existing APIs cannot support the mode.

## Do Not Do

- Do not duplicate capture persistence separately per mode.
- Do not silently fall back from missing direct provider to agent mode or vice versa.
- Do not let agent mode require manual tool approvals in curator conversation.

## Acceptance Checklist

- Both runtime modes compile and return the same result shape.
- Direct mode uses configured provider/model.
- Agent mode uses configured/default agent.
- Missing configuration throws actionable errors.
- Captured improvements are created from both modes.

## Proof Required

- Unit tests covering direct mode, agent mode, and failure paths.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter CognitiveMemory`

## Browser Validation Logging

- N/A. Runtime behavior is surfaced in subbundle 03.

## Progression Gate

- Pass only when both modes use the same contract.
- Captured improvements must route into the subbundle 01 pipeline.

## Suggested Agent Prompt

Implement subbundle 02 only. Wire curator conversation runtime modes through one service result and one capture path. Add tests for direct mode, agent mode, and missing configuration errors.
