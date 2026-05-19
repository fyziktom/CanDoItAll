# Agent Context Policy And DTOs

## Status

- `Completed`

## Objective

- Separate agent-facing Cognitive Memory context from recall diagnostics and make process-critical fail/skip behavior explicit.

## Success Criteria

- MAF contributor builds a dedicated agent context package.
- Interactive or optional modes can skip unavailable memory with metadata.
- Process-critical modes fail predictably when memory is required and unavailable.
- Tests cover skip, provided, and process-critical failure paths.

## Covered Inputs

- CM-P0-004 agent-facing DTO separation.
- CM-P0-005 explicit process-critical policy.

## Prerequisites

- Subbundle 01 compile gate passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryMafIntegration.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Context\AgentContextContributionContracts.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs

## Deliverables

- Agent context package/builder types.
- Explicit policy method for required vs optional memory.
- Unit tests.

## Dependency Impact

- Docs and roadmap cannot claim process-critical memory behavior is beta-ready without these tests.

## Validation Depth

- Process-critical closure.

## Implementation Steps

1. Inspect existing MAF request policy modes.
2. Add dedicated agent context package/builder.
3. Update contributor to use explicit fail/skip policy.
4. Add targeted tests.

## Scope Exceptions

- Does not redesign all MAF context contribution APIs.

## Do Not Do

- Do not expose raw diagnostic recall payloads as agent answer context.
- Do not silently skip required memory in process-critical mode.

## Acceptance Checklist

- Agent context text is built from dedicated type.
- Metadata remains useful and non-sensitive.
- Process-critical unavailable memory fails in tests.

## Proof Required

- `AgentContextContributionTests` pass.

## Proof Captured

- Added `CognitiveMemoryAgentContextPackage` for agent-facing context.
- Updated `CognitiveMemoryAgentContextContributor` to render from that package instead of raw diagnostic recall payloads.
- Added explicit required-memory policy for governed process automation, auto-approved non-interactive runs, and A2A endpoint mode.
- Added tests for process-critical missing-scope and unavailable-memory failure.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory|FullyQualifiedName~AgentContextContributionTests" --logger "console;verbosity=minimal" -m:1` passed 136/136.

## Browser Validation Logging

- N/A - service behavior only.

## Progression Gate

- Docs closure may continue only after agent context tests pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
